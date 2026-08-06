namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// Why one attempt at one item ended without a subtitle.
/// </summary>
/// <remarks>
/// The list is short on purpose and is not the whole failure vocabulary this
/// plugin will have; #32 owns that. What is needed here is the division the retry
/// policy turns on, so every value added later has to answer one question: would
/// trying again tomorrow, unchanged, plausibly produce a different result.
/// </remarks>
public enum TranscriptionFailureReason
{
    /// <summary>
    /// The operator stopped the run, or the server did. Nothing was learned about
    /// the item.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The configured backend was not usable when the item came up.
    /// </summary>
    BackendNotReady,

    /// <summary>
    /// The remote endpoint could not be reached, or the local tool could not be
    /// started.
    /// </summary>
    BackendUnreachable,

    /// <summary>
    /// The backend ran and failed, for a reason it did not describe as permanent.
    /// </summary>
    BackendFailed,

    /// <summary>
    /// The item has no audio stream, so there is nothing to transcribe.
    /// </summary>
    NoAudioStream,

    /// <summary>
    /// The audio is there and cannot be read or decoded.
    /// </summary>
    AudioUnreadable,

    /// <summary>
    /// The backend produced output this plugin could not parse into segments.
    /// </summary>
    OutputUnparseable
}
