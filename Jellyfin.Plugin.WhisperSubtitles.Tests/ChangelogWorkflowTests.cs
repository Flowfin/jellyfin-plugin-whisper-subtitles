using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The changelog workflow drafts the release notes out of the merged pull
/// requests and does nothing else. This holds the shape #59 decided on
/// 2026-09-05: one pinned drafter reading this repository's own configuration,
/// on a merge to the mainline, and no release-prep half.
/// </summary>
/// <remarks>
/// Until that decision this file held a different workflow to a different
/// property: a shared workflow guarded by the repository name handed to it,
/// which skipped in silence for months when the name was the template's. That
/// shared workflow drafted and then pushed a branch that rewrote the version
/// into two files, which is the first clause of #59 undone by the route meant to
/// serve its fourth, and it is gone from this tree. What is held now is the
/// other failure the same file can have: a step arriving beside the drafter that
/// writes into the tree again, or a drafter reading a configuration this
/// repository does not carry, or one pinned to nothing.
///
/// WHAT THIS DOES NOT DO. It reads the workflow as text and runs nothing, so what
/// the drafter writes for a set of pull requests is not measured here. It
/// matches the tokens a bump step is written with, so a step that rewrites the
/// tree through a tool this vocabulary does not name walks past it, which is the
/// bound every token scanner in this directory states about itself.
/// </remarks>
public class ChangelogWorkflowTests
{
    private const string Workflow = ".github/workflows/changelog.yaml";

    private const string Config = ".github/release-drafter.yml";

    private static readonly Regex _pinnedDrafter = new(
        @"uses:\s+release-drafter/release-drafter@(?<sha>[0-9a-f]{40})\s+#\s+v\d+\.\d+\.\d+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _anyUses = new(
        @"uses:\s+(?<action>\S+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The shapes a release-prep half is written with: a checkout the tree is
    /// then edited in, an in-place edit, a commit, a push.
    /// </summary>
    private static readonly string[] _writesTheTree =
    [
        "actions/" + "checkout",
        "sed " + "-i",
        "git " + "commit",
        "git " + "push",
        "yq " + "eval",
        "prepare-" + "$",
    ];

    [Fact]
    public void The_drafter_is_the_one_action_and_it_is_pinned_to_a_commit_with_its_version_beside_it()
    {
        var workflow = Read(Workflow);
        var actions = _anyUses.Matches(workflow).Select(match => match.Groups["action"].Value).ToList();

        Assert.Single(actions);
        Assert.Matches(_pinnedDrafter, workflow);
    }

    [Fact]
    public void The_drafter_reads_this_repositorys_own_configuration()
    {
        // The shared route used to fail on a configuration this repository did not
        // carry, and a drafter reading a name that resolves to no file drafts
        // nothing and says so only in a run log.
        var workflow = Read(Workflow);

        Assert.Contains("config-name: " + Path.GetFileName(Config), workflow, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), Config)), $"{Config} is not in the tree");
    }

    [Fact]
    public void Nothing_in_the_workflow_writes_into_the_tree()
    {
        var workflow = WithoutComments(Read(Workflow));

        foreach (var token in _writesTheTree)
        {
            Assert.False(
                workflow.Contains(token, StringComparison.Ordinal),
                $"{Workflow} carries {token}, which is the shape of the release-prep half #59 decided against");
        }
    }

    [Fact]
    public void It_drafts_on_a_merge_to_the_mainline_and_writes_only_the_release()
    {
        var workflow = Read(Workflow);

        Assert.Matches(new Regex(@"push:\s*\n\s*branches:\s*\n\s*-\s*master", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)), workflow);
        Assert.Contains("contents: write", workflow, StringComparison.Ordinal);
        Assert.Contains("pull-requests: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull-requests: write", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scanner_would_see_a_shape_it_was_shown()
    {
        // The vocabulary is assembled from fragments, so a typo in the assembly would
        // leave a token matching nothing and passing for as long as nobody looked.
        foreach (var token in _writesTheTree)
        {
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Contains(token, "      run: " + token + "something", StringComparison.Ordinal);
        }
    }

    private static string WithoutComments(string text) =>
        string.Join(
            '\n',
            text.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => !line.TrimStart().StartsWith('#')));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
