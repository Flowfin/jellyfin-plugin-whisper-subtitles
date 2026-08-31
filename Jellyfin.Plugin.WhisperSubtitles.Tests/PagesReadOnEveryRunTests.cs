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
/// The release checklist names the pages under <c>docs/</c> that are read against the
/// tree on every run, and this refuses a list that is not the set the suite actually
/// reads.
/// </summary>
/// <remarks>
/// The item this holds is the one that says the documentation matches what the build
/// produces. It decides nothing yet, and the argument it hands a releaser instead is a
/// population: some pages here are already compared against the tree rather than
/// believed, so the item is a choice between calling those readers the answer and
/// asking for a comparison against the published archive that nothing makes.
///
/// That argument was carried by a hand count and a list of four reader class names, and
/// the count was wrong by half. The suite read eight pages under <c>docs/</c> on the day
/// this landed. A releaser reading the item would have taken the weaker of the two
/// readings while believing it covered half of what it covers, and nothing anywhere said
/// the sentence had drifted, because nothing read it.
///
/// So the repair is the one the neighbouring lists here already took: the item names the
/// pages, and the two sets are compared. A page that gains a reader and a page that loses
/// one are both a red suite rather than a sentence going quietly out of date.
///
/// WHAT THIS DOES NOT DO, and the first bound is the largest. It derives "read on every
/// run" from a page name appearing in a source file of this test project, and never from
/// what a run actually opened. A class that names a page in a comment and reads nothing
/// counts here, and a class that builds a page name from parts does not. Both directions
/// are visible in the source rather than hidden, which is why this is the reading taken:
/// the alternative is a coverage run over a suite that has to be green before it can be
/// asked, and it would say nothing more about a page whose reader was deleted.
///
/// It says nothing about whether a reader is any good. Whether the comparison a class
/// makes is worth making, and whether it would catch the drift its page is at risk of,
/// is a reading and the review is where it is caught.
///
/// It is not the clause of #62 that asks a release to refuse to publish while an item is
/// unanswered. Nothing here reaches the publish run, and the closing section of the page
/// says so in the page's own words.
///
/// Each leg carries a fixture it has to refuse, under
/// <c>Fixtures/pages-read-on-every-run/</c>, judged against a fabricated pair of page
/// names rather than against this tree, so no leg here can pass or fail because a file
/// landed under <c>docs/</c>.
/// </remarks>
public class PagesReadOnEveryRunTests
{
    /// <summary>
    /// The paragraph the checklist carries the list in. It runs to the end of its own
    /// paragraph rather than to a full stop, because every name in it carries one.
    /// </summary>
    private static readonly Regex NamedOnThePage = new(
        @"^The pages read against the tree on every run rather than trusted are(?<list>.*?)\r?\n\r?\n",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A page name inside that paragraph.
    /// </summary>
    private static readonly Regex Quoted = new(
        @"`([^`\r\n]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A page under <c>docs/</c> named in a source file, in either of the two shapes this
    /// project writes: the relative path as one string, and the directory and the file as
    /// two arguments of a path join. The second is written across several lines in most
    /// of the classes that use it, which is why the text is flattened before this runs.
    /// </summary>
    private static readonly Regex NamedInSource = new(
        @"""docs/(?<name>[A-Za-z0-9._-]+\.md)""|""docs""\s*,\s*""(?<name>[A-Za-z0-9._-]+\.md)""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex Whitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The pair the fixtures are judged against. Neither name is a file in this tree, so
    /// a fixture leg cannot be moved by anything landing under <c>docs/</c>.
    /// </summary>
    private static readonly string[] Fabricated = ["docs/alpha.md", "docs/beta.md"];

    [Fact]
    public void The_checklist_names_exactly_the_pages_this_suite_reads()
    {
        var disagreement = Disagreement(Checklist(), PagesTheSuiteReads());

        Assert.True(
            disagreement.Count == 0,
            $"docs/release-checklist.md and this test project do not name the same pages as read on every run: {string.Join("; ", disagreement)}. The item that says the documentation matches what the build produces rests on that population, and a releaser reading it takes the weaker of two readings on the strength of it.");
    }

    [Fact]
    public void The_census_finds_the_pages_this_suite_reads()
    {
        // Without this the comparison above passes on a scan that stopped recognising the
        // shapes it looks for, by finding nothing on that side and agreeing with a
        // checklist that names nothing either. The floor is what the tree held when this
        // was written.
        var read = PagesTheSuiteReads();

        Assert.True(
            read.Count >= 6,
            $"this test project names {read.Count} page(s) under docs/ and this was written against eight: {string.Join(", ", read)}. A shape that stopped being recognised leaves the comparison agreeing with nothing.");
    }

    [Fact]
    public void The_reader_finds_the_paragraph_the_checklist_carries()
    {
        // The other side of the same vacuity, and the state this class was written in: the
        // page counted its readers and named a page nowhere.
        var named = PagesTheChecklistNames(Checklist());

        Assert.NotNull(named);
        Assert.NotEmpty(named);
    }

    [Fact]
    public void A_checklist_naming_exactly_the_pages_read_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader refusing every pair
        // would satisfy each refusal leg below and say nothing about the real files.
        Assert.Empty(Disagreement(Fixture("clean"), Fabricated));
    }

    [Fact]
    public void A_checklist_that_leaves_a_read_page_out_is_refused()
    {
        // The direction that costs, and the one the real page was wrong in. A reader whose
        // page the checklist never names can be deleted without the checklist noticing,
        // and the item goes on resting on a population that has shrunk.
        Assert.Equal(
            ["the suite reads docs/beta.md, and the checklist does not name it"],
            Disagreement(Fixture("leaves-a-page-out"), Fabricated));
    }

    [Fact]
    public void A_checklist_naming_a_page_nothing_reads_is_refused()
    {
        // The other direction. The checklist credits a page with a guard it has not got,
        // and a releaser deciding this item is answered by the readers is deciding it on a
        // page nothing compares.
        Assert.Equal(
            ["the checklist names docs/gamma.md, and nothing in this test project reads it"],
            Disagreement(Fixture("names-a-page-nothing-reads"), Fabricated));
    }

    [Fact]
    public void A_checklist_that_counts_its_readers_instead_of_naming_them_is_refused()
    {
        // The shape the item actually had, word for word. A count with a list of reader
        // class names reads as correct against every population there could be, which is
        // exactly why it says nothing.
        Assert.Equal(
            ["docs/release-checklist.md carries no paragraph naming the pages read on every run"],
            Disagreement(Fixture("speaks-of-the-readers-without-naming-one"), Fabricated));
    }

    [Fact]
    public void No_fixture_is_a_page_anything_else_reads()
    {
        // Every fixture here is a checklist that is deliberately wrong, and each is kept
        // under an extension no reader of docs walks.
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
    /// Everything the checklist and the suite disagree about, or the reason the checklist
    /// could not be read.
    /// </summary>
    /// <param name="checklist">The checklist text.</param>
    /// <param name="read">The pages the suite reads, as repository-relative paths.</param>
    /// <returns>The complaints, ordered so a failure names them the same way twice.</returns>
    private static List<string> Disagreement(string checklist, IReadOnlyCollection<string> read)
    {
        var named = PagesTheChecklistNames(checklist);

        if (named is null)
        {
            return ["docs/release-checklist.md carries no paragraph naming the pages read on every run"];
        }

        var complaints = new List<string>();

        foreach (var page in read.Except(named, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            complaints.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the suite reads {page}, and the checklist does not name it"));
        }

        foreach (var page in named.Except(read, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            complaints.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the checklist names {page}, and nothing in this test project reads it"));
        }

        return complaints;
    }

    /// <summary>
    /// The pages the checklist names, or nothing where it carries no such paragraph.
    /// </summary>
    /// <param name="checklist">The checklist text.</param>
    /// <returns>The names, or null where the paragraph is absent.</returns>
    private static List<string>? PagesTheChecklistNames(string checklist)
    {
        var paragraph = NamedOnThePage.Match(checklist);

        if (!paragraph.Success)
        {
            return null;
        }

        return Quoted.Matches(paragraph.Groups["list"].Value)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The pages under <c>docs/</c> this test project names, which is the population the
    /// checklist's item calls read on every run.
    /// </summary>
    /// <remarks>
    /// A name counts only where a file of that name is under <c>docs/</c>, which is what
    /// keeps a fixture page name written in a source file out of the census.
    /// </remarks>
    /// <returns>The repository-relative paths, ordered.</returns>
    private static List<string> PagesTheSuiteReads()
    {
        var documents = Path.Combine(RepositoryRoot(), "docs");
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var source in Directory.EnumerateFiles(ProjectDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(source))
            {
                continue;
            }

            var flattened = Whitespace.Replace(File.ReadAllText(source), " ");

            foreach (Match named in NamedInSource.Matches(flattened))
            {
                var name = named.Groups["name"].Value;

                if (File.Exists(Path.Combine(documents, name)))
                {
                    found.Add("docs/" + name);
                }
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Whether a source file is something the build wrote rather than something somebody
    /// wrote. Generated assembly attributes are not a reader of anything.
    /// </summary>
    /// <param name="path">The file's full path.</param>
    /// <returns>Whether it sits under an output directory.</returns>
    private static bool IsBuildOutput(string path)
    {
        var relative = Path.GetRelativePath(ProjectDirectory(), path);

        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            segment.Equals("obj", StringComparison.Ordinal) || segment.Equals("bin", StringComparison.Ordinal));
    }

    private static string Checklist() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "release-checklist.md"));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(ProjectDirectory(), "Fixtures", "pages-read-on-every-run");

    private static string ProjectDirectory() =>
        Path.GetDirectoryName(ThisFile())!;

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(ProjectDirectory())!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
