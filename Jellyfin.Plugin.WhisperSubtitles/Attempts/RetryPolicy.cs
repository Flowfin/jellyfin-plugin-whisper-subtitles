using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// Decides what one failure does to an item's record.
/// </summary>
/// <remarks>
/// A function of the record and the reason and nothing else, so the rule can be
/// read and tested without a run, a clock or a store. Without a rule of this kind
/// a scheduled task retries the same broken item on every trigger forever, and the
/// operator pays for the same failure every night.
/// </remarks>
public static class RetryPolicy
{
    /// <summary>
    /// How many counted failures an item gets before it is quarantined.
    /// </summary>
    /// <remarks>
    /// Three rather than one, because the two most common failures here are a
    /// backend that was busy and an endpoint that was briefly unreachable, and
    /// quarantining an item for those would need an operator to come and clear it
    /// by hand for something that fixed itself.
    /// </remarks>
    public const int DefaultFailureLimit = 3;

    /// <summary>
    /// Whether trying this item again, unchanged, could plausibly end differently.
    /// </summary>
    /// <param name="reason">How the attempt ended.</param>
    /// <returns>Whether another attempt is worth the machine.</returns>
    /// <remarks>
    /// Every value of the vocabulary is named and there is no fallback arm, which
    /// is deliberate and is the only place this decision is refused rather than
    /// defaulted. A fallback of false would give every new failure mode the answer
    /// "never retry" silently, and an item quarantined by a mode nobody made a
    /// decision about needs an operator to clear it by hand.
    /// </remarks>
    // CS8524 is the compiler pointing out that a value cast in from outside the
    // vocabulary is not handled, and it is not handled on purpose: such a value
    // throws here rather than taking the answer of whichever arm a fallback would
    // have been. CS8509, which fires for a NAMED value with no arm, stays on, and
    // that is the refusal this shape exists for - a mode added to the vocabulary
    // without a decision here fails the build.
#pragma warning disable CS8524
    public static bool IsRetryable(TranscriptionFailureReason reason) => reason switch
    {
        TranscriptionFailureReason.Cancelled => true,
        TranscriptionFailureReason.BackendNotReady => true,
        TranscriptionFailureReason.BackendUnreachable => true,
        TranscriptionFailureReason.BackendFailed => true,

        // Nothing about tomorrow changes any of these. The item has no audio, or
        // its audio cannot be decoded, or the backend produced something this
        // plugin cannot read. Retrying spends the machine on a certainty.
        TranscriptionFailureReason.NoAudioStream => false,
        TranscriptionFailureReason.AudioUnreadable => false,
        TranscriptionFailureReason.OutputUnparseable => false,

        // Facts about the file rather than about the run. Nothing about tomorrow
        // makes a silent item speak or a concert recording hold narration, and
        // retrying either spends the machine on a certainty.
        TranscriptionFailureReason.AudioIsSilent => false,
        TranscriptionFailureReason.AudioHasNoSpeech => false,
        TranscriptionFailureReason.AudioHasSeveralLanguages => false,

        // Both of these move when a setting moves, and neither moves on its own.
        // Retrying unchanged is what this asks about, so both are false: a floor
        // an operator lowers and a backend an operator repoints are changes, and a
        // changed configuration is what clears a quarantine.
        TranscriptionFailureReason.DetectionBelowTheFloor => false,
        TranscriptionFailureReason.NoSegments => false,

        // The one that looks retryable and is not. A backend transcribing the
        // wrong file does it again, and the same wrong file every night is the
        // most expensive way to learn nothing.
        TranscriptionFailureReason.TimingsDoNotFitTheItem => false
    };
#pragma warning restore CS8524

    /// <summary>
    /// Whether this failure counts towards quarantine at all.
    /// </summary>
    /// <param name="reason">How the attempt ended.</param>
    /// <returns>Whether the failure count moves.</returns>
    /// <remarks>
    /// Cancellation is recorded and does not count. It says nothing about the
    /// item: the operator stopped the run, or the server's maximum runtime did.
    /// An operator who stops the nightly run three times would otherwise quarantine
    /// whatever was in flight each time, and would find items skipped that had
    /// never actually been tried.
    /// </remarks>
    public static bool CountsTowardsQuarantine(TranscriptionFailureReason reason) =>
        reason != TranscriptionFailureReason.Cancelled;

    /// <summary>
    /// Applies one failure to what is known about an item.
    /// </summary>
    /// <param name="previous">What was known before, or null if this is the first failure.</param>
    /// <param name="itemId">The item that failed.</param>
    /// <param name="reason">How the attempt ended.</param>
    /// <param name="endedAt">When it ended.</param>
    /// <param name="failureLimit">How many counted failures are allowed before quarantine.</param>
    /// <returns>The record to keep.</returns>
    public static ItemAttempt Record(
        ItemAttempt? previous,
        Guid itemId,
        TranscriptionFailureReason reason,
        DateTimeOffset endedAt,
        int failureLimit = DefaultFailureLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureLimit, 1);

        var failures = previous?.Failures ?? 0;

        if (CountsTowardsQuarantine(reason))
        {
            failures++;
        }

        var quarantined = (previous?.IsQuarantined ?? false)
            || !IsRetryable(reason)
            || (CountsTowardsQuarantine(reason) && failures >= failureLimit);

        return new ItemAttempt(itemId, failures, endedAt, reason, quarantined);
    }
}
