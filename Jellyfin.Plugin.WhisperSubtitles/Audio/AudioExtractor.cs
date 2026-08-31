using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// Turns one audio stream of one item into the file a backend can read.
/// </summary>
/// <remarks>
/// The media tool is the server's own. Jellyfin already knows where a working
/// one is and exposes it as <c>IMediaEncoder.EncoderPath</c>, and that is the
/// only place the path handed to this type may come from. This plugin ships no
/// media tool, downloads none, and does not go looking for one on the machine:
/// a plugin that searched a path would eventually find a different build from
/// the one the server transcodes with, and the failures of that arrangement
/// arrive months later on somebody else's container. Wiring the real value in is
/// the composition root, in #71.
///
/// The temporary file is created in a directory this plugin owns rather than in
/// the system temporary directory. Sharing a directory with everything else on
/// the machine means a sweep of stale files cannot tell its own leftovers from
/// somebody else's, and that sweep is what covers the case where the server died
/// between writing a file and deleting it.
///
/// <see cref="TemporaryAudioSweep"/> is that sweep. Nothing in this tree calls it
/// at the start of a run yet, because a run has no items to be before, which is
/// the half of #21 that is still open.
/// </remarks>
public sealed class AudioExtractor
{
    private readonly IProcessRunner _runner;
    private readonly string _encoderPath;
    private readonly string _workingDirectory;
    private readonly long _ceilingBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioExtractor"/> class.
    /// </summary>
    /// <param name="runner">The seam every program this plugin starts goes through.</param>
    /// <param name="encoderPath">The server's own media tool, from <c>IMediaEncoder.EncoderPath</c>.</param>
    /// <param name="workingDirectory">The directory this plugin owns and writes its temporary audio into.</param>
    /// <param name="ceilingBytes">The most one extracted file may reach.</param>
    public AudioExtractor(
        IProcessRunner runner,
        string encoderPath,
        string workingDirectory,
        long ceilingBytes = PcmAudio.DefaultCeilingBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThan(ceilingBytes, 1);

        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _encoderPath = encoderPath;
        _workingDirectory = workingDirectory;
        _ceilingBytes = ceilingBytes;
    }

    /// <summary>
    /// Extracts one stream and hands back the file.
    /// </summary>
    /// <param name="mediaPath">The media file to read.</param>
    /// <param name="stream">The stream to take, chosen by <see cref="AudioStreamChoice"/>.</param>
    /// <param name="cancellationToken">Ends the tool rather than only stopping the wait for it.</param>
    /// <returns>The extracted audio, which the caller disposes.</returns>
    public async Task<ExtractedAudio> ExtractAsync(
        string mediaPath,
        AudioStreamDescription stream,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ArgumentNullException.ThrowIfNull(stream);

        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_workingDirectory);

        // A name per attempt rather than a name per item. Two runs of the same
        // item, or a run overlapping the leftovers of one that died, must not be
        // able to read each other's half-written file.
        var outputPath = Path.Combine(
            _workingDirectory,
            string.Create(CultureInfo.InvariantCulture, $"{Guid.NewGuid():N}.wav"));

        var invocation = FfmpegArguments.Build(_encoderPath, mediaPath, stream.Index, outputPath, _ceilingBytes);

        IStartedProcess process;

        try
        {
            process = _runner.Start(invocation);
        }
        catch (Exception started) when (started is not OperationCanceledException)
        {
            // A tool that could not be started is a different state from one that
            // ran and failed, and the two want different actions from an operator.
            // Nothing was written, so there is nothing to remove.
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.BackendUnreachable,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The server's media tool at \"{0}\" could not be started, so no audio could be extracted.",
                    _encoderPath),
                started);
        }

        try
        {
            using (process)
            using (cancellationToken.Register(process.Kill))
            {
                // After the registration and before the wait. After, because a
                // cancellation surfacing out of the ask has to reach a child that
                // is already ending; before, because a decode asked to step down
                // once it has finished has run at the ordinary priority for all of
                // the time that mattered.
                AskToRunBelowOrdinaryWork(process);

                var exitCode = await process.WaitForExitAsync().ConfigureAwait(false);

                // After the wait rather than before it. A token cancelled while the
                // tool was running has already killed it, and the exit code that
                // produced says nothing about the audio.
                cancellationToken.ThrowIfCancellationRequested();

                if (exitCode != 0)
                {
                    throw new TranscriptionFailedException(
                        TranscriptionFailureReason.AudioUnreadable,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The server's media tool ended with exit code {0} and extracted no usable audio: {1}",
                            exitCode,
                            process.StandardError));
                }
            }

            return Measure(outputPath);
        }
        catch
        {
            // Every exit path that is not a returned file removes the file, here,
            // rather than in a handler the caller has to remember to write. That
            // includes cancellation, where the tool was killed mid-write and what
            // it left is a truncated WAV with a plausible name.
            Delete(outputPath);
            throw;
        }
    }

    /// <summary>
    /// Asks the media tool to be scheduled below the work this machine was bought
    /// for, and treats a refusal as nothing.
    /// </summary>
    /// <remarks>
    /// The decode is the second thing this plugin runs that takes every core it is
    /// given, and it runs beside a server that is streaming to somebody. The same
    /// argument that lowers the transcription child in
    /// <see cref="Backends.Local.LocalWhisperBackend"/> reaches this one: a run
    /// that finished sooner and made playback stutter is the failure #22 asks
    /// against, and a lower priority costs a machine with a core to spare nothing,
    /// because it is consulted only when something else wants the processor.
    ///
    /// Best effort by design. Lowering a priority is not available on every
    /// platform and not permitted to every account, and an extraction that ran at
    /// the ordinary priority is a worse run rather than a failed one, so a refusal
    /// must not reach the item.
    ///
    /// The swallow is HERE rather than behind the seam, so a double that refuses
    /// can be handed to a run and the item watched surviving it. Inside
    /// <see cref="Backends.Local.SystemStartedProcess"/> the same swallow would be
    /// a promise nothing in the suite could read.
    ///
    /// A CANCELLATION IS NOT SWALLOWED, AND IT ENDS THE TOOL ON THE WAY OUT. The
    /// registration above fires on the token and not on an exception, so an ask
    /// that unwound this method by itself would leave the tool decoding an item
    /// nobody is waiting for any more. The kill is safe to ask for twice, which
    /// is what covers the case where the registration has already asked.
    ///
    /// WHAT THIS DOES NOT DO IS LOG, for the same reason the transcription child's
    /// ask does not: this plugin does not log at all, docs/logging.md is the table
    /// the first change that logs is measured against, and the three tests that
    /// hold it are owed by #73. So an operator whose platform refuses this is not
    /// told, and the half of #22's sentence that asks for a log line stays owed.
    /// </remarks>
    /// <param name="process">The tool that has just been started.</param>
    private static void AskToRunBelowOrdinaryWork(IStartedProcess process)
    {
        try
        {
            process.LowerPriority();
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            // Nothing here can repair it and nothing may report it yet. The
            // remarks above say why both halves are deliberate.
        }
        catch (OperationCanceledException)
        {
            process.Kill();

            throw;
        }
    }

    private ExtractedAudio Measure(string outputPath)
    {
        var file = new FileInfo(outputPath);

        if (!file.Exists)
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.AudioUnreadable,
                "The server's media tool reported success and wrote no file, so there is nothing to transcribe.");
        }

        if (file.Length <= PcmAudio.HeaderBytes)
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.AudioUnreadable,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The server's media tool reported success and wrote {0} byte(s), which is a header and no audio.",
                    file.Length));
        }

        if (file.Length >= _ceilingBytes)
        {
            // The tool was told to stop at the ceiling, so a file that reached it
            // is the item running out of room rather than finishing. Transcribing
            // it would produce a subtitle that stops partway through with nothing
            // saying why, which is worse than a failure that names the ceiling.
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.AudioUnreadable,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Extracting this item reached the {0} byte ceiling, so the audio is incomplete and nothing was transcribed. About {1} byte(s) of this format is one hour.",
                    _ceilingBytes,
                    PcmAudio.BytesPerSecond * 3600));
        }

        return new ExtractedAudio(outputPath, file.Length);
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Already unwinding through a failure that has a better reason than
            // this one. What is left behind is what TemporaryAudioSweep collects,
            // which is the case a handler cannot cover anyway.
        }
        catch (UnauthorizedAccessException)
        {
            // Same, and the same sweep collects it.
        }
    }
}
