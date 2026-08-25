using System;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the sentence in #22 that the thread count default is
/// below the core count and not equal to it, so the machine keeps something for
/// the job it was bought for, and that a number above the ceiling is refused
/// rather than quietly reduced.
/// </summary>
/// <remarks>
/// The default is asserted as a RELATION to the processor count rather than as a
/// table of expected numbers alone. A table says what half of eight is; the rule
/// this default exists for is that whatever a run takes, something is left, and a
/// table passes unchanged the day somebody makes the fraction one.
/// </remarks>
public sealed class ThreadCountTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 4)]
    [InlineData(12, 6)]
    [InlineData(64, 32)]
    public void Nobody_choosing_leaves_the_machine_something(int processors, int expected)
    {
        var chosen = ThreadCount.DefaultFor(processors);

        Assert.Equal(expected, chosen);
        Assert.True(
            chosen < processors,
            $"a default of {chosen} on {processors} processors takes the machine an operator did not offer");
        Assert.True(chosen >= 1);
    }

    [Fact]
    public void One_processor_is_the_machine_where_there_is_no_value_below_it()
    {
        // Stated at DefaultFor and asserted here so it is a decision rather than
        // an edge the rule above happens to round into. Refusing to transcribe on
        // a one-processor server would be a limit that removed the feature.
        Assert.Equal(1, ThreadCount.DefaultFor(1));
    }

    [Fact]
    public void An_odd_processor_count_rounds_towards_leaving_more_behind()
    {
        // Three processors could give one or two. It gives one, because rounding
        // the other way is the direction that takes what it was not offered.
        Assert.Equal(1, ThreadCount.DefaultFor(3));
        Assert.Equal(3, ThreadCount.DefaultFor(7));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(64, 64)]
    public void The_ceiling_is_one_thread_per_processor(int processors, int ceiling)
    {
        Assert.Equal(ceiling, ThreadCount.CeilingFor(processors));
    }

    [Fact]
    public void A_machine_with_no_processors_is_not_a_machine_this_answers_for()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadCount.CeilingFor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadCount.DefaultFor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadCount.Choose(1, 0));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(4, 8)]
    [InlineData(8, 8)]
    public void A_number_the_machine_can_carry_is_accepted_as_it_was_typed(int requested, int processors)
    {
        var choice = ThreadCount.Choose(requested, processors);

        Assert.True(choice.IsAccepted);
        Assert.Equal(requested, choice.Threads);
        Assert.Null(choice.Refusal);
    }

    [Fact]
    public void An_operator_may_ask_for_the_whole_machine_for_one_item()
    {
        // The ceiling is one per processor rather than the default, so a machine
        // bought for this can be given all of it. What the default protects is the
        // operator who has not been to the page, not the one who has.
        var choice = ThreadCount.Choose(8, 8);

        Assert.True(choice.IsAccepted);
        Assert.Equal(8, choice.Threads);
    }

    [Fact]
    public void A_number_above_the_ceiling_is_refused_and_not_reduced()
    {
        var choice = ThreadCount.Choose(32, 8);

        Assert.False(choice.IsAccepted);
        Assert.Equal(0, choice.Threads);
        Assert.NotNull(choice.Refusal);
        Assert.Contains("32", choice.Refusal, StringComparison.Ordinal);
        Assert.Contains("8", choice.Refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_number_that_is_not_a_number_of_threads_is_refused(int requested)
    {
        var choice = ThreadCount.Choose(requested, 8);

        Assert.False(choice.IsAccepted);
        Assert.Equal(0, choice.Threads);
        Assert.NotNull(choice.Refusal);
    }

    [Fact]
    public void A_refused_choice_and_a_choice_of_zero_are_not_the_same_state()
    {
        // The reason the choice is a type rather than an integer. Both carry zero
        // threads; only one of them is a state a caller may run in.
        var refused = ThreadCount.Choose(0, 8);

        Assert.Equal(0, refused.Threads);
        Assert.False(refused.IsAccepted);
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadCountChoice.Accepted(0));
    }

    [Fact]
    public void A_refusal_says_something()
    {
        Assert.Throws<ArgumentException>(() => ThreadCountChoice.Refused("   "));
    }
}
