using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Calibration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the refining rule, and the failure they are written
/// against is a factor the last item owns. An estimate built on a number that
/// swings with whatever was transcribed most recently is about that item rather
/// than about the library, which is the way an estimate becomes a lie without
/// anybody editing it.
/// </summary>
/// <remarks>
/// The fixture is twelve completed items whose own ratios run from 2.5 to 6.0,
/// with durations from three minutes to two hours, so the two things being
/// claimed are separable: that the answer settles inside a band far narrower
/// than the scatter, and that no position in the sequence decides it.
/// </remarks>
public sealed class ThroughputFactorTests
{
    private static readonly CompletedItem[] Fixture =
    [
        new(180, 5.0),
        new(5520, 3.6),
        new(1320, 4.4),
        new(2880, 2.5),
        new(420, 6.0),
        new(7080, 4.1),
        new(1500, 3.2),
        new(2640, 4.7),
        new(720, 5.5),
        new(5160, 3.9),
        new(1860, 4.2),
        new(3300, 3.4),
    ];

    [Fact]
    public void One_item_is_its_own_ratio()
    {
        var factor = ThroughputFactor.Measured(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(40));

        Assert.Equal(4.0, factor.WorkPerSecondOfAudio, 10);
        Assert.Equal(1, factor.Items);
        Assert.Equal(TimeSpan.FromMinutes(10), factor.AudioMeasured);
    }

    [Fact]
    public void The_items_are_scattered_far_wider_than_the_band_the_factor_lands_in()
    {
        var ratios = Fixture.Select(item => item.Ratio).ToArray();

        Assert.Equal(2.5, ratios.Min(), 10);
        Assert.Equal(6.0, ratios.Max(), 10);

        var factor = Fold(Fixture);

        Assert.InRange(factor.WorkPerSecondOfAudio, 3.6, 4.3);
        Assert.Equal(Fixture.Length, factor.Items);
    }

    [Fact]
    public void Folding_the_fixture_in_another_order_gives_the_same_factor()
    {
        var forwards = Fold(Fixture).WorkPerSecondOfAudio;
        var backwards = Fold(Fixture.Reverse().ToArray()).WorkPerSecondOfAudio;
        var shortestLast = Fold(Fixture.OrderByDescending(item => item.Audio).ToArray()).WorkPerSecondOfAudio;
        var shortestFirst = Fold(Fixture.OrderBy(item => item.Audio).ToArray()).WorkPerSecondOfAudio;

        Assert.Equal(forwards, backwards, 10);
        Assert.Equal(forwards, shortestLast, 10);
        Assert.Equal(forwards, shortestFirst, 10);
    }

    [Fact]
    public void A_late_outlier_does_not_take_the_factor_with_it()
    {
        var before = Fold(Fixture);
        var after = before.And(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30 * 20));

        Assert.Equal(20.0, 30.0 * 20 / 30.0, 10);
        Assert.True(
            after.WorkPerSecondOfAudio < 5.0,
            $"one item at twenty took the factor to {after.WorkPerSecondOfAudio}");
        Assert.True(after.WorkPerSecondOfAudio > before.WorkPerSecondOfAudio);
    }

    [Fact]
    public void Each_further_item_of_the_same_length_moves_it_less_than_the_one_before()
    {
        var factor = ThroughputFactor.Measured(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(40));
        var moves = new List<double>();

        for (var fold = 0; fold < 8; fold++)
        {
            var before = factor.WorkPerSecondOfAudio;
            factor = factor.And(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(80));
            moves.Add(factor.WorkPerSecondOfAudio - before);
        }

        Assert.All(moves, move => Assert.True(move > 0));

        for (var step = 1; step < moves.Count; step++)
        {
            Assert.True(
                moves[step] < moves[step - 1],
                $"item {step + 1} moved the factor by {moves[step]} and item {step} moved it by {moves[step - 1]}");
        }
    }

    [Fact]
    public void Weight_comes_from_the_audio_and_not_from_the_number_of_items()
    {
        var factor = ThroughputFactor
            .Measured(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10))
            .And(TimeSpan.FromMinutes(99), TimeSpan.FromMinutes(198));

        Assert.Equal(2.08, factor.WorkPerSecondOfAudio, 10);
        Assert.NotEqual(6.0, factor.WorkPerSecondOfAudio, 2);
    }

    /// <remarks>
    /// The sweep is every thirty seconds up to four hours, and that step is the
    /// bound on what this leg says. A correction that dipped and recovered
    /// entirely between two samples would pass it. Thirty seconds was chosen by
    /// watching it: at seven minutes it missed a bucketed correction that pays a
    /// penalty below ten minutes, which is the shape a reader would write first.
    /// </remarks>
    [Fact]
    public void A_longer_item_never_costs_less_than_a_shorter_one()
    {
        var factor = Fold(Fixture).And(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(600));
        var previous = TimeSpan.MinValue;

        for (var seconds = 0; seconds <= 4 * 60 * 60; seconds += 30)
        {
            var expected = factor.Expect(TimeSpan.FromSeconds(seconds));

            Assert.True(expected >= previous, $"{seconds} seconds of media was expected to cost less than the item before it");
            previous = expected;
        }
    }

    [Fact]
    public void Media_of_no_length_costs_nothing()
    {
        Assert.Equal(TimeSpan.Zero, Fold(Fixture).Expect(TimeSpan.Zero));
    }

    [Fact]
    public void Media_long_enough_to_overflow_answers_the_longest_time_there_is()
    {
        var factor = ThroughputFactor.Measured(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(40));

        Assert.Equal(TimeSpan.MaxValue, factor.Expect(TimeSpan.MaxValue));
    }

    [Fact]
    public void Media_of_a_negative_length_is_not_something_this_answers_for()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Fold(Fixture).Expect(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void An_item_with_no_audio_is_not_a_measurement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ThroughputFactor.Measured(TimeSpan.Zero, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Fold(Fixture).And(TimeSpan.Zero, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Time_that_ran_backwards_is_not_a_measurement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ThroughputFactor.Measured(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Fold(Fixture).And(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void The_factor_says_how_much_audio_it_is_made_of()
    {
        var factor = Fold(Fixture);

        Assert.Equal(TimeSpan.FromSeconds(Fixture.Sum(item => item.Audio.TotalSeconds)), factor.AudioMeasured);
        Assert.Equal(Fixture.Length, factor.Items);
    }

    private static ThroughputFactor Fold(IReadOnlyList<CompletedItem> items)
    {
        var factor = ThroughputFactor.Measured(items[0].Audio, items[0].Work);

        for (var next = 1; next < items.Count; next++)
        {
            factor = factor.And(items[next].Audio, items[next].Work);
        }

        return factor;
    }

    private sealed class CompletedItem
    {
        public CompletedItem(int audioSeconds, double ratio)
        {
            Audio = TimeSpan.FromSeconds(audioSeconds);
            Work = TimeSpan.FromSeconds(audioSeconds * ratio);
            Ratio = ratio;
        }

        public TimeSpan Audio { get; }

        public TimeSpan Work { get; }

        public double Ratio { get; }
    }
}
