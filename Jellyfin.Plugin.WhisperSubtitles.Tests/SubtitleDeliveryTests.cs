using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is what the server is asked to do after a file lands, and
/// how often. The interesting failures are on the second half: a run over a large
/// library that asks the server to reread an item every time it finishes one is a
/// plugin making work for the rest of the server, and a refresh that failed
/// turning a finished subtitle into a failed item is a plugin lying about its own
/// work.
/// </summary>
public class SubtitleDeliveryTests
{
    private static readonly Guid _item = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task The_server_is_asked_about_exactly_the_item_that_was_written()
    {
        var refresher = new RecordingRefresher();
        using var delivery = new SubtitleDelivery(refresher);

        var delivered = await delivery.ReportAsync(_item, "A Film.en.srt", CancellationToken.None);

        Assert.True(delivered.WasRefreshRequested);
        Assert.Null(delivered.RefreshProblem);
        Assert.Equal("A Film.en.srt", delivered.Path);
        Assert.Equal(new[] { _item }, refresher.Refreshed);
    }

    [Fact]
    public async Task One_item_is_asked_about_once_however_many_files_it_gets()
    {
        // A second language for one item is a second write and not a second reason
        // to make the server reread it.
        var refresher = new RecordingRefresher();
        using var delivery = new SubtitleDelivery(refresher);

        var first = await delivery.ReportAsync(_item, "A Film.en.srt", CancellationToken.None);
        var second = await delivery.ReportAsync(_item, "A Film.de.srt", CancellationToken.None);

        Assert.True(first.WasRefreshRequested);
        Assert.False(second.WasRefreshRequested);
        Assert.Equal("A Film.de.srt", second.Path);
        Assert.Equal(new[] { _item }, refresher.Refreshed);
        Assert.Equal(1, delivery.ItemsAskedAbout);
    }

    [Fact]
    public async Task Over_a_run_of_many_items_no_two_refreshes_are_ever_in_flight_at_once()
    {
        // The rule this asserts is the one written down beside the code: one request
        // at a time, however many items the run is working on. The run is the real
        // one, at a cap an operator could set, so what is counted is what a run would
        // actually produce rather than what a loop in a test would.
        //
        // The counting is in the refresher rather than in ConcurrencyWatcher, which
        // is the work in the cap's own tests and not a probe something else can be
        // measured through.
        var refresher = new WatchedRefresher();

        using var delivery = new SubtitleDelivery(refresher);

        var items = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();

        var outcome = await BoundedRun.RunAsync(
            items,
            workers: 8,
            work: (id, token) => delivery.ReportAsync(id, "A Film.en.srt", token),
            CancellationToken.None);

        Assert.Equal(200, outcome.Completed);
        Assert.Equal(1, refresher.HighWaterMark);
        Assert.Equal(200, delivery.ItemsAskedAbout);
        Assert.Equal(200, refresher.Refreshed.Length);
        Assert.Equal(200, refresher.Refreshed.Distinct().Count());
    }

    [Fact]
    public async Task A_refresh_that_fails_leaves_the_written_subtitle_written()
    {
        // The file is real, because the property is that a failed refresh does not
        // undo or invalidate it. What must not happen is an exception reaching a
        // caller that would then record the item as failed.
        var directory = Path.Combine(Path.GetTempPath(), "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var subtitle = Path.Combine(directory, "A Film.en.srt");
            await File.WriteAllBytesAsync(subtitle, Encoding.UTF8.GetBytes("1\r\n"));

            using var delivery = new SubtitleDelivery(new RefusingRefresher());

            var delivered = await delivery.ReportAsync(_item, subtitle, CancellationToken.None);

            Assert.False(delivered.WasRefreshRequested);
            Assert.Contains("The subtitle is written.", delivered.RefreshProblem!, StringComparison.Ordinal);
            Assert.Contains("The server said no.", delivered.RefreshProblem!, StringComparison.Ordinal);
            Assert.Equal(subtitle, delivered.Path);
            Assert.True(File.Exists(subtitle));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Every_way_a_server_can_refuse_ends_the_same_way()
    {
        // Whatever an implementation of the seam throws. A caller deciding whether an
        // item is done cannot be asked to know which exception types a metadata
        // refresh can produce, and the list would be wrong the first time a server
        // version changed.
        var thrown = new Exception[]
        {
            new InvalidOperationException("The item is not in the library."),
            new IOException("The metadata database is locked."),
            new UnauthorizedAccessException("Access to the path is denied."),
            new NotSupportedException("This item type cannot be refreshed."),
            new TimeoutException("The server did not answer."),
        };

        foreach (var refusal in thrown)
        {
            using var delivery = new SubtitleDelivery(new RefusingRefresher(refusal));

            var delivered = await delivery.ReportAsync(_item, "A Film.en.srt", CancellationToken.None);

            Assert.False(delivered.WasRefreshRequested);
            Assert.Contains(refusal.Message, delivered.RefreshProblem!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_operator_stopping_the_run_before_the_request_is_not_swallowed()
    {
        // The one exception that is not a refresh failure. A cancelled run has to
        // look cancelled to whatever is counting outcomes.
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        using var delivery = new SubtitleDelivery(new RecordingRefresher());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delivery.ReportAsync(_item, "A Film.en.srt", stopped.Token));
    }

    [Fact]
    public async Task An_operator_stopping_the_run_during_the_request_is_not_swallowed_either()
    {
        // The case the test above does not reach, and the reason it exists: stopping
        // before the request is refused by the wait, so it never enters the part that
        // catches. A refresh that observes the token and stops is the arm that
        // decides whether a cancelled run is reported as a failed refresh, and
        // without this test that arm could be deleted with nothing going red.
        using var stopping = new CancellationTokenSource();

        using var delivery = new SubtitleDelivery(new StoppingRefresher(stopping));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delivery.ReportAsync(_item, "A Film.en.srt", stopping.Token));
    }

    private sealed class RecordingRefresher : IItemRefresher
    {
        private readonly ConcurrentQueue<Guid> _refreshed = new();

        public Guid[] Refreshed => _refreshed.ToArray();

        public Task RefreshAsync(Guid itemId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _refreshed.Enqueue(itemId);

            return Task.CompletedTask;
        }
    }

    private sealed class WatchedRefresher : IItemRefresher
    {
        private readonly ConcurrentQueue<Guid> _refreshed = new();
        private int _inFlight;
        private int _highWaterMark;

        public Guid[] Refreshed => _refreshed.ToArray();

        /// <summary>
        /// Gets the most requests that were inside this method at one instant.
        /// </summary>
        public int HighWaterMark => Volatile.Read(ref _highWaterMark);

        public async Task RefreshAsync(Guid itemId, CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref _inFlight);
            Remember(now);

            try
            {
                _refreshed.Enqueue(itemId);

                // Yields rather than a delay. They give every other worker a chance to
                // be inside this method at the same moment, which is the thing being
                // counted, and they cost no wall clock. A method that never awaited
                // would finish before the next worker was scheduled, and a rule that
                // was never applied would look exactly like one that was.
                await Task.Yield();
                await Task.Yield();
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private void Remember(int seen)
        {
            var highest = Volatile.Read(ref _highWaterMark);

            while (seen > highest)
            {
                var was = Interlocked.CompareExchange(ref _highWaterMark, seen, highest);

                if (was == highest)
                {
                    return;
                }

                highest = was;
            }
        }
    }

    /// <summary>
    /// A server that notices the run was stopped and stops, which is what an
    /// implementation honouring its token does.
    /// </summary>
    private sealed class StoppingRefresher : IItemRefresher
    {
        private readonly CancellationTokenSource _stopping;

        public StoppingRefresher(CancellationTokenSource stopping)
        {
            _stopping = stopping;
        }

        public async Task RefreshAsync(Guid itemId, CancellationToken cancellationToken)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class RefusingRefresher : IItemRefresher
    {
        private readonly Exception _refusal;

        public RefusingRefresher()
            : this(new InvalidOperationException("The server said no."))
        {
        }

        public RefusingRefresher(Exception refusal)
        {
            _refusal = refusal;
        }

        public Task RefreshAsync(Guid itemId, CancellationToken cancellationToken) => throw _refusal;
    }
}
