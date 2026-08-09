using System;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Hygiene;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The rules that read a pull request rather than the code in it.
/// </summary>
/// <remarks>
/// Each rule is proved here, on values, rather than by opening a pull request that
/// breaks it and watching the check go red. That matters more than it sounds: a
/// proof that costs a bad pull request is a proof nobody repeats, so the rule
/// stops being checked the day after it lands, and what is left is a workflow
/// everybody assumes still works.
///
/// The two tiers are tested apart, because what separates them is not what they
/// look at, it is what a broken one costs. A rule that moved from the tier that
/// annotates to the tier that decides would pass every test about what it finds.
/// </remarks>
public sealed class PullRequestHygieneTests
{
    [Fact]
    public void A_body_that_names_an_issue_passes_and_the_same_body_without_one_does_not()
    {
        // The pair this issue asks for, side by side, on one body rather than two,
        // so nothing else differs between the two verdicts.
        const string Without = "Brings the reader the refused-test list is checked by.";
        var with = Without + "\n\nCloses #46.";

        Assert.False(FailingRule("body-names-an-issue", Without).Held);
        Assert.True(FailingRule("body-names-an-issue", with).Held);
    }

    [Theory]
    [InlineData("Closes #46.")]
    [InlineData("Part of #80, whose first condition is the tier below.")]
    [InlineData("see https://github.com/Flowfin/jellyfin-plugin-whisper-subtitles/issues/12 and #12")]
    [InlineData("#1")]
    public void A_reference_is_a_hash_and_a_digit(string body) => Assert.True(HygieneRules.NamesAnIssue(body));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("no reference at all")]
    [InlineData("a hash on its own #")]
    [InlineData("a hash and a letter #forty-six")]
    [InlineData("the number 46 without a hash")]
    public void Anything_else_is_not_a_reference(string? body) => Assert.False(HygieneRules.NamesAnIssue(body));

    [Fact]
    public void The_commit_rule_names_every_subject_that_carries_no_reference()
    {
        // It names them rather than counting them. A check that says two commits
        // are wrong and not which two sends the author back to read the range
        // themselves, which is the work the check was supposed to have done.
        var missing = HygieneRules.SubjectsNamingNoIssue(
        [
            "Read the refused-test list against the suite (#46)",
            "Hand the progress sink the numbers in the order they were reached",
            "Pin the eight shared workflow calls to a commit (#53)",
            "Bound four call jobs to the permissions they use"
        ]);

        Assert.Equal(
            new[]
            {
                "Hand the progress sink the numbers in the order they were reached",
                "Bound four call jobs to the permissions they use"
            },
            missing);
    }

    [Fact]
    public void A_blank_line_in_the_range_is_not_a_commit_that_broke_the_rule()
    {
        // The subjects arrive as a file the workflow wrote, and a file ends with a
        // newline. Reading that as a commit would fail every pull request for a
        // commit that does not exist.
        Assert.Empty(HygieneRules.SubjectsNamingNoIssue(["Closes #1", string.Empty, "   ", "Part of #2"]));
    }

    [Fact]
    public void A_range_where_every_subject_carries_a_reference_satisfies_the_tier()
    {
        var verdicts = HygieneRules.FailingTier("Closes #80.", ["Add the hygiene gate (#80)"]);

        Assert.All(verdicts, verdict => Assert.True(verdict.Held, verdict.Detail));
        Assert.Equal(
            new[] { "body-names-an-issue", "commit-subjects-name-an-issue" },
            verdicts.Select(verdict => verdict.Rule).ToArray());
    }

    [Fact]
    public void Every_rule_that_decides_says_what_it_found_whether_or_not_it_held()
    {
        // A verdict with no detail is a red check that sends its reader to the
        // source of the check to find out what it wanted.
        var held = HygieneRules.FailingTier("Closes #80.", ["Add the hygiene gate (#80)"]);
        var broken = HygieneRules.FailingTier("nothing", ["Add the hygiene gate"]);

        Assert.All(held.Concat(broken), verdict => Assert.False(string.IsNullOrWhiteSpace(verdict.Detail)));
        Assert.Contains("Add the hygiene gate", broken.Single(v => v.Rule == "commit-subjects-name-an-issue").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_change_to_the_plugin_with_no_change_to_the_suite_is_worth_a_note()
    {
        Assert.True(HygieneRules.MovesThePluginWithoutTheSuite(
            ["Jellyfin.Plugin.WhisperSubtitles/Scheduling/BoundedRun.cs"]));

        Assert.False(HygieneRules.MovesThePluginWithoutTheSuite(
        [
            "Jellyfin.Plugin.WhisperSubtitles/Scheduling/BoundedRun.cs",
            "Jellyfin.Plugin.WhisperSubtitles.Tests/BoundedRunTests.cs"
        ]));
    }

    [Fact]
    public void The_test_project_is_not_read_as_the_plugin_because_its_path_begins_the_same_way()
    {
        // The one-character mistake this rule invites is a prefix that stops at the
        // plugin's name, and both directions of it are here. Without the separator
        // the solution file reads as a change to the plugin, and the test project
        // reads as one too.
        Assert.False(HygieneRules.MovesThePluginWithoutTheSuite(
            ["Jellyfin.Plugin.WhisperSubtitles.Tests/BoundedRunTests.cs"]));

        Assert.False(HygieneRules.MovesThePluginWithoutTheSuite(
            ["Jellyfin.Plugin.WhisperSubtitles.sln"]));

        Assert.False(HygieneRules.MovesThePluginWithoutTheSuite(
            ["docs/limits.md", ".github/workflows/pr-hygiene.yml"]));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(400, false)]
    [InlineData(401, true)]
    public void A_diff_is_large_only_once_it_is_over_the_figure(int changedLines, bool large) =>
        Assert.Equal(large, HygieneRules.IsALargeDiff(changedLines));

    [Fact]
    public void The_tier_that_annotates_reports_the_same_shape_and_decides_nothing()
    {
        // What makes it advisory is that the run reads its verdicts and does not
        // act on them. What makes that checkable is that it produces verdicts at
        // all, in the same shape, so a rule cannot be moved between tiers by
        // changing what it returns.
        var noted = HygieneRules.AdvisoryTier(["Jellyfin.Plugin.WhisperSubtitles/Plugin.cs"], 5000);

        Assert.Equal(
            new[] { "diff-size", "plugin-moved-with-the-suite" },
            noted.Select(verdict => verdict.Rule).ToArray());
        Assert.All(noted, verdict => Assert.False(verdict.Held));
        Assert.All(noted, verdict => Assert.False(string.IsNullOrWhiteSpace(verdict.Detail)));
    }

    private static Verdict FailingRule(string rule, string body) =>
        HygieneRules.FailingTier(body, ["Add the hygiene gate (#80)"]).Single(verdict => verdict.Rule == rule);
}
