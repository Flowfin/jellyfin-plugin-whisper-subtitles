using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The disposition page argues from pasted command output, and this reads the half
/// of that evidence a checkout can answer.
/// </summary>
/// <remarks>
/// The failure this is written against has happened three times on this page, in one
/// direction each time. A command was run, its output was pasted under it, the thing
/// the command asked about moved afterwards, and nothing said so. One paste named the
/// required status checks and named three of them while the ruleset had four. One
/// filtered the code-scanning tab to open alerts and returned nothing once the alert
/// there was dismissed. One froze a window of workflow runs that moves with every push.
///
/// What separates those three from the rest of the page is the subject rather than the
/// wording. A command that reads the tracked tree can be run again by anything holding
/// the tree, and every such paste on the page reproduces today. A command that reads a
/// repository setting or the tracker's own state cannot be run by this suite at all,
/// because every test here runs with the machine offline, so a paste of one is a claim
/// that ages in silence. The repair was to hand those three commands to the reader
/// without freezing their output, and the evidence that replaced the first of them is a
/// search over a workflow file, which is the kind this can hold.
///
/// WHAT THIS DOES NOT DO. It reads one paste and the absence of one shape, not every
/// claim on the page. It says nothing about whether a disposition is right or still
/// applies; a paragraph drawing the wrong conclusion from correct output passes here.
/// The score and the heading set are compared elsewhere, by
/// `.github/scripts/refuse-an-undisposed-finding.sh` in the audit's own job, against the
/// document that run produced.
///
/// The second leg refuses one literal shape and not the class. A ruleset pasted in some
/// other form, or a second tab query frozen under a heading, passes it. A wider rule is
/// not available here: this page legitimately quotes the code-scanning tab in the two
/// sections whose subject is the tab, so refusing tab queries outright would refuse the
/// page for saying the true thing.
/// </remarks>
public class ScorecardDispositionsPageTests
{
    /// <summary>
    /// The workflow whose triggers the page pastes, relative to the repository root.
    /// </summary>
    private const string AuditWorkflow = ".github/workflows/scorecard.yml";

    /// <summary>
    /// The search the page quotes, and the one re-run below. Held as one constant so a
    /// command repointed on the page stops being found here rather than being found and
    /// answered from somewhere else.
    /// </summary>
    private const string TriggerCommand =
        "git grep -nE '^  (branch_protection_rule|schedule|push):' -- " + AuditWorkflow;

    /// <summary>
    /// The same expression the command names, applied to the workflow file line by line.
    /// </summary>
    private static readonly Regex TriggerLine =
        new("^  (branch_protection_rule|schedule|push):", RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void The_trigger_search_prints_what_the_page_pastes_under_it()
    {
        var pasted = PastedUnder(TriggerCommand);
        var found = TriggerLines();

        Assert.True(
            pasted.SequenceEqual(found, StringComparer.Ordinal),
            $"docs/scorecard-dispositions.md pastes {Show(pasted)} under \"{TriggerCommand}\" and {AuditWorkflow} answers {Show(found)}. The page is then describing a different workflow from the one it is in, and the sentence above the paste is what a reader takes from it.");
    }

    [Fact]
    public void The_page_does_not_freeze_the_required_status_checks_again()
    {
        // The ruleset is a repository setting. Nothing in this repository reads one, so
        // a paste of it is evidence no route here can re-derive, and this page carried a
        // stale one for as long as it took a person to notice.
        var frozen = Lines()
            .Select((text, index) => (Number: index + 1, Text: text))
            .Where(line => line.Text.Contains("\"required\":[", StringComparison.Ordinal))
            .Select(line => $"line {line.Number}")
            .ToList();

        Assert.True(
            frozen.Count == 0,
            $"docs/scorecard-dispositions.md pastes a required status check list at {Show(frozen)}. That set lives in the ruleset and moves without this page, it has already been wrong here, and the command above it is what a reader runs instead.");
    }

    /// <summary>
    /// The lines of the fenced block a command opens, in order and unaltered.
    /// </summary>
    /// <remarks>
    /// This page fences its blocks rather than indenting them, so the paste is every
    /// line between the command and the fence that closes the block. An empty run is a
    /// paste claiming the command prints nothing, which is a claim like any other and is
    /// compared like one.
    /// </remarks>
    /// <param name="command">The command line as the page writes it.</param>
    /// <returns>Each pasted line.</returns>
    private static List<string> PastedUnder(string command)
    {
        var lines = Lines();
        var at = Array.FindIndex(lines, line => line.Equals(command, StringComparison.Ordinal));

        Assert.True(
            at >= 0,
            $"docs/scorecard-dispositions.md no longer quotes \"{command}\". Either the command moved or its wording did, and the paste under it is then held by nothing.");

        var pasted = new List<string>();
        var fence = "``" + "`";

        for (var i = at + 1; i < lines.Length && !lines[i].StartsWith(fence, StringComparison.Ordinal); i++)
        {
            pasted.Add(lines[i]);
        }

        return pasted;
    }

    /// <summary>
    /// What the quoted search returns, formatted the way the tool the page names formats
    /// it, which is the path, the one-based line number and the line.
    /// </summary>
    /// <returns>One entry per matching line, in file order.</returns>
    private static List<string> TriggerLines()
    {
        var path = Path.Combine(RepositoryRoot(), AuditWorkflow);

        Assert.True(
            File.Exists(path),
            $"the page quotes a search over {AuditWorkflow} and there is no such file to search");

        return File.ReadAllLines(path)
            .Select((text, index) => (Number: index + 1, Text: text))
            .Where(line => TriggerLine.IsMatch(line.Text))
            .Select(line => $"{AuditWorkflow}:{line.Number}:{line.Text}")
            .ToList();
    }

    private static string Show(IEnumerable<string> entries)
    {
        var listed = string.Join(", ", entries);

        return listed.Length == 0 ? "nothing" : listed;
    }

    /// <summary>
    /// The page, read out of the checkout rather than out of a copy beside the assembly,
    /// for the reason its neighbours in this suite give.
    /// </summary>
    /// <returns>The page, line by line, with the line endings removed.</returns>
    private static string[] Lines() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "scorecard-dispositions.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
