using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is how many transcriptions a server is doing at once.
/// The number has to be the one an operator set, in a run that goes well, in a
/// run where items fail, and in a run somebody stopped partway, because those
/// last two are where a queue quietly starts everything it has left.
/// </summary>
public sealed class BoundedRunTests
{
    private static readonly IReadOnlyList<int> _eightItems = Enumerable.Range(1, 8).ToArray();

    [Fact]
    public async Task Every_item_is_taken_exactly_once()
    {
        var watcher = new ConcurrencyWatcher();

        var outcome = await BoundedRun.RunAsync(_eightItems, 3, watcher.RunAsync, CancellationToken.None);

        Assert.Equal(8, outcome.Completed);
        Assert.Empty(outcome.Failures);
        Assert.Equal(0, outcome.NeverStarted);
        Assert.False(outcome.WasCancelled);
        Assert.Equal(_eightItems, watcher.Taken.OrderBy(item => item).ToArray());
    }

    [Fact]
    public async Task No_more_workers_are_started_than_the_cap()
    {
        var outcome = await BoundedRun.RunAsync(_eightItems, 3, new ConcurrencyWatcher().RunAsync, CancellationToken.None);

        Assert.Equal(3, outcome.WorkersStarted);
    }

    [Fact]
    public async Task A_run_with_fewer_items_than_the_cap_starts_a_worker_per_item()
    {
        var outcome = await BoundedRun.RunAsync(new[] { 1, 2 }, 5, new ConcurrencyWatcher().RunAsync, CancellationToken.None);

        Assert.Equal(2, outcome.WorkersStarted);
        Assert.Equal(2, outcome.Completed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task The_number_in_flight_never_exceeds_the_cap(int cap)
    {
        var watcher = new ConcurrencyWatcher();

        var outcome = await BoundedRun.RunAsync(_eightItems, cap, watcher.RunAsync, CancellationToken.None);

        Assert.True(
            watcher.MaximumInFlight <= cap,
            $"{watcher.MaximumInFlight} items were being transcribed at once under a cap of {cap}");
        Assert.Equal(cap, outcome.WorkersStarted);
    }

    [Fact]
    public async Task A_run_where_items_fail_stays_under_the_cap_and_finishes_the_rest()
    {
        var watcher = new ConcurrencyWatcher(failsOn: item => item % 2 == 0);

        var outcome = await BoundedRun.RunAsync(_eightItems, 3, watcher.RunAsync, CancellationToken.None);

        Assert.True(
            watcher.MaximumInFlight <= 3,
            $"{watcher.MaximumInFlight} items were being transcribed at once under a cap of 3");
        Assert.Equal(4, outcome.Completed);
        Assert.Equal(4, outcome.Failures.Count);
        Assert.Equal(new[] { 2, 4, 6, 8 }, outcome.Failures.Select(failure => failure.Item).OrderBy(item => item).ToArray());
        Assert.All(outcome.Failures, failure => Assert.IsType<InvalidOperationException>(failure.Failure));
        Assert.Equal(0, outcome.NeverStarted);
    }

    [Fact]
    public async Task A_run_stopped_partway_stays_under_the_cap_and_says_what_it_never_started()
    {
        using var stop = new CancellationTokenSource();
        var watcher = new ConcurrencyWatcher(onEntry: item =>
        {
            if (item == 3)
            {
                stop.Cancel();
            }
        });

        var outcome = await BoundedRun.RunAsync(_eightItems, 2, watcher.RunAsync, stop.Token);

        Assert.True(
            watcher.MaximumInFlight <= 2,
            $"{watcher.MaximumInFlight} items were being transcribed at once under a cap of 2");
        Assert.True(outcome.WasCancelled);
        Assert.True(outcome.NeverStarted > 0, "a run that was stopped reported that it had started everything");
        Assert.Equal(8, outcome.Completed + outcome.Failures.Count + outcome.NeverStarted + AbandonedIn(outcome, watcher));
    }

    [Fact]
    public async Task A_run_that_is_stopped_before_it_begins_takes_nothing()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var watcher = new ConcurrencyWatcher();

        var outcome = await BoundedRun.RunAsync(_eightItems, 3, watcher.RunAsync, stop.Token);

        Assert.Empty(watcher.Taken);
        Assert.Equal(8, outcome.NeverStarted);
        Assert.True(outcome.WasCancelled);
    }

    [Fact]
    public async Task A_cap_below_one_is_not_a_run()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BoundedRun.RunAsync(_eightItems, 0, new ConcurrencyWatcher().RunAsync, CancellationToken.None));
    }

    [Fact]
    public async Task A_run_over_no_items_starts_no_workers()
    {
        var outcome = await BoundedRun.RunAsync(Array.Empty<int>(), 4, new ConcurrencyWatcher().RunAsync, CancellationToken.None);

        Assert.Equal(0, outcome.WorkersStarted);
        Assert.Equal(0, outcome.Completed);
        Assert.False(outcome.WasCancelled);
    }

    // Items a worker had in flight when the stop arrived. The outcome counts them
    // nowhere on purpose, so a test that wants the sum to balance has to name
    // them, which is the point of the hole rather than a gap in it.
    private static int AbandonedIn(RunOutcome<int> outcome, ConcurrencyWatcher watcher) =>
        watcher.Taken.Count - outcome.Completed - outcome.Failures.Count;
}
