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
/// The page that tells somebody how to add a backend names the surfaces outside
/// <c>Backends/</c> that a new backend has to be written into, and this refuses a
/// list that is not the set this suite actually holds.
/// </summary>
/// <remarks>
/// What forced this class is the sentence the page opened with. It said a third
/// backend could be added without the scheduled task, the output writer, the naming,
/// the language handling OR THE CONFIGURATION PAGE changing, and the last of those
/// had been false for as long as the page had said it: the configuration page carries
/// the names an operator may choose, and a class in this project compares that list
/// against the vocabulary the code answers to. So the page promised an author a file
/// they would not have to open, and a machine had been refusing that promise the
/// whole time.
///
/// It is the expensive direction of the two. An author who believes the page adds a
/// backend, runs the suite, and meets a red leg naming a file they had no reason to
/// read; the page is the only thing that told them the list was complete. The other
/// direction costs less and is still wrong: a page crediting a surface with a guard
/// that no longer judges the vocabulary hands an author a machine that is not there.
///
/// Both are refused here, and neither is refused by reading the surfaces. The guards
/// are derived from the SOURCE of this test project: a class that names the known set
/// of backend names is a class that judges some surface against that vocabulary, and
/// the page has to name it. That is the same reading <c>PagesReadOnEveryRunTests</c>
/// makes of its own population, for the same reason - it is visible in the tree
/// rather than in what a run happened to open.
///
/// WHAT THIS DOES NOT DO, and the bounds are worth having before the output is read
/// as a work-list. It does not judge the three PATHS the page prints beside its
/// guards. A path repeated in a test is a second copy to keep in step, and what a
/// guard's subject is belongs in that guard rather than here. It does not judge
/// whether a guard is any good, which is a reading and the review is where it is
/// caught. And it cannot see a surface that carries the vocabulary and has no guard
/// at all: nothing here enumerates the files a backend name is written into, so a
/// fourth surface added tomorrow with nothing judging it is invisible to this class
/// and to the page alike.
///
/// The one class the scan cannot see is this one, excluded by path. It names the set
/// in order to search for it, so counting itself would make the page owe a guard over
/// a page rather than over a surface. That exclusion is a line of code rather than a
/// convention, and it is the reason the page's list does not name this class.
///
/// Each leg carries a fixture it has to refuse, under
/// <c>Fixtures/backend-interface-page/</c>, judged against a fabricated pair of class
/// names rather than against this project, so no leg here can pass or fail because a
/// guard landed beside a real surface.
/// </remarks>
public class BackendInterfacePageTests
{
    /// <summary>
    /// The heading the guards are read from under. The reader stops at the next
    /// heading of the same level, so a class named elsewhere on the page is not a
    /// promise the list made.
    /// </summary>
    private const string NamedUnder = "## What a new backend changes outside its own folder";

    /// <summary>
    /// The member every guard of the backend vocabulary reads.
    /// </summary>
    /// <remarks>
    /// Written plainly rather than assembled out of pieces, so this file is a hit for
    /// its own search and the exclusion by path below is load-bearing rather than
    /// decorative. A leg asserts both halves of that: the exclusion holds, and this
    /// file is what it holds against.
    /// </remarks>
    private const string TheKnownSet = "BackendNames.Known";

    /// <summary>
    /// A wrapped bullet's continuation, which is a line break followed by the
    /// indentation the page wraps under. The list's bullets run to three lines on the
    /// real page, so each item is joined onto its own bullet before the reader below
    /// is asked, and the anchoring on a bullet is kept rather than traded for a flat
    /// scan of the section.
    /// </summary>
    private static readonly Regex Continuation = new(
        @"\r?\n[ \t]+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A guard named in the list: a backticked identifier on a bullet.
    /// </summary>
    private static readonly Regex NamedOnABullet = new(
        @"^[ \t]*[-*][^\r\n]*?`(?<name>[A-Za-z0-9_]+Tests)`",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The class a source file declares, which is the first one in it.
    /// </summary>
    private static readonly Regex Declared = new(
        @"^\s*(?:public\s+|internal\s+|sealed\s+|static\s+|partial\s+)*class\s+(?<name>[A-Za-z0-9_]+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The pair the fixtures are judged against. Neither name is a class in this
    /// project, so a fixture leg cannot be moved by a guard landing beside a surface.
    /// </summary>
    private static readonly string[] Fabricated = ["AlphaGuardTests", "BetaGuardTests"];

    [Fact]
    public void The_page_names_exactly_the_guards_this_suite_holds()
    {
        var disagreement = Disagreement(Page(), GuardsThisProjectHolds());

        Assert.True(
            disagreement.Count == 0,
            $"docs/backend-interface.md and this test project do not name the same guards over the backend vocabulary: {string.Join("; ", disagreement)}. That page is what somebody adding a backend reads instead of the suite, and a list short by one is a file they are never told to open.");
    }

    [Fact]
    public void The_census_finds_the_guards_this_project_holds()
    {
        // Without this the comparison above passes on a scan that stopped recognising
        // the shape it looks for, by finding nothing on that side and agreeing with a
        // page naming nothing either. The floor is what the tree held when this was
        // written.
        var held = GuardsThisProjectHolds();

        Assert.True(
            held.Count >= 3,
            $"this test project holds {held.Count} guard(s) over the backend vocabulary and this was written against three: {string.Join(", ", held)}. A shape that stopped being recognised leaves the comparison agreeing with nothing.");
    }

    [Fact]
    public void The_reader_finds_the_list_the_page_carries()
    {
        // The other side of the same vacuity, and the state the page was in: it spoke
        // of what a backend touches and named no guard anywhere.
        var named = GuardsThePageNames(Page());

        Assert.NotNull(named);
        Assert.NotEmpty(named);
    }

    [Fact]
    public void A_page_naming_exactly_the_guards_held_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader refusing every
        // pair would satisfy each refusal leg below and say nothing about the real
        // page.
        Assert.Empty(Disagreement(Fixture("clean"), Fabricated));
    }

    [Fact]
    public void A_page_that_leaves_a_guard_out_is_refused()
    {
        // The direction the real page was wrong in.
        Assert.Equal(
            ["this project holds BetaGuardTests over the backend vocabulary, and the page does not name it"],
            Disagreement(Fixture("leaves-a-guard-out"), Fabricated));
    }

    [Fact]
    public void A_page_naming_a_guard_nothing_holds_is_refused()
    {
        // The other direction. The page credits a surface with a machine it has not
        // got.
        Assert.Equal(
            ["the page names GammaGuardTests, and nothing in this test project judges the backend vocabulary in it"],
            Disagreement(Fixture("names-a-guard-nothing-holds"), Fabricated));
    }

    [Fact]
    public void A_page_that_speaks_of_the_guards_without_naming_one_is_refused()
    {
        // The shape the repair would have taken as a sentence. It reads as correct
        // against every population there could be, which is exactly why it says
        // nothing.
        Assert.Equal(
            [
                "this project holds AlphaGuardTests over the backend vocabulary, and the page does not name it",
                "this project holds BetaGuardTests over the backend vocabulary, and the page does not name it"
            ],
            Disagreement(Fixture("speaks-of-the-guards-without-naming-one"), Fabricated));
    }

    [Fact]
    public void A_page_carrying_no_such_section_is_refused()
    {
        // Separated from the empty list on purpose. A page that lost the section and a
        // page whose list emptied are different repairs, and a reader collapsing them
        // sends somebody to the wrong one.
        Assert.Equal(
            ["docs/backend-interface.md carries no section naming what a new backend changes outside its own folder"],
            Disagreement(Fixture("no-section-at-all"), Fabricated));
    }

    [Fact]
    public void The_reader_reads_its_own_section_and_not_a_name_beside_it()
    {
        // The near miss, and the reason the reader is bounded by a heading at all. The
        // clean fixture names a third class in a later section. A reader matching over
        // the whole page credits the list with a guard it never named.
        var fixture = Fixture("clean");

        Assert.Contains("GammaGuardTests", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("GammaGuardTests", GuardsThePageNames(fixture)!, StringComparer.Ordinal);
    }

    [Fact]
    public void The_scan_does_not_count_the_class_that_defines_it()
    {
        // This file names the set in order to search for it. Counted, it would make the
        // page owe a guard over a page rather than over a surface, and the list would
        // then name a class that reads no surface at all.
        Assert.DoesNotContain(
            nameof(BackendInterfacePageTests),
            GuardsThisProjectHolds(),
            StringComparer.Ordinal);

        Assert.Contains(
            TheKnownSet,
            File.ReadAllText(ThisFile()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void No_fixture_is_a_page_anything_else_reads()
    {
        // Every fixture here is a page that is deliberately wrong, and each is kept
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
    /// Everything the page and this project disagree about, or the reason the page
    /// could not be read.
    /// </summary>
    /// <param name="page">The page text.</param>
    /// <param name="held">The guards this project holds.</param>
    /// <returns>The complaints, ordered so a failure names them the same way twice.</returns>
    private static List<string> Disagreement(string page, IReadOnlyCollection<string> held)
    {
        var named = GuardsThePageNames(page);

        if (named is null)
        {
            return ["docs/backend-interface.md carries no section naming what a new backend changes outside its own folder"];
        }

        var complaints = new List<string>();

        foreach (var guard in held.Except(named, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            complaints.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"this project holds {guard} over the backend vocabulary, and the page does not name it"));
        }

        foreach (var guard in named.Except(held, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            complaints.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the page names {guard}, and nothing in this test project judges the backend vocabulary in it"));
        }

        return complaints;
    }

    /// <summary>
    /// The guards the page names on the bullets of its own section, or nothing where
    /// it carries no such section.
    /// </summary>
    /// <param name="page">The page text.</param>
    /// <returns>The class names, or null where the section is absent.</returns>
    private static List<string>? GuardsThePageNames(string page)
    {
        var start = page.IndexOf(NamedUnder, StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        var after = start + NamedUnder.Length;
        var end = page.IndexOf("\n## ", after, StringComparison.Ordinal);
        var section = end < 0 ? page[after..] : page[after..end];

        return NamedOnABullet.Matches(Continuation.Replace(section, " "))
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The classes in this test project that judge a surface against the backend
    /// vocabulary, which is the population the page's list claims to be.
    /// </summary>
    /// <returns>The class names, ordered.</returns>
    private static List<string> GuardsThisProjectHolds()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var source in Directory.EnumerateFiles(ProjectDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(source) || string.Equals(source, ThisFile(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(source);

            if (!text.Contains(TheKnownSet, StringComparison.Ordinal))
            {
                continue;
            }

            var declared = Declared.Match(text);

            if (declared.Success)
            {
                found.Add(declared.Groups["name"].Value);
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Whether a source file is something the build wrote rather than something
    /// somebody wrote.
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

    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "backend-interface.md"));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(ProjectDirectory(), "Fixtures", "backend-interface-page");

    private static string ProjectDirectory() =>
        Path.GetDirectoryName(ThisFile())!;

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(ProjectDirectory())!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
