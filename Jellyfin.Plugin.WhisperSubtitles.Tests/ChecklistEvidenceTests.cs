using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// <c>docs/release-checklist.md</c> answers one of its items by pasting a reading of
/// this tree: a command, and what it returned. This re-runs the command and compares
/// what it returns now against what is pasted beside it.
/// </summary>
/// <remarks>
/// The defect it is written against has already happened on that page. The
/// interoperability item says nothing in this repository installs a sibling plugin,
/// and it pastes a grep over <c>.github/workflows/</c> with <c>exit=1</c> under it as
/// the evidence. The workflow #64 adds reads this repository's own claim record out
/// of <c>interoperability/claims/</c>, so that command stopped returning nothing, and
/// every route stayed green while the page went on showing a reading that no longer
/// reproduced. The paste was repaired by hand in the change that moved the tree; this
/// is the refusal that would have found it instead.
///
/// The sentence the reading supports was still true, which is what makes the shape
/// worth refusing rather than arguing about. A paste that has stopped reproducing
/// tells a reader nothing about whether the claim above it survived, and the reader
/// who checks it finds a disagreement and has to work out which half moved.
///
/// So the pastes are read rather than trusted. A line the command no longer returns
/// and a line it returns that the page does not show are both refused, and so is a
/// page claiming the command returned nothing while it returns something.
///
/// WHAT THIS DOES NOT DO. Its subject is one page and the commands on it that this
/// reader can run, which today is <c>git grep -nEi</c> with a quoted pattern and one
/// pathspec. A command written in another spelling is REFUSED rather than walked
/// past, because a reader that skips what it does not understand grows a silent
/// exemption every time somebody writes a command differently, and the fixture beside
/// this class is that case.
///
/// It runs the pattern over the files on disk under the pathspec, which is what a
/// clean checkout gives and is not the same subject as the index. A file that is
/// present and untracked is read here and not by <c>git grep</c>, so a working tree
/// carrying one reports a difference this page is not about. Every route that judges
/// this repository runs against a checkout of a commit, where the two agree.
///
/// And it says nothing about whether the sentence the reading supports is TRUE of
/// what the reading returns. That is a judgement, the review is where a wrong one is
/// caught, and it is the same bound the neighbouring readers on this page state.
/// </remarks>
public class ChecklistEvidenceTests
{
    /// <summary>
    /// How this page writes a command that returned nothing.
    /// </summary>
    private const string ReturnedNothing = "exit=1";

    /// <summary>
    /// The one command shape this reader runs: a case-insensitive extended grep with
    /// line numbers, a single-quoted pattern, and one pathspec after the separator.
    /// </summary>
    private static readonly Regex Command =
        new(@"^git grep -nEi '([^']*)' -- (\S+)$", RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_reading_this_page_pastes_is_what_the_command_returns_now()
    {
        Assert.Empty(Complaints(File.ReadAllLines(ChecklistPage()), RepositoryRoot()));
    }

    [Fact]
    public void The_reader_finds_a_reading_on_the_page_it_judges()
    {
        // Without this the leg above passes on a page whose pastes moved out of this
        // reader's shape, and a reader that found no command at all would report that
        // every reading on the page reproduces.
        Assert.NotEmpty(Readings(File.ReadAllLines(ChecklistPage())));
    }

    [Fact]
    public void A_paste_of_what_the_command_returns_is_accepted()
    {
        Assert.Empty(Complaints(Page("clean"), FixtureDirectory()));
    }

    [Fact]
    public void A_paste_that_leaves_a_returned_line_out_is_refused()
    {
        var complaint = Assert.Single(Complaints(Page("a-line-the-paste-leaves-out"), FixtureDirectory()));

        Assert.Contains("jellyfin-plugin-sso", complaint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_paste_showing_a_line_the_command_does_not_return_is_refused()
    {
        var complaint = Assert.Single(
            Complaints(Page("a-line-the-command-does-not-return"), FixtureDirectory()));

        Assert.Contains("tree/c.yml.fixture", complaint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_paste_saying_the_command_returned_nothing_while_it_returns_something_is_refused()
    {
        var complaint = Assert.Single(
            Complaints(Page("a-paste-claiming-nothing-while-lines-exist"), FixtureDirectory()));

        Assert.Contains(ReturnedNothing, complaint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reading_of_a_command_that_returns_nothing_at_all_is_refused()
    {
        // The whole reading gone rather than one line of it, which is what a pattern
        // narrowed or a pathspec moved does to a paste nobody re-ran. It is reported
        // as an emptied command rather than as every line being wrong.
        var complaint = Assert.Single(
            Complaints(Page("a-reading-of-a-command-that-returns-none"), FixtureDirectory()));

        Assert.Contains("the command returns none", complaint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_paste_saying_the_command_returned_nothing_when_it_does_is_accepted()
    {
        // The neighbour of the leg above. Without it that one is satisfied by a reader
        // refusing every empty reading, which would refuse the honest one this page
        // carried before #64 landed.
        Assert.Empty(Complaints(Page("a-paste-of-nothing-that-is-right"), FixtureDirectory()));
    }

    [Fact]
    public void A_command_this_reader_cannot_run_is_refused_rather_than_walked_past()
    {
        var complaint = Assert.Single(Complaints(Page("a-command-this-reader-cannot-run"), FixtureDirectory()));

        Assert.Contains("cannot be re-run", complaint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compares every reading pasted on a page against what its command returns.
    /// </summary>
    /// <param name="page">The page, one line per entry.</param>
    /// <param name="root">The directory the pathspecs on it are relative to.</param>
    /// <returns>One complaint per reading that no longer reproduces.</returns>
    private static List<string> Complaints(IReadOnlyList<string> page, string root)
    {
        var complaints = new List<string>();

        foreach (var (command, pasted, line) in Readings(page))
        {
            var parsed = Command.Match(command);

            if (!parsed.Success)
            {
                complaints.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {line}: \"{command}\" cannot be re-run by this reader, so what is pasted under it is trusted rather than read."));

                continue;
            }

            var returned = Returns(root, parsed.Groups[1].Value, parsed.Groups[2].Value);
            var saysNothing = pasted.Count == 1 && string.Equals(pasted[0], ReturnedNothing, StringComparison.Ordinal);
            IReadOnlyList<string> shown = saysNothing ? Array.Empty<string>() : pasted;

            if (saysNothing && returned.Count > 0)
            {
                complaints.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {line}: the page says the command ended {ReturnedNothing} and it returns {returned.Count} line(s), the first being \"{returned[0]}\"."));

                continue;
            }

            if (returned.Count == 0 && shown.Count > 0)
            {
                complaints.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {line}: the page pastes {shown.Count} line(s) and the command returns none."));

                continue;
            }

            if (!shown.SequenceEqual(returned, StringComparer.Ordinal))
            {
                var missing = returned.Except(shown, StringComparer.Ordinal).ToArray();
                var invented = shown.Except(returned, StringComparer.Ordinal).ToArray();

                complaints.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {line}: the reading pasted is not what the command returns. Returned and not shown: {Join(missing)}. Shown and not returned: {Join(invented)}."));
            }
        }

        return complaints;
    }

    /// <summary>
    /// Every fenced block on a page whose first line is a grep of this tree.
    /// </summary>
    /// <param name="page">The page, one line per entry.</param>
    /// <returns>The command, the lines pasted under it, and where the command sits.</returns>
    private static List<(string Command, IReadOnlyList<string> Pasted, int Line)> Readings(IReadOnlyList<string> page)
    {
        var readings = new List<(string, IReadOnlyList<string>, int)>();
        var inside = false;
        var opened = 0;
        var block = new List<string>();

        for (var i = 0; i < page.Count; i++)
        {
            var text = page[i];

            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                if (inside)
                {
                    block.Add(text);
                }

                continue;
            }

            if (!inside)
            {
                inside = true;
                opened = i;
                block = [];

                continue;
            }

            inside = false;

            // Only a block that opens with such a command is a reading. Everything
            // else in a fence on this page is an invocation a releaser types, with a
            // placeholder in it that no run could resolve.
            if (block.Count > 0 && block[0].StartsWith("git grep ", StringComparison.Ordinal))
            {
                readings.Add((block[0], block.Skip(1).ToArray(), opened + 2));
            }
        }

        return readings;
    }

    /// <summary>
    /// What the grep returns, in the shape it prints: path, line number, line.
    /// </summary>
    private static List<string> Returns(string root, string pattern, string pathspec)
    {
        var expression = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
        var directory = Path.Combine(root, pathspec.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(directory))
        {
            return [];
        }

        var lines = new List<string>();

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                     .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
                     .Order(StringComparer.Ordinal))
        {
            var content = File.ReadAllLines(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));

            for (var i = 0; i < content.Length; i++)
            {
                if (expression.IsMatch(content[i]))
                {
                    lines.Add(string.Create(CultureInfo.InvariantCulture, $"{file}:{i + 1}:{content[i]}"));
                }
            }
        }

        return lines;
    }

    private static string Join(string[] lines) =>
        lines.Length == 0 ? "none" : string.Join(" | ", lines);

    private static string[] Page(string name) =>
        File.ReadAllLines(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "checklist-evidence");

    private static string ChecklistPage() =>
        Path.Combine(RepositoryRoot(), "docs", "release-checklist.md");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
