using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// Drives a whisper.cpp compatible command line tool as a child process.
/// </summary>
/// <remarks>
/// Out of process, which is the shape the plan assumes: a native fault inside an
/// inference library ends one transcription instead of the media server, and
/// killing the child is a guarantee that the memory comes back. The cost is a
/// process launch and an audio file per item, and output that has to be read
/// rather than returned.
///
/// The tool and the model are paths an operator typed. Neither is downloaded and
/// neither is interpreted: they go into an argument vector, so nothing this
/// plugin does gives a quoting rule a say in what runs.
/// </remarks>
public sealed class LocalWhisperBackend : ITranscriptionBackend
{
    /// <summary>
    /// The name this backend reports.
    /// </summary>
    public const string BackendName = "Local";

    /// <summary>
    /// The model sizes whisper.cpp publishes, smallest first.
    /// </summary>
    /// <remarks>
    /// The names rather than the sizes. What each one costs on disk and in memory
    /// is what an operator needs in front of them while choosing, which is the
    /// configuration page in #36 and the guide in #56, and a second copy of those
    /// figures here would be a second copy to drift.
    /// </remarks>
    private static readonly string[] _publishedModels = { "tiny", "base", "small", "medium", "large" };

    private readonly IProcessRunner _runner;
    private readonly LocalBackendOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWhisperBackend"/> class.
    /// </summary>
    /// <param name="runner">The seam every child process is started through.</param>
    /// <param name="options">The tool and the model this backend was configured with.</param>
    public LocalWhisperBackend(IProcessRunner runner, LocalBackendOptions options)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The language list is empty, and that is a statement rather than a gap:
    /// which languages a run can produce is a property of the model file the
    /// operator supplied, and this plugin never opens a model file. The shape of
    /// <see cref="BackendDescription"/> cannot yet tell "offers none" from "does
    /// not enumerate", which is written here rather than guessed at by a caller.
    ///
    /// Detection is off for the same reason it is honest: the tool reports a
    /// detected language on its diagnostic stream, and reading that is #31. Until
    /// then a request that names no language is refused rather than answered with
    /// a language nobody measured.
    /// </remarks>
    public BackendDescription Description { get; } = new(
        BackendName,
        _publishedModels,
        Array.Empty<string>(),
        canDetectLanguage: false,
        cancellationBudget: TimeSpan.FromSeconds(10));

    /// <inheritdoc />
    /// <remarks>
    /// What this checks is that the operator has named both paths. It does not
    /// check that either file is there, that the tool runs, or that the model
    /// loads, and it says so in the reason it gives rather than implying it
    /// looked. The probe that does look is #15.
    /// </remarks>
    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsComplete)
        {
            return Task.FromResult(new BackendReadiness(
                false,
                "The local backend needs a path to a whisper.cpp compatible tool and a path to a model file. Set both on the plugin's configuration page."));
        }

        return Task.FromResult(new BackendReadiness(true, null));
    }

    /// <inheritdoc />
    /// <remarks>
    /// A placeholder range, and it is marked as one wherever it is shown. Nothing
    /// here has measured this machine, this model or this thread count, and a
    /// number that has measured none of those is not an estimate. #38 replaces it
    /// with a factor calibrated on the operator's own library, which is the only
    /// version of this that is honest.
    ///
    /// It is still linear in the media duration, so a longer item never costs less
    /// than a shorter one, which is the one property a caller may rely on.
    /// </remarks>
    public CostEstimate EstimateCost(TimeSpan mediaDuration) =>
        new(mediaDuration, mediaDuration * 10);

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        if (!_options.IsComplete)
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.BackendNotReady,
                "The local backend has no tool path or no model path.");
        }

        if (string.IsNullOrWhiteSpace(request.Language))
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.BackendNotReady,
                "The local backend cannot be asked to detect a language yet, so a request has to name one.");
        }

        var invocation = BuildInvocation(request);

        IStartedProcess process;
        try
        {
            process = _runner.Start(invocation);
        }
        catch (Exception started) when (started is not OperationCanceledException)
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.BackendUnreachable,
                "The transcription tool could not be started.",
                started);
        }

        using (process)
        {
            try
            {
                // Two routes to the same kill, and both are needed.
                //
                // The registration is for the tool that has stopped printing and is
                // still working, which is where a transcription spends most of its
                // life: nothing is going to come back through the loop, so nothing
                // downstream would notice the token at all.
                //
                // The catch below is for the case the registration cannot cover. A
                // cancellation callback that resumes an awaiting continuation inline
                // can unwind this method, and disposing the registration, before the
                // callback registered ahead of it has had its turn. Then the tool
                // outlives the run that started it, on the operator's machine, with
                // nobody reading what it prints.
                using var killOnCancellation = cancellationToken.Register(process.Kill);

                var reader = new WhisperOutputReader();

                await foreach (var line in process.StandardOutputLines.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (!reader.TryAccept(line, out var problem))
                    {
                        process.Kill();

                        throw new TranscriptionFailedException(
                            TranscriptionFailureReason.OutputUnparseable,
                            "The transcription tool printed output this plugin could not read. " + problem);
                    }
                }

                var exitCode = await process.WaitForExitAsync().ConfigureAwait(false);

                // After the output and the exit code, not before either. Cancellation
                // arriving mid-item ends as cancelled, and a tool killed by this plugin
                // exits non-zero, which would otherwise be reported as the tool failing.
                cancellationToken.ThrowIfCancellationRequested();

                if (exitCode != 0)
                {
                    throw new TranscriptionFailedException(
                        TranscriptionFailureReason.BackendFailed,
                        "The transcription tool ended with exit code "
                        + exitCode.ToString(CultureInfo.InvariantCulture)
                        + ". "
                        + Describe(process.StandardError));
                }

                progress.Report(1);

                return new TranscriptionResult(reader.Segments, request.Language);
            }
            catch (OperationCanceledException)
            {
                process.Kill();

                throw;
            }
        }
    }

    /// <summary>
    /// Builds the argument vector, one element per argument.
    /// </summary>
    /// <remarks>
    /// The flags are whisper.cpp's: the model, the audio file and the language.
    /// Nothing asks the tool to write a file, because the transcript is read from
    /// its standard output and a file the plugin did not ask for is a file
    /// somebody has to clean up.
    /// </remarks>
    private ProcessInvocation BuildInvocation(TranscriptionRequest request)
    {
        var arguments = new List<string>
        {
            "-m",
            _options.ModelPath!,
            "-l",
            request.Language!,
            "-f",
            request.AudioFilePath,
        };

        return new ProcessInvocation(_options.ExecutablePath!, arguments);
    }

    private static string Describe(string standardError) =>
        string.IsNullOrWhiteSpace(standardError)
            ? "It said nothing about why."
            : standardError.Trim();
}
