using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The changelog workflow calls a shared workflow whose one job is guarded by
/// <c>github.repository == inputs.repository-name</c>, so the name handed to it
/// decides whether anything runs at all.
/// </summary>
/// <remarks>
/// The failure this is written against is silent at every gate this repository
/// has. A name belonging to another repository does not fail the call, does not
/// fail the workflow audit and does not fail a build: the guard is false, the job
/// is skipped, and a skipped job reports green. Every run of this workflow here
/// concluded skipped for months for exactly that reason, and what said so was a
/// person reading the run list rather than anything that refuses.
///
/// What is lost while it is wrong is the release draft and the release-prep pull
/// request, neither of which anybody looks for until a release is being made.
///
/// WHAT THIS DOES NOT DO. It compares two declarations in this tree against each
/// other and neither of them is the live repository name: nothing offline can
/// read that. So this refuses a workflow that disagrees with the manifest the
/// plugin is published from, and a tree where both were changed to the same wrong
/// value passes. The manifest is the better of the two anchors because a
/// catalogue reads it and an operator's server fetches it, so a wrong name there
/// is a wrong name somebody meets.
///
/// It is a line reader and not a YAML parser, the same bound the manifest reader
/// in <c>PluginIdentityTests</c> carries: a key whose value is a plain scalar on
/// the same line. A block scalar or an anchor would defeat it, and both files are
/// written flat.
/// </remarks>
public sealed class ChangelogWorkflowTests
{
    // The name passed to the called workflow. Anchored on the key rather than on
    // the value, so a value that changed is read and compared rather than missed
    // and reported as an absent key.
    private static readonly Regex _repositoryName = new(
        @"(?m)^\s*repository-name:\s*""?(?<value>[^""\r\n]+?)""?\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The owner and name this plugin is published from, out of the manifest's
    // image URL. That field is the one machine-read place in this tree that names
    // the repository as a slug; the rest are in commands inside comments.
    private static readonly Regex _manifestSlug = new(
        @"(?m)^imageUrl:\s*""?https://raw\.githubusercontent\.com/(?<value>[^/""]+/[^/""]+)/",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly string _workflowPath = Path.Combine(".github", "workflows", "changelog.yaml");

    [Fact]
    public void The_changelog_workflow_hands_the_called_workflow_this_repository()
    {
        Assert.Equal(RepositoryTheManifestIsPublishedFrom(), RepositoryTheWorkflowNames());
    }

    [Fact]
    public void Each_reader_returns_an_owner_and_a_name_rather_than_half_of_one()
    {
        // The guard the called workflow applies compares against `owner/name`, so
        // a capture that had narrowed to one half of that would compare two
        // fragments that agree while the workflow hands over a name no repository
        // has. The leg above would redden too; this one says which half moved.
        Assert.Contains("/", RepositoryTheWorkflowNames(), StringComparison.Ordinal);
        Assert.Contains("/", RepositoryTheManifestIsPublishedFrom(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_reader_that_finds_nothing_refuses_rather_than_returning_an_empty_name()
    {
        // The near miss is a key renamed in the called workflow's interface, or a
        // manifest field rewritten. Without the refusal inside the reader, both
        // sides come back empty, two empty strings compare equal, and the leg
        // above reports agreement about a file it did not read.
        Assert.ThrowsAny<Exception>(() => NameIn(_repositoryName, "a file with no such key"));
        Assert.ThrowsAny<Exception>(() => NameIn(_manifestSlug, "a file with no such key"));
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // `.gitattributes` stores a line feed and lets the checkout decide, so one
        // clone reads these files with carriage returns and another does not. A
        // comparison that moved between the two would fail on Windows for a reason
        // that has nothing to do with either file.
        var asLineFeeds = Read(_workflowPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(NameIn(_repositoryName, asLineFeeds), NameIn(_repositoryName, asCarriageReturns));
    }

    private static string RepositoryTheWorkflowNames() =>
        NameIn(_repositoryName, Read(_workflowPath));

    private static string RepositoryTheManifestIsPublishedFrom() =>
        NameIn(_manifestSlug, Read("build.yaml"));

    private static string NameIn(Regex reader, string text)
    {
        var match = reader.Match(text);

        Assert.True(match.Success, "the reader found no name to compare");

        return match.Groups["value"].Value;
    }

    // The files a clone checked out, rather than a copy carried next to the test
    // assembly. What this is about is the bytes the runner is handed, and the
    // compiler is what knows where those are. Same reasoning as CommunityFilesTests.
    private static string Read(string relativePath)
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;
        var path = Path.Combine(root, relativePath);

        Assert.True(File.Exists(path), $"{relativePath} was not found, looked in {path}");

        return File.ReadAllText(path);
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
