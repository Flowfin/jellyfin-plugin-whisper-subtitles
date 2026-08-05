using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// The one narrow place the heavy runtime sits behind.
/// </summary>
/// <remarks>
/// Everything else in the plugin talks to this and to nothing else, so a backend
/// can be added or removed without the task, the output writer or the
/// configuration page changing. That is the whole reason the interface returns
/// timed segments rather than a formatted subtitle file: formatting, naming and
/// marking belong to this plugin and must not differ between backends.
/// </remarks>
public interface ITranscriptionBackend
{
    /// <summary>
    /// Gets what this backend offers.
    /// </summary>
    /// <remarks>
    /// Answering this must not start a transcription and must not require the
    /// backend to be ready.
    /// </remarks>
    BackendDescription Description { get; }

    /// <summary>
    /// Asks whether the backend is usable right now.
    /// </summary>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>Whether the backend can transcribe, and what stands in the way when it cannot.</returns>
    /// <remarks>
    /// This answers without transcribing anything, so an administrator surface can
    /// ask it as often as it likes.
    /// </remarks>
    Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Turns a media duration into the wall-clock time this backend expects to need.
    /// </summary>
    /// <param name="mediaDuration">How long the media is.</param>
    /// <returns>The range the backend expects to take.</returns>
    /// <remarks>
    /// A hint and not a promise. It exists so a dry run can say what the work will
    /// cost before an operator commits a machine to it.
    /// </remarks>
    CostEstimate EstimateCost(TimeSpan mediaDuration);

    /// <summary>
    /// Transcribes one audio file.
    /// </summary>
    /// <param name="request">What to transcribe, and in which language.</param>
    /// <param name="progress">Where to report how far along the work is, as a fraction between zero and one.</param>
    /// <param name="cancellationToken">Stops the transcription.</param>
    /// <returns>The timed segments, and the language they are in.</returns>
    /// <remarks>
    /// Cancellation is part of the contract. A backend that cannot stop within the
    /// time its description states does not satisfy this interface.
    /// </remarks>
    Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}
