using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// The records, and the bound on how many of them there may be.
/// </summary>
/// <remarks>
/// A library of a hundred thousand items must not turn into a hundred thousand
/// records. Records only exist for items that failed, so the bound is not really
/// about library size: a library where more than <see cref="DefaultCapacity"/>
/// items have failed has a problem the operator needs to be shown, not a
/// bookkeeping problem, and growing the ledger past that point would hide it while
/// costing memory on every start.
///
/// Not thread-safe, and not meant to be. One queue with one writer is what #19
/// builds; a ledger that took a lock would invite the assumption that concurrent
/// writers are supported when the rest of this has not been designed for them.
/// </remarks>
public sealed class AttemptLedger
{
    /// <summary>
    /// How many records are kept.
    /// </summary>
    /// <remarks>
    /// Ten thousand. A record is an identifier, a count, a timestamp, a reason and
    /// a flag, so the whole ledger at capacity is on the order of half a megabyte
    /// held by a plugin inside a media server, which is affordable. The number is
    /// here rather than in a document because the reason for it is a property of
    /// the record, and a document restating it would drift.
    /// </remarks>
    public const int DefaultCapacity = 10_000;

    private readonly Dictionary<Guid, ItemAttempt> _records = new();
    private readonly int _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptLedger"/> class.
    /// </summary>
    /// <param name="capacity">How many records to keep.</param>
    public AttemptLedger(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _capacity = capacity;
    }

    /// <summary>
    /// Gets how many records are held.
    /// </summary>
    public int Count => _records.Count;

    /// <summary>
    /// Gets the identifiers of every item currently quarantined.
    /// </summary>
    public IReadOnlySet<Guid> Quarantined =>
        _records.Values.Where(r => r.IsQuarantined).Select(r => r.ItemId).ToHashSet();

    /// <summary>
    /// Reads what is known about an item.
    /// </summary>
    /// <param name="itemId">The item.</param>
    /// <returns>The record, or null if there is none.</returns>
    public ItemAttempt? Find(Guid itemId) => _records.TryGetValue(itemId, out var found) ? found : null;

    /// <summary>
    /// Gets every record, in no particular order.
    /// </summary>
    /// <returns>The records.</returns>
    public IReadOnlyList<ItemAttempt> All() => _records.Values.ToList();

    /// <summary>
    /// Records one failure against an item.
    /// </summary>
    /// <param name="itemId">The item that failed.</param>
    /// <param name="reason">How the attempt ended.</param>
    /// <param name="endedAt">When it ended.</param>
    /// <param name="failureLimit">How many counted failures are allowed before quarantine.</param>
    /// <returns>The record as it now stands.</returns>
    public ItemAttempt RecordFailure(
        Guid itemId,
        TranscriptionFailureReason reason,
        DateTimeOffset endedAt,
        int failureLimit = RetryPolicy.DefaultFailureLimit)
    {
        var updated = RetryPolicy.Record(Find(itemId), itemId, reason, endedAt, failureLimit);

        _records[itemId] = updated;

        MakeRoom();

        return updated;
    }

    /// <summary>
    /// Forgets an item, which is what clearing a quarantine means.
    /// </summary>
    /// <param name="itemId">The item to forget.</param>
    /// <returns>Whether there was anything to forget.</returns>
    /// <remarks>
    /// Forgetting rather than clearing a flag. An operator who cleared a
    /// quarantine wants the item treated as new, and a record that kept its
    /// failure count would quarantine it again on the next failure instead of
    /// giving it the same run of attempts any other item gets.
    /// </remarks>
    public bool Clear(Guid itemId) => _records.Remove(itemId);

    /// <summary>
    /// Drops records once there are more than the bound allows.
    /// </summary>
    /// <remarks>
    /// The least recently touched goes first, and a record that is not
    /// quarantined goes before one that is: dropping a quarantined record makes
    /// that item a candidate again, so a full ledger would otherwise start
    /// retrying the very items it was keeping track of. That is still what happens
    /// when every record is quarantined and one has to go, and it is the residual
    /// cost of bounding this at all rather than something the order avoids.
    /// </remarks>
    private void MakeRoom()
    {
        while (_records.Count > _capacity)
        {
            var victim = _records.Values
                .OrderBy(r => r.IsQuarantined)
                .ThenBy(r => r.LastAttempt)
                .ThenBy(r => r.ItemId)
                .First();

            _records.Remove(victim.ItemId);
        }
    }
}
