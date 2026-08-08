using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// A subtitle that was produced and could not be written, carrying which of the
/// known reasons it was.
/// </summary>
/// <remarks>
/// The reason is on the exception rather than in its message, for the reason
/// <see cref="Backends.TranscriptionFailedException"/> gives: a caller has to
/// decide what to tell the operator and whether the item is worth trying again,
/// and reading a sentence to make those decisions is how two callers come to
/// disagree about one of them.
/// </remarks>
public sealed class SubtitleNotWrittenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleNotWrittenException"/> class.
    /// </summary>
    /// <param name="failure">Which of the known reasons this was.</param>
    /// <param name="message">What to tell the operator.</param>
    public SubtitleNotWrittenException(SubtitleWriteFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleNotWrittenException"/> class.
    /// </summary>
    /// <param name="failure">Which of the known reasons this was.</param>
    /// <param name="message">What to tell the operator.</param>
    /// <param name="innerException">What went wrong underneath.</param>
    public SubtitleNotWrittenException(SubtitleWriteFailure failure, string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>
    /// Gets which of the known reasons this was.
    /// </summary>
    public SubtitleWriteFailure Failure { get; }
}
