using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A workflow file that names a ruleset names the one this repository's own routes
/// read, and this refuses any other name.
/// </summary>
/// <remarks>
/// The failure this is written against had landed and stood. The dependency review
/// workflow explained why its job carries no name by naming a ruleset, and the name it
/// gave belongs to the repository this gate is measured against rather than to this one.
/// Two things followed and only the first is a wording matter. A reader looking for the
/// ruleset it named found none here. And the sentence asserted that renaming the job
/// would break a required status check, which is a claim about this repository's gate
/// that was not true: the ruleset here does not name that check at all, and making it do
/// so is what #53 asks for and #54 decides.
///
/// What makes the comparison possible without a network is that this tree already reads
/// the ruleset by name in three places and every one of them selects the same name. So
/// the name is not a constant written down here. It is derived from the routes that use
/// it, and a repository whose ruleset is renamed moves those routes and this check
/// together.
///
/// WHAT THIS DOES NOT DO, and the first bound is the largest. It compares a name against
/// the names this tree selects by, and nothing here reads a ruleset, so a workflow naming
/// a ruleset that is declared here and does not exist on the server passes. That is the
/// same bound docs/scorecard-dispositions.md carries for the same reason: the ruleset is
/// a repository setting and every test here runs with the machine offline.
///
/// It reads one shape, a quoted name followed by the word ruleset, and not the class. A
/// name written without quotes, or after the word rather than before it, is invisible to
/// it. .github/workflows/fuzz.yml and .github/workflows/pr-hygiene.yml both carry that
/// second spelling today and are not judged here.
///
/// Its subject is .github/workflows/. The documentation argues about the ruleset at
/// length and is deliberately outside, because the pastes there are held by
/// ScorecardDispositionsPageTests and two guards over one file is how a repair gets made
/// twice and reverted once.
///
/// And it says nothing about whether a sentence naming the right ruleset says a true
/// thing about it. A comment claiming the gate requires a check it does not require
/// passes, so long as it names the gate correctly.
/// </remarks>
public class RulesetNamedInWorkflowsTests
{
    /// <summary>
    /// A ruleset selected by name. The caller also requires the line to be reading
    /// rulesets, which is what keeps a bill-of-materials component out of the answer:
    /// .github/workflows/package-contents.yml selects components by name with the same
    /// expression and is about something else entirely.
    /// </summary>
    private static readonly Regex SelectedByName =
        new(@"\.name\s*==\s*""([^""]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// A ruleset named in prose: the name in quotes, then the word it is the name of.
    /// </summary>
    private static readonly Regex NamedInProse =
        new(@"""([^""]+)""\s+ruleset", RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_ruleset_a_workflow_names_is_one_this_tree_reads_by_that_name()
    {
        var declared = DeclaredRulesetNames();

        Assert.True(
            declared.Count > 0,
            "nothing in this tree selects a ruleset by name any more, so there is no declared name to compare against and this check would pass whatever a workflow claimed");

        var wrong = new List<string>();

        foreach (var file in WorkflowFiles())
        {
            var lines = ReadLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in NamedInProse.Matches(lines[i]))
                {
                    var named = match.Groups[1].Value;

                    if (!declared.Contains(named))
                    {
                        wrong.Add(string.Create(
                            System.Globalization.CultureInfo.InvariantCulture,
                            $"{Relative(file)}:{i + 1} names \"{named}\""));
                    }
                }
            }
        }

        Assert.True(
            wrong.Count == 0,
            $"a workflow names a ruleset this tree does not read by that name: {string.Join(", ", wrong)}. The names this tree selects by are {string.Join(", ", declared)}. A workflow explaining its own shape by a gate that is somewhere else sends a reader looking for a setting this repository does not have, and the reason it gives for that shape goes unchecked with it.");
    }

    /// <summary>
    /// Every ruleset name this tree selects by, read out of the routes that use it rather
    /// than written down here.
    /// </summary>
    /// <returns>The distinct names, which is one name while the tree agrees with itself.</returns>
    private static HashSet<string> DeclaredRulesetNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var sources = WorkflowFiles().Append(Path.Combine(RepositoryRoot(), "CONTRIBUTING.md"));

        foreach (var file in sources)
        {
            foreach (var line in ReadLines(file))
            {
                if (!line.Contains("rulesets", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in SelectedByName.Matches(line))
                {
                    names.Add(match.Groups[1].Value);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// The workflow files, found in the directory rather than listed, so a file added
    /// tomorrow is judged on the day it arrives.
    /// </summary>
    /// <returns>Each workflow file's full path, in a stable order.</returns>
    private static List<string> WorkflowFiles()
    {
        var folder = Path.Combine(RepositoryRoot(), ".github", "workflows");

        Assert.True(
            Directory.Exists(folder),
            "there is no .github/workflows directory, so this check has nothing to read and would pass for the wrong reason");

        return Directory
            .EnumerateFiles(folder)
            .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A file's lines, with the line endings removed so a clone that rewrote them reads
    /// the same as one that did not.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The file, line by line.</returns>
    private static string[] ReadLines(string path) =>
        File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot(), path).Replace(Path.DirectorySeparatorChar, '/');

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
