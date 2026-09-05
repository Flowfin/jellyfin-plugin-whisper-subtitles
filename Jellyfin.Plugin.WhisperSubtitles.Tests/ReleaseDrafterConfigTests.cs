using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The drafter's configuration, read against the two things it has to agree
/// with: the tag shape the publish route releases from, and the labels #59
/// decided a change is classified by.
/// </summary>
/// <remarks>
/// The configuration is read by a route on the server and by nothing in this
/// suite otherwise, so a template that spells a tag the publish workflow refuses,
/// or a category naming a label the board does not use, is a release note that is
/// wrong on the day the first release is cut and green until then. Both are
/// decidable from the tree: the tag pattern is in the publish workflow's trigger,
/// and the labels are the five the decision names, written here as the list to
/// argue with rather than derived from a board this suite may not reach.
///
/// It reads the file as lines rather than as YAML, because this suite carries no
/// YAML reader and the file's shape is flat enough for a line to be the unit: a
/// key at the margin, a label as a quoted item under a category.
///
/// WHAT THIS DOES NOT DO. It does not run the drafter, so what the notes look
/// like for a set of pull requests is not measured here, and it does not read
/// the board, so a label that exists in this file and not on the board is caught
/// by a reader and not by this.
/// </remarks>
public class ReleaseDrafterConfigTests
{
    private const string Config = ".github/release-drafter.yml";

    private const string Publish = ".github/workflows/publish.yaml";

    /// <summary>
    /// The labels the decision of 2026-09-05 on #59 names as the ones a change is
    /// classified by, and the one the board renamed the second to for issues.
    /// </summary>
    private static readonly string[] _labelsTheDecisionNames =
    [
        "security",
        "enhancement",
        "bug",
        "documentation",
        "chore",
    ];

    private static readonly Regex _keyValue = new(
        @"^(?<key>[a-z-]+):\s*'(?<value>[^']*)'\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _labelItem = new(
        @"^\s+-\s+'(?<label>[^']+)'\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _tagTrigger = new(
        @"^\s+-\s+""(?<pattern>\[0-9\]\+[^""]*-stable)""\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_reader_finds_the_keys_and_the_labels_rather_than_comparing_nothing()
    {
        var keys = Keys();
        var labels = Labels();

        Assert.True(keys.Count > 3, $"the reader found {keys.Count} quoted keys in {Config}");
        Assert.True(labels.Count > 3, $"the reader found {labels.Count} labels in {Config}");
        Assert.NotEmpty(TagPatterns());
    }

    [Fact]
    public void The_name_and_the_tag_the_drafter_writes_are_ones_the_publish_route_releases_from()
    {
        // The publish workflow runs on a pushed tag matching its own patterns, and
        // a drafter writing a tag outside them is a draft nobody can publish from.
        var keys = Keys();

        foreach (var key in new[] { "name-template", "tag-template" })
        {
            Assert.True(keys.ContainsKey(key), $"{Config} carries no {key}");
            Assert.EndsWith("-stable", keys[key], StringComparison.Ordinal);
            Assert.Contains("$RESOLVED_VERSION", keys[key], StringComparison.Ordinal);
        }

        Assert.All(
            TagPatterns(),
            pattern => Assert.EndsWith("-stable", pattern, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_label_the_decision_names_has_a_category()
    {
        var labels = Labels();

        foreach (var named in _labelsTheDecisionNames)
        {
            Assert.True(
                labels.Contains(named),
                $"#59 decided a change carrying {named} is classified, and {Config} has no category naming it");
        }
    }

    [Fact]
    public void Every_line_names_the_pull_request_it_came_from()
    {
        // A note without the number is a note a reader cannot follow back to the
        // argument for the change.
        var keys = Keys();

        Assert.True(keys.ContainsKey("change-template"), $"{Config} carries no change-template");
        Assert.Contains("$TITLE", keys["change-template"], StringComparison.Ordinal);
        Assert.Contains("#$NUMBER", keys["change-template"], StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_excluded_so_an_unclassified_change_is_listed_rather_than_dropped()
    {
        var text = Read(Config);

        Assert.Contains("exclude-labels: []", text, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Keys() =>
        _keyValue.Matches(Read(Config))
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value, StringComparer.Ordinal);

    private static HashSet<string> Labels() =>
        _labelItem.Matches(Read(Config))
            .Select(match => match.Groups["label"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static List<string> TagPatterns() =>
        _tagTrigger.Matches(Read(Publish))
            .Select(match => match.Groups["pattern"].Value)
            .ToList();

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
