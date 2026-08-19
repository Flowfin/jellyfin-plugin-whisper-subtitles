using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Hygiene;

/// <summary>
/// The rules that read a pull request rather than the code in it.
/// </summary>
/// <remarks>
/// Two tiers, and the split is the point rather than a convenience. A rule in the
/// failing tier has no judgement in it: a reader who disagrees with a verdict is
/// disagreeing about whether a string is present, which is not an argument anybody
/// has. A rule that could reasonably be broken on purpose annotates and never
/// fails, because a check people learn to argue with is one they learn to route
/// around, and then the rules with no judgement in them go the same way.
///
/// Every rule here is a function over values, so what proves one is a unit test
/// rather than a deliberately bad pull request somebody has to open, watch fail
/// and then remember to close.
/// </remarks>
internal static class HygieneRules
{
    /// <summary>
    /// How many changed lines a diff may carry before the advisory tier says
    /// something about it.
    /// </summary>
    /// <remarks>
    /// A number in the tier that cannot fail anything, because a change over it is
    /// sometimes right. The figure is the one the corpus this repository's practice
    /// comes from settled on, and what it is for is a reviewer knowing before they
    /// start that they are being asked for more than an ordinary read.
    /// </remarks>
    public const int LargeDiffLines = 400;

    /// <summary>
    /// The manifest the version and the changelog are both fields of.
    /// </summary>
    /// <remarks>
    /// Named once and quoted into every verdict below, so a reader meeting a red
    /// check is told which file to open rather than left to find it.
    /// </remarks>
    public const string ManifestPath = "build.yaml";

    /// <summary>
    /// Whether a piece of text names an issue in this repository.
    /// </summary>
    /// <remarks>
    /// A hash and at least one digit, which is what the tracker itself links. It
    /// says nothing about whether the issue exists or is the right one; that is a
    /// judgement, and a rule in the failing tier may not make one.
    /// </remarks>
    /// <param name="text">The text to read.</param>
    /// <returns>Whether an issue reference is present.</returns>
    public static bool NamesAnIssue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        for (var index = 0; index < text.Length - 1; index++)
        {
            if (text[index] == '#' && char.IsAsciiDigit(text[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The commit subjects that name no issue.
    /// </summary>
    /// <param name="subjects">The subject line of every non-merge commit in the range.</param>
    /// <returns>The subjects that carry no reference, in the order they were given.</returns>
    public static IReadOnlyList<string> SubjectsNamingNoIssue(IEnumerable<string> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        return subjects
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Where(subject => !NamesAnIssue(subject))
            .ToArray();
    }

    /// <summary>
    /// What a manifest gives a field, as the field stands in the file.
    /// </summary>
    /// <remarks>
    /// A reader for two fields of one document rather than a parser. It takes what
    /// is left of the line the field is declared on together with every line
    /// indented under it, because the changelog is a block scalar: the line
    /// declaring it carries a marker and never the text, so a reader stopping at
    /// the end of that line reports a rewritten changelog as an unchanged one.
    ///
    /// Only a declaration at column nought is the document's own field, so a word
    /// sitting inside the description is not read as one.
    /// </remarks>
    /// <param name="manifest">The manifest's text.</param>
    /// <param name="field">The field to read.</param>
    /// <returns>The value, or <c>null</c> where the manifest declares no such field.</returns>
    public static string? ManifestField(string? manifest, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (string.IsNullOrWhiteSpace(manifest))
        {
            return null;
        }

        var lines = manifest.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var declaration = field + ":";

        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith(declaration, StringComparison.Ordinal))
            {
                continue;
            }

            var value = new List<string> { lines[index][declaration.Length..] };

            for (var below = index + 1; below < lines.Length; below++)
            {
                var line = lines[below];
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                {
                    break;
                }

                value.Add(line);
            }

            return string.Join('\n', value).Trim();
        }

        return null;
    }

    /// <summary>
    /// Whether a change that moved the manifest's version moved its changelog with it.
    /// </summary>
    /// <remarks>
    /// The changed paths cannot decide this one. The version and the changelog are
    /// two fields of one file, so the manifest is among the paths of a bump whether
    /// or not the changelog moved, and it is among them for a change to the
    /// overview as well. What separates those cases is the file itself at each end
    /// of the range, which is why this rule takes text rather than names.
    ///
    /// It reads what a bump left behind rather than who made it, so it holds the
    /// same way for a number written by hand and for one written by a release
    /// preparation that rewrites both fields in a single commit.
    /// </remarks>
    /// <param name="baseManifest">The manifest as it stands at the base of the range.</param>
    /// <param name="headManifest">The manifest as it stands at the head of the range.</param>
    /// <returns>What the rule decided.</returns>
    public static Verdict VersionBumpCarriesTheChangelog(string? baseManifest, string? headManifest)
    {
        const string Rule = "version-bump-carries-the-changelog";

        var before = ManifestField(baseManifest, "version");
        var after = ManifestField(headManifest, "version");

        if (before is null || after is null)
        {
            // A rule that read nothing and a rule that found nothing wrong look
            // identical from outside, so this refuses rather than passing quietly.
            return new Verdict(
                Rule,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no version could be read from {ManifestPath} at one end of the range, so nothing was compared"));
        }

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return new Verdict(
                Rule,
                true,
                string.Create(CultureInfo.InvariantCulture, $"the version in {ManifestPath} did not move"));
        }

        var changelogBefore = ManifestField(baseManifest, "changelog");
        var changelogAfter = ManifestField(headManifest, "changelog");

        if (changelogBefore is null || changelogAfter is null)
        {
            return new Verdict(
                Rule,
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the version moved from {before} to {after} and {ManifestPath} declares no changelog at one end of the range"));
        }

        var moved = !string.Equals(changelogBefore, changelogAfter, StringComparison.Ordinal);

        return new Verdict(
            Rule,
            moved,
            moved
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"the version moved from {before} to {after} and the changelog moved with it")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"the version moved from {before} to {after} and the changelog in {ManifestPath} did not; say what the release carries before the number claims it"));
    }

    /// <summary>
    /// Whether a change touches the plugin without touching the test project.
    /// </summary>
    /// <remarks>
    /// Advisory, and it has to be: a change to a comment, to the configuration
    /// page's wording or to a document under the plugin is a legitimate change with
    /// no test to add. What it is for is the other case, where a behaviour moved and
    /// nothing was written to hold it there.
    /// </remarks>
    /// <param name="paths">Every path the change touches.</param>
    /// <returns>Whether the plugin moved and the suite did not.</returns>
    public static bool MovesThePluginWithoutTheSuite(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var touched = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();

        var plugin = touched.Any(path =>
            path.StartsWith("Jellyfin.Plugin.WhisperSubtitles/", StringComparison.Ordinal));
        var suite = touched.Any(path =>
            path.StartsWith("Jellyfin.Plugin.WhisperSubtitles.Tests/", StringComparison.Ordinal));

        return plugin && !suite;
    }

    /// <summary>
    /// Whether a diff is large enough for the advisory tier to say so.
    /// </summary>
    /// <param name="changedLines">Lines added and removed together.</param>
    /// <returns>Whether the diff is over the figure above.</returns>
    public static bool IsALargeDiff(int changedLines) => changedLines > LargeDiffLines;

    /// <summary>
    /// What the failing tier decides about one pull request.
    /// </summary>
    /// <param name="body">The pull request's body.</param>
    /// <param name="commitSubjects">The subject line of every non-merge commit in the range.</param>
    /// <param name="baseManifest">The manifest as it stands at the base of the range.</param>
    /// <param name="headManifest">The manifest as it stands at the head of the range.</param>
    /// <returns>One verdict per rule, in the order they are reported.</returns>
    public static IReadOnlyList<Verdict> FailingTier(
        string? body,
        IEnumerable<string> commitSubjects,
        string? baseManifest,
        string? headManifest)
    {
        ArgumentNullException.ThrowIfNull(commitSubjects);

        var unreferenced = SubjectsNamingNoIssue(commitSubjects);

        return
        [
            new Verdict(
                "body-names-an-issue",
                NamesAnIssue(body),
                NamesAnIssue(body)
                    ? "the body names an issue"
                    : "the body names no issue; add the issue this change closes or is part of"),
            new Verdict(
                "commit-subjects-name-an-issue",
                unreferenced.Count == 0,
                unreferenced.Count == 0
                    ? "every commit subject names an issue"
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{unreferenced.Count} commit subject(s) name no issue: {string.Join(" | ", unreferenced)}")),
            VersionBumpCarriesTheChangelog(baseManifest, headManifest)
        ];
    }

    /// <summary>
    /// What the advisory tier says about one pull request. It decides nothing.
    /// </summary>
    /// <param name="changedPaths">Every path the change touches.</param>
    /// <param name="changedLines">Lines added and removed together.</param>
    /// <returns>One note per rule, in the order they are reported.</returns>
    public static IReadOnlyList<Verdict> AdvisoryTier(IEnumerable<string> changedPaths, int changedLines)
    {
        ArgumentNullException.ThrowIfNull(changedPaths);

        var large = IsALargeDiff(changedLines);
        var unheld = MovesThePluginWithoutTheSuite(changedPaths);

        return
        [
            new Verdict(
                "diff-size",
                !large,
                large
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"{changedLines} changed lines, over {LargeDiffLines}; a reviewer is being asked for more than an ordinary read")
                    : string.Create(CultureInfo.InvariantCulture, $"{changedLines} changed lines")),
            new Verdict(
                "plugin-moved-with-the-suite",
                !unheld,
                unheld
                    ? "the plugin changed and the test project did not"
                    : "nothing to say about where the change landed")
        ];
    }
}
