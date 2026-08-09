using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the number an operator watches while a run works.
/// The fixture is deliberately lopsided: three minutes, three hours and twenty
/// minutes, because every mistake this code can make is invisible on items of
/// equal length.
/// </summary>
public sealed class DurationWeightedProgressTests
{
    private static readonly IReadOnlyList<TimeSpan> _lopsided =
    [
        TimeSpan.FromMinutes(3),
        TimeSpan.FromHours(3),
        TimeSpan.FromMinutes(20),
    ];

    [Fact]
    public async Task A_run_that_finishes_everything_reports_each_item_by_its_own_length()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);

        await RunOverTheFixture(progress, 1, failing: _ => false);
        progress.RunFinished();

        var total = _lopsided.Sum(length => length.TotalMinutes);
        Assert.Equal(
            [
                Round(100 * 3 / total),
                Round(100 * (3 + 180) / total),
                100,
            ],
            seen.Values.Select(Round).ToArray());
    }

    [Fact]
    public async Task The_number_never_goes_backwards_with_several_workers_running()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);

        await RunOverTheFixture(progress, 3, failing: _ => false);
        progress.RunFinished();

        AssertNeverGoesBackwards(seen.Values);
        Assert.Equal(100, seen.Values[^1]);
    }

    /// <summary>
    /// The leg above runs three items behind a cap of three, which is a window
    /// narrow enough that twenty-five consecutive runs of it missed the defect
    /// #146 was about while a required check was going red on it in CI. This one
    /// widens the window instead of running the narrow one more often: enough
    /// items that the arithmetic between two workers takes long enough to
    /// interleave, and enough attempts that a miss is not what the green means.
    /// </summary>
    [Fact]
    public void The_sink_is_never_handed_a_number_below_the_one_before_it()
    {
        var lengths = Enumerable.Range(1, 64).Select(minutes => TimeSpan.FromMinutes(minutes)).ToArray();

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var seen = new Recorder();
            var progress = new DurationWeightedProgress(lengths, seen);

            Parallel.For(0, lengths.Length, item => progress.ItemFinished(item));
            progress.RunFinished();

            AssertNeverGoesBackwards(seen.Values);
            Assert.Equal(100, seen.Values[^1]);
        }
    }

    [Fact]
    public async Task An_item_that_failed_advances_the_number_by_its_whole_length()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);

        // The three hour film is the one that fails, so a number that stalled on
        // a failure would be visibly wrong rather than a rounding difference.
        await RunOverTheFixture(progress, 1, failing: item => item == 1);
        progress.RunFinished();

        var total = _lopsided.Sum(length => length.TotalMinutes);
        Assert.Contains(Round(100 * (3 + 180) / total), seen.Values.Select(Round));
        AssertNeverGoesBackwards(seen.Values);
        Assert.Equal(100, seen.Values[^1]);
    }

    [Fact]
    public async Task A_run_where_everything_failed_still_ends_at_a_hundred_exactly_once()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);

        await RunOverTheFixture(progress, 2, failing: _ => true);
        progress.RunFinished();

        AssertNeverGoesBackwards(seen.Values);
        // At the ceiling rather than exactly on it. The last report before the run
        // is told it is over is arithmetic over item lengths, so whether it lands on
        // a hundred or a fraction past it is a property of the division rather than
        // of this class. What the leg is about survives either: the number only ever
        // rises, so at most one report can be at the top, and the once is what is
        // asserted.
        Assert.Single(seen.Values, value => value >= 100);
        Assert.Equal(100, seen.Values[^1]);
    }

    [Fact]
    public void A_backend_that_reports_its_own_progress_moves_the_number_within_an_item()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);
        var total = _lopsided.Sum(length => length.TotalMinutes);

        progress.ItemProgressed(1, 0.5);

        Assert.Equal(Round(100 * 90 / total), Round(seen.Values.Single()));
    }

    [Fact]
    public void A_fraction_that_would_lower_the_number_is_absorbed()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);

        progress.ItemProgressed(1, 0.5);
        progress.ItemProgressed(1, 0.2);
        progress.ItemProgressed(1, 0.5);

        Assert.Single(seen.Values);
    }

    [Fact]
    public void A_selection_with_no_length_at_all_still_ends_at_a_hundred()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress([TimeSpan.Zero, TimeSpan.Zero], seen);

        progress.ItemFinished(0);
        progress.ItemFinished(1);
        progress.RunFinished();

        Assert.Equal([100], seen.Values);
    }

    [Fact]
    public void A_run_told_twice_that_it_is_over_says_so_once()
    {
        var seen = new Recorder();
        var progress = new DurationWeightedProgress(_lopsided, seen);

        progress.RunFinished();
        progress.RunFinished();

        Assert.Equal([100], seen.Values);
    }

    [Fact]
    public void An_item_outside_the_selection_is_not_a_number_this_can_move()
    {
        var progress = new DurationWeightedProgress(_lopsided, new Recorder());

        Assert.Throws<ArgumentOutOfRangeException>(() => progress.ItemFinished(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.ItemProgressed(-1, 0.5));
    }

    private static Task<RunOutcome<int>> RunOverTheFixture(DurationWeightedProgress progress, int cap, Func<int, bool> failing) =>
        BoundedRun.RunAsync(
            Enumerable.Range(0, 3).ToArray(),
            cap,
            async (item, token) =>
            {
                await Task.Yield();

                try
                {
                    if (failing(item))
                    {
                        throw new InvalidOperationException($"item {item} could not be read");
                    }
                }
                finally
                {
                    progress.ItemFinished(item);
                }
            },
            CancellationToken.None);

    private static void AssertNeverGoesBackwards(IReadOnlyList<double> values)
    {
        for (var step = 1; step < values.Count; step++)
        {
            Assert.True(
                values[step] >= values[step - 1],
                $"the number went from {values[step - 1]} to {values[step]}");
        }
    }

    private static double Round(double value) => Math.Round(value, 6);

    private sealed class Recorder : IProgress<double>
    {
        private readonly ConcurrentQueue<double> _values = new();

        public IReadOnlyList<double> Values => _values.ToArray();

        public void Report(double value) => _values.Enqueue(value);
    }
}
