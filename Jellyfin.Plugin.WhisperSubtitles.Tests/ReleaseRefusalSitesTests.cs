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
/// The release page says how many places the publish run refuses in, and this refuses a
/// page whose number is not the number of them.
/// </summary>
/// <remarks>
/// <c>docs/RELEASING.md</c> is the page a person follows while cutting a release, and its
/// "What fails the run" section is what tells them which conditions stop a tag. That list
/// is prose written by hand beside a workflow that grows, and the page's own disclosure
/// used to end there: a refusal added to the run reached the page only if somebody wrote
/// it there as well, and three entries had already arrived that way.
///
/// The failure is a releaser meeting a red run for a reason the page never named, on the
/// one route where the reflex is to retry and where a second attempt can burn a tag
/// permanently.
///
/// Matching a bullet to the refusal it describes is what this does NOT do, and it is not
/// an omission: the messages the run prints carry no identifier a page could name, and
/// giving them one is a change to the release route rather than to the page. What is
/// comparable without such an anchor is HOW MANY refusals there are, and that is the
/// whole subject here. A refusal added to the run moves the count and reddens this until
/// somebody comes back to the list; a refusal deleted from it does the same.
///
/// So the bound is the size of what this buys, and it is worth reading before this is
/// trusted. It says a refusal ARRIVED, never that the page describes it. A message
/// rewritten in place, a bullet saying the wrong thing about the site it is about, and a
/// bullet describing a refusal that has gone all leave the count where it was.
///
/// The count is the number of refusal MESSAGES rather than of exits. Two of the sites set
/// a flag that is tested afterwards rather than exiting where they print, so a reading
/// keyed on the exit would find fewer sites than the run has reasons to fail for, which
/// is the direction that quietly under-counts.
///
/// Each leg carries a fixture it has to refuse, under <c>Fixtures/release-refusals/</c>,
/// so the proof it bites is in the tree rather than in the memory of whoever last broke
/// the page on purpose, and the pair that breaks no rule has to stay accepted or a reader
/// refusing everything would satisfy every refusal leg.
/// </remarks>
public class ReleaseRefusalSitesTests
{
    /// <summary>
    /// The paste on the page: the command, then the number it returned.
    /// </summary>
    /// <remarks>
    /// Anchored on the command rather than on a sentence, so the number this reads is the
    /// one the page offers as the output of that command and never a figure written
    /// elsewhere in prose. A page carrying the command and no output is a page stating no
    /// number, which is refused rather than read as nought.
    /// </remarks>
    private static readonly Regex PastedOnThePage = new(
        @"grep -c '::error::' \.github/workflows/publish\.yaml[ \t]*\r?\n[ \t]*(?<count>[0-9]+)[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A refusal message the run prints.
    /// </summary>
    private static readonly Regex RefusalInTheRun = new(
        @"::error::",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_states_the_number_of_places_the_publish_run_refuses_in()
    {
        var disagreement = Disagreement(
            File.ReadAllText(ReleasingPage()),
            File.ReadAllText(PublishWorkflow()));

        Assert.True(disagreement.Count == 0, string.Join("; ", disagreement));
    }

    [Fact]
    public void The_reader_finds_the_paste_on_the_page()
    {
        // Without this the comparison above passes on a page whose paste was reworded
        // away, by finding nothing on that side and refusing nothing.
        Assert.NotNull(NumberThePageStates(File.ReadAllText(ReleasingPage())));
    }

    [Fact]
    public void The_reader_finds_the_refusals_in_the_publish_run()
    {
        // The other side of the same guard. A workflow whose messages moved out of this
        // reader's sight is a side that knows nothing, and a page compared against nothing
        // would be compared against nought.
        Assert.True(RefusalsInTheRun(File.ReadAllText(PublishWorkflow())) > 0);
    }

    [Fact]
    public void A_page_stating_the_number_the_run_refuses_in_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader refusing every pair
        // would satisfy each refusal leg below and say nothing about the real files.
        Assert.Empty(Disagreement(Page("clean"), Workflow("two-refusals")));
    }

    [Fact]
    public void A_page_stating_a_number_the_run_does_not_have_is_refused()
    {
        // The failure this exists against, in the direction it arrives in: the run grew a
        // refusal and the page went on stating the number it was right about.
        Assert.Equal(
            ["docs/RELEASING.md says the publish run refuses in 3 places, and it refuses in 2"],
            Disagreement(Page("states-a-number-the-run-does-not-have"), Workflow("two-refusals")));
    }

    [Fact]
    public void A_page_stating_no_number_is_refused_rather_than_read_as_agreeing()
    {
        // The shape the page had before this landed: a disclosure where a comparison
        // belongs reads correctly against every workflow, which is why it says nothing.
        Assert.Equal(
            ["docs/RELEASING.md carries no count of the places the publish run refuses in"],
            Disagreement(Page("states-no-number"), Workflow("two-refusals")));
    }

    [Fact]
    public void A_run_carrying_no_refusal_is_refused_rather_than_read_as_none()
    {
        Assert.Equal(
            [".github/workflows/publish.yaml carries no refusal this reads"],
            Disagreement(Page("clean"), Workflow("no-refusal")));
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
    /// What the page and the run disagree about, in the words a failure names them in.
    /// </summary>
    /// <param name="page">The release page text.</param>
    /// <param name="workflow">The publish workflow text.</param>
    /// <returns>The disagreements, empty where there are none.</returns>
    private static List<string> Disagreement(string page, string workflow)
    {
        var refusals = RefusalsInTheRun(workflow);

        if (refusals == 0)
        {
            return [".github/workflows/publish.yaml carries no refusal this reads"];
        }

        var stated = NumberThePageStates(page);

        if (stated is null)
        {
            return ["docs/RELEASING.md carries no count of the places the publish run refuses in"];
        }

        return stated == refusals
            ? []
            : [string.Create(
                CultureInfo.InvariantCulture,
                $"docs/RELEASING.md says the publish run refuses in {stated} places, and it refuses in {refusals}")];
    }

    private static int? NumberThePageStates(string page)
    {
        var pasted = PastedOnThePage.Match(page);

        return pasted.Success
            ? int.Parse(pasted.Groups["count"].Value, CultureInfo.InvariantCulture)
            : null;
    }

    private static int RefusalsInTheRun(string workflow) => RefusalInTheRun.Count(workflow);

    private static string Page(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".yaml.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "release-refusals");

    private static string ReleasingPage() =>
        Path.Combine(RepositoryRoot(), "docs", "RELEASING.md");

    private static string PublishWorkflow() =>
        Path.Combine(RepositoryRoot(), ".github", "workflows", "publish.yaml");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
