using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// What the plugin remembers about one item between runs.
/// </summary>
public sealed class ItemAttempt
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemAttempt"/> class.
    /// </summary>
    /// <param name="itemId">The item this is about.</param>
    /// <param name="failures">How many failures have counted towards quarantine.</param>
    /// <param name="lastAttempt">When the last attempt ended.</param>
    /// <param name="lastReason">How the last attempt ended.</param>
    /// <param name="isQuarantined">Whether the item is being skipped.</param>
    public ItemAttempt(
        Guid itemId,
        int failures,
        DateTimeOffset lastAttempt,
        TranscriptionFailureReason lastReason,
        bool isQuarantined)
    {
        ItemId = itemId;
        Failures = failures;
        LastAttempt = lastAttempt;
        LastReason = lastReason;
        IsQuarantined = isQuarantined;
    }

    /// <summary>
    /// Gets the item this is about.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets how many failures have counted towards quarantine.
    /// </summary>
    /// <remarks>
    /// Counted failures rather than attempts. A run the operator stopped is
    /// recorded and does not count, so stopping the nightly run three times does
    /// not quarantine a library that was never actually tried.
    /// </remarks>
    public int Failures { get; }

    /// <summary>
    /// Gets when the last attempt ended.
    /// </summary>
    public DateTimeOffset LastAttempt { get; }

    /// <summary>
    /// Gets how the last attempt ended.
    /// </summary>
    public TranscriptionFailureReason LastReason { get; }

    /// <summary>
    /// Gets a value indicating whether the item is being skipped.
    /// </summary>
    public bool IsQuarantined { get; }
}
