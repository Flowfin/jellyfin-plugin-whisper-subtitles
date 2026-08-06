using System;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the number an operator is allowed to set, and that a
/// number above the ceiling is refused rather than quietly reduced. An operator
/// who typed sixteen and got four would go on believing the server was doing
/// sixteen.
/// </summary>
public sealed class ConcurrencyCapTests
{
    [Fact]
    public void Nobody_choosing_means_one_at_a_time()
    {
        Assert.Equal(1, ConcurrencyCap.Default);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(64, 64)]
    public void The_ceiling_is_one_worker_per_processor(int processors, int ceiling)
    {
        Assert.Equal(ceiling, ConcurrencyCap.CeilingFor(processors));
    }

    [Fact]
    public void A_machine_with_no_processors_is_not_a_machine_this_answers_for()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConcurrencyCap.CeilingFor(0));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(4, 8)]
    [InlineData(8, 8)]
    public void A_number_the_machine_can_carry_is_accepted_as_it_was_typed(int requested, int processors)
    {
        var choice = ConcurrencyCap.Choose(requested, processors);

        Assert.True(choice.IsAccepted);
        Assert.Equal(requested, choice.Workers);
        Assert.Null(choice.Refusal);
    }

    [Fact]
    public void A_number_above_the_ceiling_is_refused_and_not_reduced()
    {
        var choice = ConcurrencyCap.Choose(16, 4);

        Assert.False(choice.IsAccepted);
        Assert.Equal(0, choice.Workers);
        Assert.NotNull(choice.Refusal);
        Assert.Contains("16", choice.Refusal, StringComparison.Ordinal);
        Assert.Contains("4", choice.Refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_run_of_no_items_at_a_time_is_refused(int requested)
    {
        var choice = ConcurrencyCap.Choose(requested, 8);

        Assert.False(choice.IsAccepted);
        Assert.NotNull(choice.Refusal);
    }

    [Fact]
    public void An_accepted_choice_cannot_be_built_out_of_a_number_no_run_could_use()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConcurrencyCapChoice.Accepted(0));
    }

    [Fact]
    public void A_refusal_says_something_or_it_is_not_a_refusal()
    {
        Assert.Throws<ArgumentException>(() => ConcurrencyCapChoice.Refused("   "));
    }
}
