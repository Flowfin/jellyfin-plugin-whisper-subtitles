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
/// No workflow in this tree hands this repository's label set to a configuration
/// declared outside it, and this refuses one that does.
/// </summary>
/// <remarks>
/// The failure this is written against had landed and stood for as long as the
/// repository existed, because it arrived with the template. A scheduled workflow
/// called a reusable one in another organisation, which fetched its configuration
/// over plain HTTPS at run time and then created, edited and deleted labels here.
/// A label this board declared for itself could not survive it: the configuration
/// naming no such label is exactly the condition under which the run deletes it.
///
/// It fired once, on 2026-09-01 at 04:26 UTC, and the cost is measured rather than
/// supposed. Three labels this board had declared for itself were deleted, and the
/// issues carrying them lost them with nothing written to say why. That reading is
/// on #233 and the removal is #308.
///
/// The direction is why this is worth a check rather than a note. The workflow was
/// correct on the day it was copied in, it is dormant for a month at a time, and the
/// evidence that it did anything is a run log that ages out. So the shape comes back
/// silently - the next time somebody refreshes this repository against the template
/// it is a file nobody diffed - and the next reader to notice is whoever wonders
/// where a label went.
///
/// WHAT THIS DOES NOT DO, and the first bound is the largest. Its subject is
/// <c>.github/workflows/</c>, and a label written by any other route is invisible to
/// it: a person editing labels in the web interface, a workflow that reaches the
/// issues API through a script rather than through a named action, and every fleet
/// tool that runs off this machine are all outside. What it refuses is one shape, a
/// reference by name, and it is a floor rather than coverage.
///
/// It reads a <c>uses:</c> value and matches the two spellings the shape has been
/// written in, the action's own name and the reusable workflow's file name. A
/// synchroniser named neither way - one called <c>labeler</c>, say, or a step that
/// runs the same tool through <c>run:</c> - passes it. Widening the pattern to the
/// word label alone is not the repair: <c>.github/workflows/pr-hygiene.yml</c>
/// annotates a pull request, which is the issues API and is not this, and a check
/// that refuses it teaches people to work around it.
///
/// And it says nothing about the repository settings. Whether the workflow that was
/// deleted still has a schedule registered on the server, and whether the labels it
/// deleted have been restored, are answers that live on the tracker and on the
/// server, and every test here runs with the machine offline.
/// </remarks>
public class LabelSyncAbsentTests
{
    /// <summary>
    /// A reference to a workflow or an action, on one line, in the shape every
    /// workflow in this tree writes it.
    /// </summary>
    private static readonly Regex Reference =
        new(@"uses:\s*(\S+)", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The two spellings the label synchroniser has been referenced by: the action's
    /// own repository name, and the file name of the reusable workflow that runs it.
    /// </summary>
    private static readonly string[] Spellings = ["label-sync", "sync-labels"];

    [Fact]
    public void No_workflow_in_this_tree_synchronises_this_repository_s_labels()
    {
        var found = new List<string>();

        foreach (var file in WorkflowFiles())
        {
            foreach (var reference in Synchronisers(File.ReadAllText(file)))
            {
                found.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Relative(file)} uses {reference}"));
            }
        }

        Assert.True(
            found.Count == 0,
            $"a workflow hands this repository's label set to a configuration declared outside this tree: {string.Join(", ", found)}. That configuration names no label this board declared for itself, so every one of them is deleted on the next run and the issues carrying it lose it with nothing written to say why. It happened on 2026-09-01 and #308 is where the removal is argued.");
    }

    [Fact]
    public void The_scanner_can_see_the_workflows_it_judges()
    {
        // Without this the leg above passes on a tree whose workflows moved, and a
        // reader that found no file at all would report that nothing here syncs
        // labels. The second assertion is the other half of the same vacuity: a
        // reader whose reference pattern had stopped matching would find no `uses:`
        // anywhere and pass just as quietly.
        var files = WorkflowFiles();

        Assert.NotEmpty(files);
        Assert.Contains(files, file => Relative(file) == ".github/workflows/pr-hygiene.yml");
        Assert.Contains(files, file => Reference.IsMatch(File.ReadAllText(file)));
    }

    [Fact]
    public void A_workflow_that_calls_no_synchroniser_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader that refused
        // every `uses:` would satisfy both refusal legs below and say nothing about
        // the real workflows.
        Assert.Empty(Synchronisers(Fixture("clean")));
    }

    [Fact]
    public void The_reader_refuses_the_reusable_workflow_this_board_carried()
    {
        // The defect this class exists for, in the bytes it actually had: the file
        // this change deletes, kept here so the shape stays readable after it is gone
        // from the tree.
        Assert.Equal(
            new[] { "jellyfin/jellyfin-meta-plugins/.github/workflows/sync-labels.yaml@eb99033a7ff644881b014bc0b4169916c854a68b" },
            Synchronisers(Fixture("calls-the-reusable-label-sync-workflow")));
    }

    [Fact]
    public void The_reader_refuses_the_action_that_reusable_workflow_runs()
    {
        // The same defect one layer down, and the reason the reader knows two
        // spellings rather than one. Copying the inner step here instead of calling
        // the reusable workflow deletes exactly the same labels, and it names neither
        // the workflow file nor the organisation the first fixture names.
        Assert.Equal(
            new[] { "EndBug/label-sync@52074158190acb45f3077f9099fea818aa43f97a" },
            Synchronisers(Fixture("runs-the-label-sync-action-directly")));
    }

    [Fact]
    public void Prose_about_the_synchroniser_is_not_a_call_to_one()
    {
        // The near miss, and the reason the pattern reads a `uses:` value rather than
        // the line. A workflow explaining why it does NOT sync labels has to be able
        // to say so, and a reader matching the word anywhere would refuse the comment
        // that records this decision - which is the one place a later reader looks.
        Assert.Empty(Synchronisers(Fixture("explains-the-synchroniser-in-a-comment")));
    }

    [Fact]
    public void No_fixture_is_a_workflow_anything_else_reads()
    {
        // Every fixture here is a workflow this repository must not carry, and each
        // one is kept under an extension no route that walks workflows picks up.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !path.EndsWith("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".yaml.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// Every reference in a workflow that hands the label set to a configuration
    /// outside this tree.
    /// </summary>
    /// <param name="workflow">The workflow text.</param>
    /// <returns>The references, in the order the file carries them.</returns>
    private static List<string> Synchronisers(string workflow) =>
        Reference.Matches(workflow)
            .Select(match => match.Groups[1].Value)
            .Where(reference => Spellings.Any(spelling =>
                reference.Contains(spelling, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>
    /// The workflow files, found in the directory rather than listed, so a file added
    /// tomorrow is judged on the day it arrives.
    /// </summary>
    /// <returns>The paths, ordered so a failure names them the same way twice.</returns>
    private static List<string> WorkflowFiles()
    {
        var directory = Path.Combine(RepositoryRoot(), ".github", "workflows");

        return Directory.GetFiles(directory, "*.yml")
            .Concat(Directory.GetFiles(directory, "*.yaml"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/');

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".yaml.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "label-sync");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
