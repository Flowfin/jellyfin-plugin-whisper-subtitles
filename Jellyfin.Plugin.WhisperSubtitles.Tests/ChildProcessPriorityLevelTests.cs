using System;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The priority control as a named level rather than a switch.
/// </summary>
/// <remarks>
/// The neighbour of <see cref="ChildProcessPriorityTests"/> and a different
/// subject. That one holds what the local backend does with the process it started,
/// including that a platform refusing the lower priority still gets its transcript.
/// This holds the vocabulary an operator sets, which is what #22 decided on
/// 2026-09-04 and which the tree did not have: the lowering was unconditional and
/// there was no level to read.
///
/// A level rather than a switch, because a third level added later leaves every
/// stored level meaning what it meant, and a switch set to off says nothing about
/// which of two later meanings its owner wanted.
/// </remarks>
public class ChildProcessPriorityLevelTests
{
    [Fact]
    public void The_lowered_level_asks_and_the_ordinary_one_does_not()
    {
        Assert.True(ChildProcessPriority.LowersPriority(ChildProcessPriority.IdleOnly));
        Assert.False(ChildProcessPriority.LowersPriority(ChildProcessPriority.Normal));
    }

    [Fact]
    public void The_default_is_what_the_tree_already_did()
    {
        // The setting arriving must not change what an operator who never opens the
        // page gets, and what they got was a lowered child.
        Assert.Equal(ChildProcessPriority.IdleOnly, ChildProcessPriority.Default);
        Assert.True(ChildProcessPriority.LowersPriority(ChildProcessPriority.Default));
    }

    [Fact]
    public void Nobody_choosing_is_not_a_level()
    {
        // A file written before this setting existed carries the empty string, and a
        // run that read it as a level would take whichever the list put first.
        Assert.False(ChildProcessPriority.IsALevel(ChildProcessPriority.NobodyChose));
        Assert.Throws<ArgumentException>(
            () => ChildProcessPriority.LowersPriority(ChildProcessPriority.NobodyChose));
    }

    [Theory]
    [InlineData("Idle-only")]
    [InlineData("idle only")]
    [InlineData("below normal")]
    [InlineData(null)]
    public void A_level_this_release_does_not_know_is_refused_rather_than_guessed_at(string? level)
    {
        Assert.False(ChildProcessPriority.IsALevel(level));
    }

    [Fact]
    public void Every_level_the_release_writes_is_one_the_rule_can_decide()
    {
        // The direction that would otherwise pass silently: a level added to the list
        // and not to the rule reads as a level an operator may set and throws when a
        // run reaches it.
        Assert.All(ChildProcessPriority.Levels, level => Assert.True(ChildProcessPriority.IsALevel(level)));
        Assert.All(ChildProcessPriority.Levels, level => ChildProcessPriority.LowersPriority(level));
    }

    [Fact]
    public void Exactly_one_of_the_levels_leaves_the_child_where_the_platform_started_it()
    {
        // A vocabulary where every level lowered the priority would be a switch that
        // is always on, which is the state this replaces.
        Assert.Single(ChildProcessPriority.Levels, level => !ChildProcessPriority.LowersPriority(level));
    }
}
