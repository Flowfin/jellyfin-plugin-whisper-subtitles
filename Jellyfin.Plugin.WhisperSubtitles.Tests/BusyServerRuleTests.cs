using System;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The busy-server rule, in both directions.
/// </summary>
/// <remarks>
/// #22 asks for exactly that phrase, and it is the shape that matters here rather
/// than a count of assertions. A rule that held every item would pass a suite
/// asking only whether a busy server holds one, and it would stop this plugin
/// transcribing anything at all on a server that is never completely quiet - which
/// is a failure an operator would read as the plugin being broken rather than as a
/// limit doing its job. So an idle server starting an item is a leg with the same
/// weight as a busy one holding it.
///
/// The definition under test is the one decided on #22 on 2026-09-04: at least one
/// active playback session or at least one transcode, read at the moment an item
/// would start. Both halves are exercised on their own, because a rule that read
/// only transcodes would pass every leg that varied both.
///
/// WHAT THIS DOES NOT DO. It says nothing about where the two numbers come from,
/// which is <see cref="IServerActivitySource"/> and the server's session manager on
/// the far side of it, and nothing about when the rule is asked, which is the run
/// that does not exist yet.
/// </remarks>
public class BusyServerRuleTests
{
    [Fact]
    public void An_idle_server_starts_the_item()
    {
        var decision = BusyServerRule.Decide(ServerActivity.Idle, BusyServerRule.Pause);

        Assert.True(decision.MayStart);
        Assert.Null(decision.Reason);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 3)]
    [InlineData(2, 2)]
    public void A_server_doing_something_for_somebody_holds_the_item(int sessions, int transcodes)
    {
        var decision = BusyServerRule.Decide(new ServerActivity(sessions, transcodes), BusyServerRule.Pause);

        Assert.False(decision.MayStart);
        Assert.NotNull(decision.Reason);
    }

    [Fact]
    public void One_session_is_enough_rather_than_a_threshold()
    {
        // The thing being protected is one person's playback rather than an average,
        // so the smallest busy server there is has to hold the item. A threshold
        // would be a number somebody had to defend on every machine this runs on.
        var decision = BusyServerRule.Decide(new ServerActivity(1, 0), BusyServerRule.Pause);

        Assert.False(decision.MayStart);
    }

    [Fact]
    public void The_reason_says_which_half_of_the_definition_held_the_item()
    {
        // An operator reading "the run did nothing overnight" needs to know whether
        // it was somebody watching or the server transcoding, because the two have
        // different answers.
        var playing = BusyServerRule.Decide(new ServerActivity(2, 0), BusyServerRule.Pause);
        var transcoding = BusyServerRule.Decide(new ServerActivity(0, 2), BusyServerRule.Pause);

        Assert.Contains("playing", playing.Reason, StringComparison.Ordinal);
        Assert.Contains("transcoding", transcoding.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    public void The_other_level_starts_the_item_whatever_the_server_is_doing(int sessions, int transcodes)
    {
        // The operator with hardware bought for this. Without this leg the setting is
        // a level with one meaning, which is a switch that is always on.
        var decision = BusyServerRule.Decide(new ServerActivity(sessions, transcodes), BusyServerRule.StartAnyway);

        Assert.True(decision.MayStart);
    }

    [Fact]
    public void Nobody_choosing_is_not_a_level_the_rule_will_decide_on()
    {
        // The trap a stored empty string is. A configuration file written before this
        // setting existed carries one, and a rule that treated it as a level would
        // pick whichever the list happened to put first rather than the documented
        // default. The configuration rule resolves it; this refuses it.
        Assert.False(BusyServerRule.IsALevel(BusyServerRule.NobodyChose));
        Assert.Throws<ArgumentException>(
            () => BusyServerRule.Decide(ServerActivity.Idle, BusyServerRule.NobodyChose));
    }

    [Theory]
    [InlineData("Pause")]
    [InlineData("paused")]
    [InlineData("start-anyway")]
    [InlineData(null)]
    public void A_level_this_release_does_not_know_is_refused_rather_than_guessed_at(string? level)
    {
        Assert.False(BusyServerRule.IsALevel(level));
    }

    [Fact]
    public void The_default_is_the_conservative_level_and_is_one_of_the_levels()
    {
        Assert.Equal(BusyServerRule.Pause, BusyServerRule.Default);
        Assert.True(BusyServerRule.IsALevel(BusyServerRule.Default));
    }

    [Fact]
    public void The_levels_are_the_two_this_release_writes_and_no_more()
    {
        // A third level arriving is a decision rather than an edit, so it moves this
        // and the page's list together or it is red.
        Assert.Equal(new[] { "idle-only", "normal" }, ChildProcessPriority.Levels);
        Assert.Equal(new[] { "pause", "start anyway" }, BusyServerRule.Levels);
    }

    [Fact]
    public void The_source_is_asked_at_the_moment_the_decision_is_taken()
    {
        // The seam side of the same rule. A run holds the answer for as long as it
        // takes to decide one item and never longer: a server somebody starts
        // watching between two items has to hold the next one, so a cached reading
        // would be a limit that stopped working the moment it mattered.
        var source = new StubServerActivitySource();

        var first = BusyServerRule.Decide(source.Current(), BusyServerRule.Pause);
        source.Activity = new ServerActivity(1, 0);
        var second = BusyServerRule.Decide(source.Current(), BusyServerRule.Pause);

        Assert.True(first.MayStart);
        Assert.False(second.MayStart);
        Assert.Equal(2, source.Asks);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void A_count_that_is_not_a_number_of_anything_is_refused(int sessions, int transcodes)
    {
        // A negative count is a source that failed rather than a quiet server, and
        // reading it as one would start items while somebody was watching.
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServerActivity(sessions, transcodes));
    }
}
