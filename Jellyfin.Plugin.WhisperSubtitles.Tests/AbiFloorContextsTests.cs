using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The page that says how this repository has to be configured names every context the
/// ABI floor workflow reports, and this refuses a page naming a different set from the
/// jobs that workflow declares.
/// </summary>
/// <remarks>
/// A ruleset entry names the context a run reports under rather than the workflow's
/// title, so the set somebody has to type into the required checks is the set of job
/// names. <c>docs/RELEASING.md</c> is where that set is written for them, and it was
/// written by hand: a count and two names, beside a workflow that can grow a third job
/// without anything saying so.
///
/// The failure runs in the expensive direction. A job added to the ABI floor workflow
/// and not named on the page is a check that runs on every pull request and is required
/// on none, so the branch a release is cut from is protected by less than the tree
/// actually checks, and the page reads as complete because it names everything it knows
/// about. #54 is the issue that asks for the full set to be required, and a page listing
/// less than the full set is that issue defeated at the step before the setting.
///
/// The other direction costs differently and is refused too. A context named here that
/// no job reports is a required check that never arrives, which is a branch that stops
/// merging for a name nobody can explain.
///
/// WHAT THIS DOES NOT DO. It compares two files in this tree and never the ruleset.
/// Whether either context is actually required on any branch is a repository setting,
/// every test here runs with the machine offline, and that comparison is what #54 is
/// open on. A page and a workflow that agree with each other and disagree with the live
/// ruleset pass every leg below.
///
/// It reads one workflow, the one whose contexts this bullet is about, and says nothing
/// about the other checks this repository reports. <see cref="NamedChecksTests"/> holds
/// the other direction over every page and every workflow: that a check a page NAMES is
/// one this repository reports. Neither of the two is the other, and it is this one that
/// notices a job nobody wrote down.
///
/// The list is read from a sentence that runs to the blank line after it, so the names
/// are read whatever line each one falls on, and anything else in backticks inside that
/// paragraph is read as a context. That is why the paragraph holds the list and nothing
/// else, and why a missing sentence is refused rather than read as an empty list.
/// </remarks>
public class AbiFloorContextsTests
{
    /// <summary>
    /// The paragraph the page carries the list in: the sentence, then everything up to
    /// the blank line that ends it.
    /// </summary>
    private static readonly Regex NamedOnThePage = new(
        @"The contexts the ABI floor workflow reports are(?<list>.*?)\r?\n[ \t]*\r?\n",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A context name inside that paragraph.
    /// </summary>
    private static readonly Regex Quoted = new(
        @"`([^`\r\n]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A job's own name in a workflow, which is the context a run reports under.
    /// </summary>
    /// <remarks>
    /// At the job's indentation and no deeper, so a step's name is not read as a
    /// context. A job declaring no name reports under its key instead, and this reads
    /// none for it: the page would then be missing a context this cannot see, which is
    /// why the workflow is required to declare one per job below.
    /// </remarks>
    private static readonly Regex DeclaredByTheWorkflow = new(
        @"^ {4}name:[ \t]*(?<name>[^\r\n]+?)[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A job key in a workflow, which is what a job that declares no name reports under.
    /// </summary>
    private static readonly Regex JobInTheWorkflow = new(
        @"^ {2}(?<key>[A-Za-z0-9_-]+):[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Where the jobs start. Everything above it is triggers and settings written at
    /// the same indentation, and counting keys over the whole file would count those
    /// too.
    /// </summary>
    private static readonly Regex JobsStart = new(
        @"^jobs:[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_names_exactly_the_contexts_the_abi_floor_workflow_reports()
    {
        var disagreement = Disagreement(
            File.ReadAllText(ReleasingPage()),
            File.ReadAllText(AbiFloorWorkflow()));

        Assert.True(disagreement.Count == 0, string.Join("; ", disagreement));
    }

    [Fact]
    public void The_reader_finds_the_list_on_the_page()
    {
        // Without this the comparison above passes on a page whose paragraph was
        // reworded away, by finding nothing on that side and agreeing with nothing.
        var named = ContextsThePageNames(File.ReadAllText(ReleasingPage()));

        Assert.NotNull(named);
        Assert.NotEmpty(named);
    }

    [Fact]
    public void Every_job_the_abi_floor_workflow_declares_carries_a_name()
    {
        // The bound this reader depends on. A job with no name of its own reports under
        // its key, which this does not read, so the comparison would be blind to exactly
        // the job nobody wrote down.
        var jobs = JobsBlock(File.ReadAllText(AbiFloorWorkflow()));

        Assert.NotEmpty(jobs);
        Assert.Equal(
            JobInTheWorkflow.Count(jobs),
            DeclaredByTheWorkflow.Count(jobs));
    }

    [Fact]
    public void A_page_naming_exactly_the_contexts_the_workflow_reports_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader refusing every
        // pair would satisfy each refusal leg below and say nothing about the real files.
        Assert.Empty(Disagreement(Page("clean"), Workflow("two-jobs")));
    }

    [Fact]
    public void A_page_that_leaves_a_reported_context_out_is_refused()
    {
        // The direction that costs. The workflow grew a job, the page still names
        // everything it knows about, and the gate is configured on less than this tree
        // checks.
        Assert.Equal(
            ["the ABI floor workflow reports `And it refuses a floor no manifest declares`, and the page does not name it"],
            Disagreement(Page("clean"), Workflow("three-jobs")));
    }

    [Fact]
    public void A_page_naming_a_context_no_job_reports_is_refused()
    {
        // The other direction. A required check that never arrives is a branch that stops
        // merging for a name nobody can explain.
        Assert.Equal(
            ["the page names `A context this workflow does not report`, and the ABI floor workflow reports no such job"],
            Disagreement(Page("names-a-context-no-job-reports"), Workflow("two-jobs")));
    }

    [Fact]
    public void A_page_that_speaks_of_the_contexts_without_naming_them_is_refused()
    {
        // The shape the bullet had. A count and a phrase read as correct against a
        // workflow of any size, which is exactly why they say nothing.
        Assert.Equal(
            ["docs/RELEASING.md carries no paragraph naming the contexts the ABI floor workflow reports"],
            Disagreement(Page("speaks-of-the-contexts-without-naming-them"), Workflow("two-jobs")));
    }

    [Fact]
    public void A_workflow_declaring_no_job_is_refused_rather_than_read_as_reporting_none()
    {
        // A workflow this reader cannot see the jobs of is a side that knows nothing, and
        // a page compared against nothing would have every name of its own refused.
        Assert.Equal(
            [".github/workflows/abi-floor.yml declares no job this reads"],
            Disagreement(Page("clean"), Workflow("no-job")));
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
                    || path.EndsWith(".yml.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// What the page and the workflow disagree about, in the words a failure names them
    /// in.
    /// </summary>
    /// <param name="page">The release page text.</param>
    /// <param name="workflow">The ABI floor workflow text.</param>
    /// <returns>The disagreements, ordered so a failure names them the same way twice.</returns>
    private static List<string> Disagreement(string page, string workflow)
    {
        var reported = ContextsTheWorkflowReports(workflow);

        if (reported.Count == 0)
        {
            return [".github/workflows/abi-floor.yml declares no job this reads"];
        }

        var named = ContextsThePageNames(page);

        if (named is null || named.Count == 0)
        {
            return ["docs/RELEASING.md carries no paragraph naming the contexts the ABI floor workflow reports"];
        }

        return reported
            .Where(context => !named.Contains(context))
            .Select(context => $"the ABI floor workflow reports `{context}`, and the page does not name it")
            .Concat(named
                .Where(context => !reported.Contains(context))
                .Select(context => $"the page names `{context}`, and the ABI floor workflow reports no such job"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static HashSet<string>? ContextsThePageNames(string page)
    {
        var paragraph = NamedOnThePage.Match(page);

        return paragraph.Success
            ? Quoted.Matches(paragraph.Groups["list"].Value).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal)
            : null;
    }

    private static HashSet<string> ContextsTheWorkflowReports(string workflow) =>
        DeclaredByTheWorkflow.Matches(JobsBlock(workflow))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The part of a workflow that declares jobs, or nothing where it declares none.
    /// </summary>
    /// <param name="workflow">The workflow text.</param>
    /// <returns>The text from the jobs key onwards, empty where there is none.</returns>
    private static string JobsBlock(string workflow)
    {
        var start = JobsStart.Match(workflow);

        return start.Success ? workflow[(start.Index + start.Length)..] : string.Empty;
    }

    private static string Page(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".yml.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "abi-floor-contexts");

    private static string ReleasingPage() =>
        Path.Combine(RepositoryRoot(), "docs", "RELEASING.md");

    private static string AbiFloorWorkflow() =>
        Path.Combine(RepositoryRoot(), ".github", "workflows", "abi-floor.yml");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
