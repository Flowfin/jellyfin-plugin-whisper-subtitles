using System;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The per-item wall clock: a multiple of the media's own length, never under a
/// floor.
/// </summary>
/// <remarks>
/// Both numbers are decided rather than derived - four and ten minutes, taken on
/// #22 on 2026-09-04 - so what is worth holding is the shape rather than the
/// constants. Two legs are about the constants anyway, because a default that
/// changed silently would change what every operator who has not been to the page
/// gets.
///
/// The floor is the half a reader skips. Four times a short clip is a limit shorter
/// than the time a backend spends loading its model, so without the floor this
/// setting would abandon items for being short, which is the opposite of what it is
/// for.
/// </remarks>
public class ItemWallClockTests
{
    [Fact]
    public void An_item_gets_a_multiple_of_its_own_length()
    {
        var limit = ItemWallClock.For(TimeSpan.FromMinutes(90), multiple: 4, floorMinutes: 10);

        Assert.Equal(TimeSpan.FromMinutes(360), limit);
    }

    [Fact]
    public void A_short_item_gets_the_floor_rather_than_a_limit_it_could_not_meet()
    {
        // Four times ninety seconds is six minutes, and a backend loading a model off
        // a spinning disk can spend that before it says anything.
        var limit = ItemWallClock.For(TimeSpan.FromSeconds(90), multiple: 4, floorMinutes: 10);

        Assert.Equal(TimeSpan.FromMinutes(10), limit);
    }

    [Fact]
    public void The_floor_and_the_multiple_meeting_exactly_is_the_floor()
    {
        // The boundary, because an off-by-one here is a limit one tick short of what
        // the operator asked for and nothing else in the tree would notice.
        var limit = ItemWallClock.For(TimeSpan.FromMinutes(2.5), multiple: 4, floorMinutes: 10);

        Assert.Equal(TimeSpan.FromMinutes(10), limit);
    }

    [Fact]
    public void An_item_with_no_duration_still_gets_the_floor()
    {
        // An item whose metadata carries no duration reaches here as zero, and a
        // limit of zero would abandon it the moment it started.
        var limit = ItemWallClock.For(TimeSpan.Zero, multiple: 4, floorMinutes: 10);

        Assert.Equal(TimeSpan.FromMinutes(10), limit);
    }

    [Fact]
    public void The_operator_sets_both_numbers()
    {
        // Both, rather than one with the other fixed, which is what #22 decided. A
        // floor that could not move would be wrong on every machine whose model loads
        // slower than the one the number was chosen on.
        Assert.Equal(TimeSpan.FromMinutes(200), ItemWallClock.For(TimeSpan.FromMinutes(100), 2, 10));
        Assert.Equal(TimeSpan.FromMinutes(45), ItemWallClock.For(TimeSpan.FromMinutes(1), 4, 45));
    }

    [Fact]
    public void The_defaults_are_the_decided_ones()
    {
        Assert.Equal(4, ItemWallClock.DefaultMultiple);
        Assert.Equal(10, ItemWallClock.DefaultFloorMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_multiple_outside_what_an_operator_may_set_is_refused_by_name(int requested)
    {
        var choice = ItemWallClock.ChooseMultiple(requested);

        Assert.False(choice.IsAccepted);
        Assert.Contains(requested.ToString(System.Globalization.CultureInfo.InvariantCulture), choice.Refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(100)]
    public void A_multiple_inside_it_is_the_multiple_a_run_uses(int requested)
    {
        var choice = ItemWallClock.ChooseMultiple(requested);

        Assert.True(choice.IsAccepted);
        Assert.Equal(requested, choice.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public void A_floor_outside_what_an_operator_may_set_is_refused_by_name(int requested)
    {
        var choice = ItemWallClock.ChooseFloorMinutes(requested);

        Assert.False(choice.IsAccepted);
        Assert.Contains(requested.ToString(System.Globalization.CultureInfo.InvariantCulture), choice.Refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1440)]
    public void A_floor_inside_it_is_the_floor_a_run_uses(int requested)
    {
        var choice = ItemWallClock.ChooseFloorMinutes(requested);

        Assert.True(choice.IsAccepted);
        Assert.Equal(requested, choice.Value);
    }

    [Fact]
    public void The_sentinel_is_a_value_neither_number_would_ever_accept()
    {
        // Nobody choosing has to be unmistakable. A file written before these
        // settings existed carries zero in both fields, and a run that read that as a
        // number would abandon every item at once.
        Assert.False(ItemWallClock.ChooseMultiple(ItemWallClock.LetThePolicyDecide).IsAccepted);
        Assert.False(ItemWallClock.ChooseFloorMinutes(ItemWallClock.LetThePolicyDecide).IsAccepted);
    }

    [Fact]
    public void A_number_that_reached_the_limit_unresolved_is_refused_rather_than_used()
    {
        // The rule above is the only way in. A caller that skipped it and handed the
        // sentinel straight to the limit is a defect, and it fails here rather than
        // producing a limit of nothing.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ItemWallClock.For(TimeSpan.FromMinutes(10), ItemWallClock.LetThePolicyDecide, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ItemWallClock.For(TimeSpan.FromMinutes(10), 4, ItemWallClock.LetThePolicyDecide));
    }
}
