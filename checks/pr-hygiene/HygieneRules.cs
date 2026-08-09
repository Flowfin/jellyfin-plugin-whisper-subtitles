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
    /// <returns>One verdict per rule, in the order they are reported.</returns>
    public static IReadOnlyList<Verdict> FailingTier(string? body, IEnumerable<string> commitSubjects)
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
                        $"{unreferenced.Count} commit subject(s) name no issue: {string.Join(" | ", unreferenced)}"))
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
