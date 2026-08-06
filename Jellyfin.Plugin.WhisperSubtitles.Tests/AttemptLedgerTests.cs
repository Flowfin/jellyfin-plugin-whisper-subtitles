using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Without memory between runs a scheduled task retries the same broken item on
/// every trigger forever. These assert the memory, its rule, and the bound on how
/// much of it there may be.
/// </summary>
public class AttemptLedgerTests
{
    private static readonly DateTimeOffset _when = new(2026, 3, 1, 22, 0, 0, TimeSpan.Zero);

    private static readonly Guid _library = new("33333333-3333-3333-3333-333333333333");

    private static readonly TranscriptionFailureReason[] _retryable =
    {
        TranscriptionFailureReason.Cancelled,
        TranscriptionFailureReason.BackendNotReady,
        TranscriptionFailureReason.BackendUnreachable,
        TranscriptionFailureReason.BackendFailed
    };

    private static readonly TranscriptionFailureReason[] _permanent =
    {
        TranscriptionFailureReason.NoAudioStream,
        TranscriptionFailureReason.AudioUnreadable,
        TranscriptionFailureReason.OutputUnparseable
    };

    [Fact]
    public void Every_reason_is_classified_and_the_two_classes_are_the_whole_enum()
    {
        // A value added later that nobody classified would otherwise default to
        // permanent and quarantine an item on its first failure, silently.
        var all = Enum.GetValues<TranscriptionFailureReason>();

        Assert.Equal(all.Length, _retryable.Length + _permanent.Length);
        Assert.All(_retryable, r => Assert.True(RetryPolicy.IsRetryable(r), r.ToString()));
        Assert.All(_permanent, r => Assert.False(RetryPolicy.IsRetryable(r), r.ToString()));
    }

    [Fact]
    public void A_permanent_reason_quarantines_on_the_first_failure()
    {
        foreach (var reason in _permanent)
        {
            var ledger = new AttemptLedger();

            var record = ledger.RecordFailure(Id(reason.ToString()), reason, _when);

            Assert.True(record.IsQuarantined, reason.ToString());
            Assert.Equal(reason, record.LastReason);
        }
    }

    [Fact]
    public void A_retryable_reason_quarantines_only_at_the_limit()
    {
        var ledger = new AttemptLedger();
        var item = Id("flaky");

        for (var attempt = 1; attempt < RetryPolicy.DefaultFailureLimit; attempt++)
        {
            var interim = ledger.RecordFailure(item, TranscriptionFailureReason.BackendUnreachable, _when);

            Assert.False(interim.IsQuarantined);
            Assert.Equal(attempt, interim.Failures);
        }

        var final = ledger.RecordFailure(item, TranscriptionFailureReason.BackendUnreachable, _when);

        Assert.True(final.IsQuarantined);
        Assert.Equal(RetryPolicy.DefaultFailureLimit, final.Failures);
    }

    [Fact]
    public void A_run_the_operator_stopped_is_recorded_and_does_not_count()
    {
        // The near-miss. An operator who stops the nightly run would otherwise
        // quarantine whatever was in flight each time, and would find items
        // skipped that had never actually been tried.
        var ledger = new AttemptLedger();
        var item = Id("interrupted");

        for (var i = 0; i < RetryPolicy.DefaultFailureLimit * 3; i++)
        {
            ledger.RecordFailure(item, TranscriptionFailureReason.Cancelled, _when);
        }

        var record = ledger.Find(item);

        Assert.NotNull(record);
        Assert.False(record!.IsQuarantined);
        Assert.Equal(0, record.Failures);
        Assert.Equal(TranscriptionFailureReason.Cancelled, record.LastReason);
    }

    [Fact]
    public void Quarantine_once_set_is_not_lifted_by_a_later_cancellation()
    {
        var ledger = new AttemptLedger();
        var item = Id("broken");

        ledger.RecordFailure(item, TranscriptionFailureReason.NoAudioStream, _when);
        var after = ledger.RecordFailure(item, TranscriptionFailureReason.Cancelled, _when);

        Assert.True(after.IsQuarantined);
    }

    [Fact]
    public void A_quarantined_item_is_not_selected_on_the_next_run()
    {
        var ledger = new AttemptLedger();
        var broken = Id("broken");
        var fine = Id("fine");

        ledger.RecordFailure(broken, TranscriptionFailureReason.NoAudioStream, _when);

        var library = new[] { Item(broken, "broken"), Item(fine, "fine") };

        var selected = ItemSelection.Select(library, Options(ledger.Quarantined));

        Assert.Equal(new[] { fine }, selected.Candidates.Select(c => c.Id));
    }

    [Fact]
    public void Clearing_the_quarantine_makes_the_item_selectable_again()
    {
        var ledger = new AttemptLedger();
        var broken = Id("broken");

        ledger.RecordFailure(broken, TranscriptionFailureReason.AudioUnreadable, _when);
        Assert.True(ledger.Clear(broken));

        var selected = ItemSelection.Select(new[] { Item(broken, "broken") }, Options(ledger.Quarantined));

        Assert.Equal(new[] { broken }, selected.Candidates.Select(c => c.Id));
    }

    [Fact]
    public void Clearing_forgets_the_failure_count_as_well_as_the_flag()
    {
        // An operator who cleared a quarantine wants the item treated as new. A
        // record that kept its count would quarantine it again on the next single
        // failure instead of giving it the run of attempts every other item gets.
        var ledger = new AttemptLedger();
        var item = Id("cleared");

        for (var i = 0; i < RetryPolicy.DefaultFailureLimit; i++)
        {
            ledger.RecordFailure(item, TranscriptionFailureReason.BackendFailed, _when);
        }

        ledger.Clear(item);

        var afterOneMoreFailure = ledger.RecordFailure(item, TranscriptionFailureReason.BackendFailed, _when);

        Assert.False(afterOneMoreFailure.IsQuarantined);
        Assert.Equal(1, afterOneMoreFailure.Failures);
    }

    [Fact]
    public void The_ledger_holds_no_more_records_than_its_bound()
    {
        const int Capacity = 50;

        var ledger = new AttemptLedger(Capacity);

        for (var i = 0; i < Capacity * 4; i++)
        {
            ledger.RecordFailure(
                Id("item " + i.ToString(CultureInfo.InvariantCulture)),
                TranscriptionFailureReason.BackendFailed,
                _when.AddMinutes(i));
        }

        Assert.Equal(Capacity, ledger.Count);
    }

    [Fact]
    public void A_full_ledger_drops_what_is_not_quarantined_first()
    {
        // Dropping a quarantined record makes that item a candidate again, so a
        // full ledger would otherwise start retrying the very items it was keeping
        // track of.
        const int Capacity = 10;

        var ledger = new AttemptLedger(Capacity);
        var quarantined = Id("quarantined");

        ledger.RecordFailure(quarantined, TranscriptionFailureReason.NoAudioStream, _when);

        for (var i = 0; i < Capacity * 3; i++)
        {
            ledger.RecordFailure(
                Id("filler " + i.ToString(CultureInfo.InvariantCulture)),
                TranscriptionFailureReason.BackendFailed,
                _when.AddMinutes(i + 1));
        }

        Assert.Equal(Capacity, ledger.Count);
        Assert.NotNull(ledger.Find(quarantined));
        Assert.Contains(quarantined, ledger.Quarantined);
    }

    [Fact]
    public void A_full_ledger_of_quarantined_records_still_holds_its_bound()
    {
        // The residual cost of bounding this at all, asserted rather than left to
        // be discovered: when everything is quarantined, something quarantined is
        // dropped and that item becomes a candidate again.
        const int Capacity = 10;

        var ledger = new AttemptLedger(Capacity);

        for (var i = 0; i < Capacity * 2; i++)
        {
            ledger.RecordFailure(
                Id("permanent " + i.ToString(CultureInfo.InvariantCulture)),
                TranscriptionFailureReason.NoAudioStream,
                _when.AddMinutes(i));
        }

        Assert.Equal(Capacity, ledger.Count);
        Assert.Equal(Capacity, ledger.Quarantined.Count);
        Assert.Null(ledger.Find(Id("permanent 0")));
    }

    [Fact]
    public void A_capacity_below_one_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptLedger(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryPolicy.Record(null, Id("x"), TranscriptionFailureReason.BackendFailed, _when, failureLimit: 0));
    }

    private static SelectionOptions Options(IReadOnlySet<Guid> quarantined) =>
        new(
            new[] { _library },
            new[] { "Episode" },
            "en",
            null,
            null,
            quarantined);

    private static ItemDescription Item(Guid id, string name) =>
        new(id, name, _library, "Episode", TimeSpan.FromMinutes(30), true, Array.Empty<string>(), _when);

    private static Guid Id(string name)
    {
        var bytes = new byte[16];
        var source = System.Text.Encoding.UTF8.GetBytes(name);

        for (var i = 0; i < source.Length; i++)
        {
            bytes[i % 16] ^= source[i];
        }

        return new Guid(bytes);
    }
}
