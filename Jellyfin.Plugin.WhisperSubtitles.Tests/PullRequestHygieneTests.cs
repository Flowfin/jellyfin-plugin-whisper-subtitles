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
    /// <summary>
    /// The manifest cut to the fields the version rule reads, with a declaration
    /// between them, so a value is followed by the next field the way it is in the
    /// file. The lines are joined here rather than written as one literal, because
    /// what this fixture is about is where a value ends, and a clone that checked
    /// the source out with the other line ending would be testing something else.
    /// </summary>
    private static readonly string Manifest = string.Join(
        '\n',
        "---",
        "name: \"Whisper Subtitles\"",
        "version: \"1.0.0.0\"",
        "targetAbi: \"10.11.0.0\"",
        "changelog: >",
        "  what the release carries");

    /// <summary>
    /// The same manifest with the version moved on and no changelog field at all.
    /// </summary>
    private static readonly string ManifestWithNoChangelog = string.Join(
        '\n',
        "---",
        "name: \"Whisper Subtitles\"",
        "version: \"1.1.0.0\"",
        "targetAbi: \"10.11.0.0\"",
        "category: \"General\"");

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
        var verdicts = HygieneRules.FailingTier(
            "Closes #80.",
            ["Add the hygiene gate (#80)"],
            Manifest,
            Manifest);

        Assert.All(verdicts, verdict => Assert.True(verdict.Held, verdict.Detail));
        Assert.Equal(
            new[] { "body-names-an-issue", "commit-subjects-name-an-issue", "version-bump-carries-the-changelog" },
            verdicts.Select(verdict => verdict.Rule).ToArray());
    }

    [Fact]
    public void Every_rule_that_decides_says_what_it_found_whether_or_not_it_held()
    {
        // A verdict with no detail is a red check that sends its reader to the
        // source of the check to find out what it wanted.
        var held = HygieneRules.FailingTier("Closes #80.", ["Add the hygiene gate (#80)"], Manifest, Manifest);
        var broken = HygieneRules.FailingTier("nothing", ["Add the hygiene gate"], Manifest, Bumped(Manifest));

        Assert.All(held.Concat(broken), verdict => Assert.False(string.IsNullOrWhiteSpace(verdict.Detail)));
        Assert.Contains("Add the hygiene gate", broken.Single(v => v.Rule == "commit-subjects-name-an-issue").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_version_bump_that_leaves_the_changelog_alone_is_refused_and_the_same_bump_carrying_one_is_not()
    {
        // One change apart, so the only thing between the two verdicts is the field
        // this rule exists for.
        var silent = HygieneRules.VersionBumpCarriesTheChangelog(Manifest, Bumped(Manifest));
        var spoken = HygieneRules.VersionBumpCarriesTheChangelog(Manifest, Rewritten(Bumped(Manifest)));

        Assert.False(silent.Held);
        Assert.Contains("1.0.0.0", silent.Detail, StringComparison.Ordinal);
        Assert.Contains("1.1.0.0", silent.Detail, StringComparison.Ordinal);
        Assert.True(spoken.Held, spoken.Detail);
    }

    [Fact]
    public void A_change_that_leaves_the_version_where_it_was_is_not_asked_about_the_changelog()
    {
        // Most pull requests here touch neither field, and a rule asking them for a
        // changelog would be the judgement call this tier may not make. A changelog
        // reworded on its own is the same case from the other side.
        Assert.True(HygieneRules.VersionBumpCarriesTheChangelog(Manifest, Manifest).Held);
        Assert.True(HygieneRules.VersionBumpCarriesTheChangelog(Manifest, Rewritten(Manifest)).Held);
    }

    [Fact]
    public void The_reader_takes_the_text_under_a_block_scalar_and_not_only_the_line_that_opens_it()
    {
        // The one-character mistake this rule invites. The line declaring the
        // changelog carries a marker and never the text, so it is identical on both
        // sides of every bump, and a reader stopping at the end of it would refuse
        // a bump that rewrote the changelog underneath.
        var opened = HygieneRules.ManifestField(Manifest, "changelog");

        Assert.NotNull(opened);
        Assert.Contains("what the release carries", opened, StringComparison.Ordinal);
        Assert.NotEqual(opened, HygieneRules.ManifestField(Rewritten(Manifest), "changelog"));
    }

    [Fact]
    public void A_value_stops_at_the_next_declaration_rather_than_running_into_it()
    {
        Assert.Equal("\"1.0.0.0\"", HygieneRules.ManifestField(Manifest, "version"));
        Assert.Equal("\"10.11.0.0\"", HygieneRules.ManifestField(Manifest, "targetAbi"));
        Assert.Null(HygieneRules.ManifestField(Manifest, "imageUrl"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("name: \"Whisper Subtitles\"\n")]
    public void A_manifest_that_could_not_be_read_is_refused_rather_than_passed(string? headManifest)
    {
        // A rule handed nothing and a rule that found nothing wrong are the same
        // green tick, and only one of them means the pull request is fine.
        var verdict = HygieneRules.VersionBumpCarriesTheChangelog(Manifest, headManifest);

        Assert.False(verdict.Held);
        Assert.Contains("build.yaml", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bump_that_took_the_changelog_field_away_with_it_is_refused()
    {
        Assert.False(HygieneRules.VersionBumpCarriesTheChangelog(Manifest, ManifestWithNoChangelog).Held);
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

    private static string Bumped(string manifest) =>
        manifest.Replace("\"1.0.0.0\"", "\"1.1.0.0\"", StringComparison.Ordinal);

    private static string Rewritten(string manifest) =>
        manifest.Replace("what the release carries", "the first release", StringComparison.Ordinal);

    private static Verdict FailingRule(string rule, string body) =>
        HygieneRules.FailingTier(body, ["Add the hygiene gate (#80)"], Manifest, Manifest)
            .Single(verdict => verdict.Rule == rule);
}
