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
    private readonly IFileFacts _files;
    private readonly LocalBackendOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWhisperBackend"/> class.
    /// </summary>
    /// <param name="runner">The seam every child process is started through.</param>
    /// <param name="files">The seam the readiness probe looks at a path through.</param>
    /// <param name="options">The tool and the model this backend was configured with.</param>
    public LocalWhisperBackend(IProcessRunner runner, IFileFacts files, LocalBackendOptions options)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _files = files ?? throw new ArgumentNullException(nameof(files));
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
    /// detected language on its diagnostic stream, and this backend does not read
    /// it. A request that names no language is refused rather than answered with a
    /// language nobody measured.
    ///
    /// The confidence flag follows from that and is not a second decision. A
    /// backend that detects nothing reports no confidence in nothing, and #31's
    /// floor never reaches this backend while both are false. Whoever turns
    /// detection on here decides both at once, because the diagnostic line that
    /// carries the language also carries the probability beside it.
    /// </remarks>
    public BackendDescription Description { get; } = new(
        BackendName,
        _publishedModels,
        Array.Empty<string>(),
        canDetectLanguage: false,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.FromSeconds(10));

    /// <inheritdoc />
    /// <remarks>
    /// This looks. It reads what is at each of the two paths and answers from what
    /// it found, so an operator learns that a path is wrong from the page rather
    /// than from a run that fails on its first item hours later.
    ///
    /// WHAT IT DOES NOT DO is run the tool. Two things follow from that and both
    /// are said here rather than implied by a green answer: the version string the
    /// tool prints is not reported, because which invocation makes a whisper.cpp
    /// compatible tool print one is not settled anywhere in this tree, and whether
    /// the model loads is unknown, because the only thing that knows is the tool
    /// with the model in front of it. A ready answer here means the two paths hold
    /// files this plugin could hand to a run, and no more than that.
    ///
    /// The order is the order an operator fixes them in. The tool first, because a
    /// missing tool makes the model irrelevant, and the first thing wrong is the
    /// only thing reported: a page listing every fault at once is a page somebody
    /// reads none of.
    /// </remarks>
    public async Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsComplete)
        {
            return new BackendReadiness(
                false,
                "The local backend needs a path to a whisper.cpp compatible tool and a path to a model file. Set both on the plugin's configuration page.");
        }

        // Linked rather than replacing the caller's token, so an operator who
        // navigates away from the page stops the probe, and the deadline stops one
        // they are still waiting on. Which of the two fired is what the catch below
        // tells apart, and reporting a deadline as a cancelled probe would leave
        // the page saying nothing at all.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.ProbeTimeout);

        try
        {
            var tool = await _files.DescribeAsync(_options.ExecutablePath!, deadline.Token).ConfigureAwait(false);

            if (!tool.Exists)
            {
                return NotReady("There is no file at the transcription tool path, {0}.", _options.ExecutablePath!);
            }

            if (tool.IsExecutable == false)
            {
                return NotReady(
                    "The file at the transcription tool path, {0}, may not be executed by the account this server runs as.",
                    _options.ExecutablePath!);
            }

            var model = await _files.DescribeAsync(_options.ModelPath!, deadline.Token).ConfigureAwait(false);

            if (!model.Exists)
            {
                return NotReady("There is no file at the model path, {0}.", _options.ModelPath!);
            }

            if (model.SizeInBytes < LocalBackendOptions.SmallestPlausibleModelBytes)
            {
                return NotReady(
                    "The file at the model path, {0}, is {1} bytes, which is far too small to be a whisper model. A download that was refused and saved anyway looks like this.",
                    _options.ModelPath!,
                    model.SizeInBytes.ToString(CultureInfo.InvariantCulture));
            }

            return new BackendReadiness(true, null);
        }
        catch (OperationCanceledException)
        {
            // The caller's token wins the tie, for the same reason it wins it in a
            // transcription: somebody who stopped the probe themselves has not
            // discovered anything about their file system.
            cancellationToken.ThrowIfCancellationRequested();

            return NotReady(
                "The file system did not answer about the configured paths within {0}, so nothing is known about either.",
                _options.ProbeTimeout.ToString("g", CultureInfo.InvariantCulture));
        }
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

    /// <summary>
    /// A refusal with the path in it, formatted once so every reason reads the same.
    /// </summary>
    /// <remarks>
    /// The path is quoted back because the operator typed it and a trailing space or
    /// a path they meant to change is invisible in a settings field and obvious in a
    /// sentence. Neither path is a secret: the key that is one belongs to the other
    /// backend and never comes near this class.
    /// </remarks>
    private static BackendReadiness NotReady(string sentence, params object[] values) =>
        new(false, string.Format(CultureInfo.InvariantCulture, sentence, values));

    private static string Describe(string standardError) =>
        string.IsNullOrWhiteSpace(standardError)
            ? "It said nothing about why."
            : standardError.Trim();
}
