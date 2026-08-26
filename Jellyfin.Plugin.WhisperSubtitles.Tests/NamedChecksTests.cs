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
/// A page that names a status check names a context something in this tree reports
/// under, and this refuses any other name.
/// </summary>
/// <remarks>
/// The failure this is written against had landed and stood. <c>docs/RELEASING.md</c>
/// listed the repository settings the publish route expects and asked for a check
/// called "ABI floor build". Nothing reports under that name: it is close to the title
/// of the workflow file, and the two jobs inside that file report under sentences of
/// their own. A ruleset entry carries the context a run reports under, so following
/// that page would have required a check that never arrives, which is not a slow gate
/// but a branch that stops merging. The neighbouring shape is the one #107 held from
/// the other end, and this is the same failure reached from the page rather than from
/// the setting.
///
/// The direction is the reason it is worth a check at all. A page naming a check that
/// does exist is corrected by whoever configures the ruleset within minutes, because
/// the entry either goes green or it does not. A page naming one that does not exist
/// is discovered while somebody is cutting a release, which is the worst moment this
/// repository has.
///
/// What makes the comparison possible with the machine offline is that a context is a
/// job name in a workflow file, so both sides of it are bytes a clone checked out. The
/// set is derived from <c>.github/workflows/</c> rather than written down here, so a
/// job renamed moves the pages that name it and this check together.
///
/// WHAT THIS DOES NOT DO, and the first bound is the largest. It reads what this tree
/// declares and never what the server reports, so a job this tree does not carry and a
/// job whose run has never been created are the same thing to it. Whether the ruleset
/// requires any of these names is a repository setting, every test here runs with the
/// machine offline, and #54 is where that comparison is owed.
///
/// A job that calls another workflow reports under a composed name, job then inner,
/// and the inner half is declared in a repository this tree cannot read. So a composed
/// name is accepted on its first half alone, and a page naming a real calling job and
/// an inner job that does not exist passes. That is a hole this reader cannot close
/// from here, and it is narrower than the hole of reading nothing.
///
/// It reads one shape, a quoted name followed by the word check, and it reads a name
/// on one line. A check named without backticks, or wrapped across a line break, is
/// invisible to it. Its subject is the markdown a reader of this repository meets: the
/// pages at the root and the pages under <c>docs/</c>. <c>.github/</c> is outside,
/// because the workflow files there are the thing being compared against and a comment
/// in one of them naming a job is a different question.
/// </remarks>
public class NamedChecksTests
{
    /// <summary>
    /// The separator a called workflow's context is composed with.
    /// </summary>
    private const string CalledSeparator = " / ";

    /// <summary>
    /// A status check named in prose: the context in backticks, on one line, then the
    /// word it is the name of.
    /// </summary>
    private static readonly Regex NamedInProse =
        new(@"`([^`\r\n]+)`\s+check", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// A job identifier, two spaces in, under the jobs key.
    /// </summary>
    private static readonly Regex JobHeader =
        new(@"^  ([A-Za-z0-9_-]+):\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The two keys of a job this reader cares about, four spaces in.
    /// </summary>
    private static readonly Regex JobKey =
        new(@"^    (name|uses):\s*(.*)$", RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_check_a_page_names_is_one_this_repository_reports()
    {
        var jobs = JobsThisTreeDeclares();
        var wrong = new List<string>();

        foreach (var page in PagesThisRepositoryPublishes())
        {
            var lines = File.ReadAllLines(page);

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in NamedInProse.Matches(lines[i]))
                {
                    var named = match.Groups[1].Value;

                    if (!IsReported(named, jobs))
                    {
                        wrong.Add(string.Create(
                            CultureInfo.InvariantCulture,
                            $"{Relative(page)}:{i + 1} names \"{named}\""));
                    }
                }
            }
        }

        Assert.True(
            wrong.Count == 0,
            $"a page names a check no job in this tree reports under: {string.Join(", ", wrong)}. A ruleset entry carries the context a run reports under, so a name nothing reports is a required check that never arrives and a branch that then stops merging. The names this tree reports under are {string.Join(", ", jobs.Select(job => job.Reported).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}.");
    }

    [Fact]
    public void The_scanner_can_see_the_pages_it_judges()
    {
        // Without this the leg above passes on a tree whose pages moved out of its
        // subject, and a reader that found no page at all would report that every
        // check named in this repository is one a run reports.
        var pages = PagesThisRepositoryPublishes();

        Assert.NotEmpty(pages);
        Assert.Contains(pages, page => Relative(page) == "docs/RELEASING.md");
    }

    [Fact]
    public void The_scanner_can_see_the_jobs_it_compares_against()
    {
        // The other side of the same vacuity. An empty job set makes every name wrong
        // rather than every name right, so it fails loudly instead of silently, but it
        // fails for a reason that has nothing to do with any page. The second leg is
        // the composed shape: without a calling job in the tree, the branch of the
        // comparison that accepts one is never taken by anything real.
        var jobs = JobsThisTreeDeclares();

        Assert.NotEmpty(jobs);
        Assert.Contains(jobs, job => job.CallsAnotherWorkflow);
    }

    [Fact]
    public void The_page_naming_a_job_this_tree_declares_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader that refused
        // everything would satisfy every refusal leg below and say nothing about the
        // real pages.
        Assert.Empty(NamesNothingReports(Fixture("clean")));
    }

    [Fact]
    public void The_reader_refuses_a_page_naming_a_check_no_job_reports()
    {
        // The defect this class exists for, in the shape it actually had: the title of
        // the workflow file where the job's own name belongs.
        Assert.Equal(
            new[] { "ABI floor build" },
            NamesNothingReports(Fixture("names-the-workflow-instead-of-the-job")));
    }

    [Fact]
    public void A_check_a_called_workflow_reports_is_accepted()
    {
        // The composed name. Two of the contexts this repository requires today are of
        // that shape, and a reader that knew only about job names declared here would
        // refuse a page that named them correctly.
        Assert.Empty(NamesNothingReports(Fixture("names-a-called-workflows-check")));
    }

    [Fact]
    public void A_composed_name_whose_first_half_calls_nothing_is_refused()
    {
        // The near miss beside it, and the reason a composed name is not accepted on
        // its shape alone. A job that runs steps of its own reports one context and
        // never one with a separator in it, so a page composing a name off such a job
        // describes a check that does not exist while looking exactly like the case
        // above.
        Assert.Equal(
            new[] { "Pull request hygiene / build" },
            NamesNothingReports(Fixture("composes-a-name-off-a-job-that-calls-nothing")));
    }

    [Fact]
    public void A_name_wrapped_across_a_line_break_is_not_read_as_a_check()
    {
        // A bound written as a case, so it is not discovered by somebody wondering why
        // their page passed. Widening the pattern across line breaks is not the repair:
        // it would take a backtick opened in one paragraph and closed in the next as a
        // check name, which is a refusal nobody can act on. Keeping the name on one
        // line is.
        Assert.Empty(NamesNothingReports(Fixture("wraps-the-name-across-a-line-break")));
    }

    [Fact]
    public void No_fixture_is_a_page_anything_else_reads()
    {
        // Every fixture here is a page that is deliberately wrong, and each one is kept
        // under an extension no reader of docs/ walks.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !path.EndsWith("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".md.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// The check names in a page that no job in this tree reports under.
    /// </summary>
    /// <param name="page">The page text.</param>
    /// <returns>The names, in the order the page carries them.</returns>
    private static List<string> NamesNothingReports(string page)
    {
        var jobs = JobsThisTreeDeclares();

        return NamedInProse.Matches(page)
            .Select(match => match.Groups[1].Value)
            .Where(named => !IsReported(named, jobs))
            .ToList();
    }

    private static bool IsReported(string named, IReadOnlyList<Job> jobs)
    {
        if (jobs.Any(job => string.Equals(job.Reported, named, StringComparison.Ordinal)))
        {
            return true;
        }

        var separator = named.IndexOf(CalledSeparator, StringComparison.Ordinal);

        if (separator < 0)
        {
            return false;
        }

        var caller = named[..separator];

        return jobs.Any(job =>
            job.CallsAnotherWorkflow && string.Equals(job.Reported, caller, StringComparison.Ordinal));
    }

    /// <summary>
    /// Every job this tree declares, read out of the workflow files rather than written
    /// down here.
    /// </summary>
    /// <remarks>
    /// A line reader and not a YAML parser, the same bound <c>ChangelogWorkflowTests</c>
    /// carries. It expects the jobs key at column zero, a job identifier two spaces in
    /// and that job's keys four spaces in, which is how every workflow in this tree is
    /// written. A job whose name is a block scalar, or a workflow written with another
    /// indentation, is read as having no name and answers to its identifier instead.
    /// </remarks>
    /// <returns>The jobs.</returns>
    private static List<Job> JobsThisTreeDeclares()
    {
        var jobs = new List<Job>();

        foreach (var file in WorkflowFiles())
        {
            var inJobs = false;
            string? identifier = null;
            string? name = null;
            var calls = false;

            void Close()
            {
                if (identifier is not null)
                {
                    jobs.Add(new Job(name ?? identifier, calls));
                }

                identifier = null;
                name = null;
                calls = false;
            }

            foreach (var line in File.ReadAllLines(file))
            {
                if (!inJobs)
                {
                    inJobs = line.TrimEnd() == "jobs:";
                    continue;
                }

                var header = JobHeader.Match(line);

                if (header.Success)
                {
                    Close();
                    identifier = header.Groups[1].Value;
                    continue;
                }

                if (identifier is null)
                {
                    continue;
                }

                var key = JobKey.Match(line);

                if (!key.Success)
                {
                    continue;
                }

                if (string.Equals(key.Groups[1].Value, "name", StringComparison.Ordinal))
                {
                    name = Unquote(key.Groups[2].Value.Trim());
                }
                else
                {
                    calls = true;
                }
            }

            Close();
        }

        return jobs;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
        {
            return value[1..^1];
        }

        return value;
    }

    /// <summary>
    /// The markdown a reader of this repository meets: the pages at the root and the
    /// pages under docs.
    /// </summary>
    /// <returns>The paths, ordered so a failure names them the same way twice.</returns>
    private static List<string> PagesThisRepositoryPublishes()
    {
        var root = RepositoryRoot();

        return Directory.GetFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

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
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "named-checks");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    /// <summary>
    /// A job as a page has to name it: the context it reports under, and whether that
    /// context is composed with a name declared where this tree cannot read it.
    /// </summary>
    /// <param name="Reported">The name a run of this job reports under.</param>
    /// <param name="CallsAnotherWorkflow">Whether the job hands its work to a workflow declared elsewhere.</param>
    private sealed record Job(string Reported, bool CallsAnotherWorkflow);
}
