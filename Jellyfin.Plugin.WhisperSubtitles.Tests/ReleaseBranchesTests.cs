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
/// The page that says how this repository has to be configured names every branch a
/// release may be cut from, and this refuses a page naming a different set from the one
/// the publish run reads.
/// </summary>
/// <remarks>
/// Two lists have to be the same set and only one of them is written where somebody
/// configuring the gate will look. <c>RELEASE_REFS</c> in
/// <c>.github/workflows/publish.yaml</c> is the list the publish run reads, and a tag on
/// a commit no branch in it contains is refused there. <c>docs/RELEASING.md</c> is the
/// page a person follows while cutting a release, and it asked for the gate on "the
/// release branches" without saying which those are.
///
/// The failure is what happens on the day a second entry is added to
/// <c>RELEASE_REFS</c>. The release route then allows a tag from a branch the gate was
/// never configured to cover, the page still reads correctly because it names no branch
/// at all, and nothing anywhere says the two have come apart. A gate covering less than
/// the release route allows is not a slow gate; it is a release cut from a branch
/// nothing checked.
///
/// So the repair is that the page carries the list rather than a phrase, and that the
/// two are compared. Adding a branch to the publish run turns this suite red until the
/// page names it, and the page is the thing that tells whoever holds the repository
/// settings which branches the gate has to cover.
///
/// WHAT THIS DOES NOT DO, and it is the larger half of the question. It compares two
/// files in this tree and never the ruleset. Whether the gate is actually required on
/// any branch is a repository setting, every test here runs with the machine offline,
/// and #318 is where that comparison is owed, the branch condition included. A page and a
/// workflow that agree with each other and disagree with the live ruleset pass every leg
/// below.
///
/// It reads one sentence shape on the page and the branch names inside it in backticks.
/// The sentence runs to the end of its own line, so a branch name is read wherever it
/// sits in the list and a name carrying a full stop, which every version branch does, is
/// read whole. What that costs is that anything else in backticks on that line is read
/// as a branch, which is why the sentence gets a line to itself. A list written another
/// way is invisible to the reader, and that is why a missing sentence is refused rather
/// than read as an empty list.
/// </remarks>
public class ReleaseBranchesTests
{
    /// <summary>
    /// The sentence the page carries the list in, on one line.
    /// </summary>
    private static readonly Regex NamedOnThePage =
        new(@"The release branches are ([^\r\n]+?)\.[ \t]*\r?$", RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    /// <summary>
    /// A branch name inside that sentence.
    /// </summary>
    private static readonly Regex Quoted =
        new(@"`([^`\r\n]+)`", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The list the publish run reads, space separated, on one line.
    /// </summary>
    private static readonly Regex CarriedByTheRun =
        new(@"^\s*RELEASE_REFS:\s*""([^""\r\n]*)""\s*$", RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_names_exactly_the_branches_a_release_may_be_cut_from()
    {
        var disagreement = Disagreement(
            File.ReadAllText(ReleasingPage()),
            File.ReadAllText(PublishWorkflow()));

        Assert.True(
            disagreement.Count == 0,
            $"docs/RELEASING.md and RELEASE_REFS in .github/workflows/publish.yaml do not name the same release branches: {string.Join(", ", disagreement)}. The page is what somebody configuring the gate reads, so a branch the publish run allows and the page does not name is a release cut from a branch the gate was never asked to cover.");
    }

    [Fact]
    public void The_reader_finds_the_release_branch_list_the_publish_run_reads()
    {
        // Without this the comparison above passes on a workflow whose list moved or was
        // renamed, by finding nothing on that side and agreeing with nothing.
        var carried = CarriedByTheRun.Match(File.ReadAllText(PublishWorkflow()));

        Assert.True(
            carried.Success,
            "RELEASE_REFS is not on one line of .github/workflows/publish.yaml in the shape this reads");
        Assert.NotEmpty(Split(carried.Groups[1].Value));
    }

    [Fact]
    public void The_reader_finds_the_sentence_the_page_carries()
    {
        // The other side of the same vacuity, and the state this class was written in:
        // the page spoke of the release branches and named none.
        var named = BranchesThePageNames(File.ReadAllText(ReleasingPage()));

        Assert.NotNull(named);
        Assert.NotEmpty(named);
    }

    [Fact]
    public void A_page_naming_exactly_the_branches_the_run_allows_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader refusing every pair
        // would satisfy each refusal leg below and say nothing about the real files.
        Assert.Empty(Disagreement(Page("clean"), Workflow("two-release-branches")));
    }

    [Fact]
    public void A_page_that_leaves_a_release_branch_out_is_refused()
    {
        // The direction that costs. The run allows a tag from a branch the page never
        // mentions, so the gate is configured on less than the release route permits while
        // the page reads as complete.
        Assert.Equal(
            ["the publish run allows a release from `releases/10.11`, and the page does not name it"],
            Disagreement(Page("leaves-a-release-branch-out"), Workflow("two-release-branches")));
    }

    [Fact]
    public void A_page_naming_a_branch_no_release_is_cut_from_is_refused()
    {
        // The other direction, which costs differently. It asks for the gate on a branch no
        // release comes from, and a required check on such a branch is how a set of checks
        // acquires an entry nobody can explain.
        Assert.Equal(
            ["the page names `releases/12.0`, and the publish run allows no release from it"],
            Disagreement(Page("names-a-branch-no-release-is-cut-from"), Workflow("one-release-branch")));
    }

    [Fact]
    public void A_page_that_speaks_of_release_branches_and_names_none_is_refused()
    {
        // The shape docs/RELEASING.md actually had. A phrase where the list belongs reads
        // correctly against every branch list, which is exactly why it says nothing.
        Assert.Equal(
            ["docs/RELEASING.md carries no sentence naming the release branches"],
            Disagreement(Page("speaks-of-the-branches-without-naming-one"), Workflow("one-release-branch")));
    }

    [Fact]
    public void A_run_carrying_no_branch_list_is_refused_rather_than_read_as_an_empty_one()
    {
        // A workflow whose list was renamed away is a side that knows nothing, and a page
        // compared against nothing agrees with it. This separates the two.
        Assert.Equal(
            [".github/workflows/publish.yaml carries no RELEASE_REFS this reads"],
            Disagreement(Page("clean"), Workflow("no-branch-list")));
    }

    [Fact]
    public void No_fixture_is_a_page_or_a_workflow_anything_else_reads()
    {
        // Every fixture here is a page or a workflow that is deliberately wrong, and each
        // is kept under an extension no reader of docs or of the workflows walks.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !path.EndsWith("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".md.fixture", StringComparison.Ordinal)
                    || path.EndsWith(".yaml.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// Everything the two lists disagree about, or the reason one side could not be read.
    /// </summary>
    /// <param name="page">The page text.</param>
    /// <param name="workflow">The publish workflow text.</param>
    /// <returns>The complaints, ordered so a failure names them the same way twice.</returns>
    private static List<string> Disagreement(string page, string workflow)
    {
        var carried = CarriedByTheRun.Match(workflow);

        if (!carried.Success)
        {
            return [".github/workflows/publish.yaml carries no RELEASE_REFS this reads"];
        }

        var named = BranchesThePageNames(page);

        if (named is null)
        {
            return ["docs/RELEASING.md carries no sentence naming the release branches"];
        }

        var allowed = Split(carried.Groups[1].Value);
        var complaints = new List<string>();

        foreach (var branch in allowed.Except(named, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            complaints.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the publish run allows a release from `{branch}`, and the page does not name it"));
        }

        foreach (var branch in named.Except(allowed, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            complaints.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the page names `{branch}`, and the publish run allows no release from it"));
        }

        return complaints;
    }

    /// <summary>
    /// The branches the page names, or nothing where it carries no such sentence.
    /// </summary>
    /// <param name="page">The page text.</param>
    /// <returns>The names, or null where the sentence is absent.</returns>
    private static List<string>? BranchesThePageNames(string page)
    {
        var sentence = NamedOnThePage.Match(page);

        if (!sentence.Success)
        {
            return null;
        }

        return Quoted.Matches(sentence.Groups[1].Value)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> Split(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string ReleasingPage() =>
        Path.Combine(RepositoryRoot(), "docs", "RELEASING.md");

    private static string PublishWorkflow() =>
        Path.Combine(RepositoryRoot(), ".github", "workflows", "publish.yaml");

    private static string Page(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".yaml.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "release-branches");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
