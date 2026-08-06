using System;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// A transcription that ended without segments, carrying which of the known
/// reasons it was.
/// </summary>
/// <remarks>
/// The reason is on the exception rather than in its message. A caller has to
/// decide whether to retry the item, whether to quarantine it and what to tell
/// the operator, and reading a sentence to make those decisions is how three
/// callers end up agreeing on two of them.
///
/// The vocabulary is <see cref="TranscriptionFailureReason"/>, which is short on
/// purpose and grows in #32. What is here is the division the retry policy
/// already turns on.
/// </remarks>
public sealed class TranscriptionFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionFailedException"/> class.
    /// </summary>
    /// <param name="reason">Which of the known reasons this was.</param>
    /// <param name="message">What to tell the operator.</param>
    public TranscriptionFailedException(TranscriptionFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionFailedException"/> class.
    /// </summary>
    /// <param name="reason">Which of the known reasons this was.</param>
    /// <param name="message">What to tell the operator.</param>
    /// <param name="innerException">What went wrong underneath.</param>
    public TranscriptionFailedException(TranscriptionFailureReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets which of the known reasons this was.
    /// </summary>
    public TranscriptionFailureReason Reason { get; }
}
