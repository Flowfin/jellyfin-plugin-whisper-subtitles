using System;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// What one attempt at one item produced: segments to write, or a reason there are
/// none.
/// </summary>
/// <remarks>
/// The two are held in one type with no third state, and the segments are
/// unreachable unless there are some. That is what makes "no file is written for
/// silence" a property of the shape rather than a rule somebody has to remember at
/// every call site: a caller holding a failed outcome has nothing to hand a writer.
///
/// An empty subtitle track is worse than no track. It looks exactly like the work
/// was done, so an operator scanning a library for items still to transcribe skips
/// it, and the viewer who selects it gets a track with nothing in it.
/// </remarks>
public sealed class TranscriptionOutcome
{
    private readonly TranscriptionResult? _result;

    private TranscriptionOutcome(TranscriptionResult? result, TranscriptionFailureReason? reason)
    {
        _result = result;
        Reason = reason;
    }

    /// <summary>
    /// Gets why there are no segments, or null where there are.
    /// </summary>
    public TranscriptionFailureReason? Reason { get; }

    /// <summary>
    /// Gets a value indicating whether this attempt produced something to write.
    /// </summary>
    public bool ProducesAFile => _result is not null;

    /// <summary>
    /// An attempt that produced segments.
    /// </summary>
    /// <param name="result">What the backend produced.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The result carries no segments.</exception>
    /// <remarks>
    /// A result with no segments is refused here rather than accepted and written
    /// as an empty file. Whatever produced it has a reason, and
    /// <see cref="TranscriptionFailureReason.NoSegments"/> is the one to use where
    /// nothing better is known.
    /// </remarks>
    public static TranscriptionOutcome Writes(TranscriptionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Segments is null || result.Segments.Count == 0)
        {
            throw new ArgumentException(
                "An outcome that writes a file has segments in it, and an empty subtitle track is worse than none.",
                nameof(result));
        }

        return new TranscriptionOutcome(result, reason: null);
    }

    /// <summary>
    /// An attempt that ended without segments.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <returns>The outcome.</returns>
    public static TranscriptionOutcome WritesNothing(TranscriptionFailureReason reason) =>
        new(result: null, reason);

    /// <summary>
    /// The segments to write.
    /// </summary>
    /// <returns>What the backend produced.</returns>
    /// <exception cref="InvalidOperationException">This attempt produced nothing.</exception>
    /// <remarks>
    /// A method that refuses rather than a property that answers null, so a caller
    /// that skipped <see cref="ProducesAFile"/> fails loudly at the moment it asks
    /// instead of writing a file out of an empty list.
    /// </remarks>
    public TranscriptionResult Result() =>
        _result ?? throw new InvalidOperationException(
            "This attempt produced no segments: " + FailureReasonMessages.For(Reason!.Value));
}
