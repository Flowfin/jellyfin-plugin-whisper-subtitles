using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The contributor guide lists the tests this repository refuses to write and names
/// what stands in for each one. A list like that is only worth reading while it is
/// true, and nothing made it true.
/// </summary>
/// <remarks>
/// The failure it is written against is quiet. A replacement gets renamed, or is
/// never written, and the line goes on naming it. The next contributor reads the
/// line, believes the coverage is somewhere, and does not write the test. Nothing
/// goes red, because a sentence in a document is not something a run reads.
///
/// So the endings of those lines are a small vocabulary rather than prose: `Replaced
/// by` and a class in backticks, or `Owed by` and the issues that have to land
/// first. Every name of the first kind is resolved against the classes THIS assembly
/// actually runs, which is the half the guide could not do for itself.
///
/// WHAT THIS DOES NOT DO, in two directions, and neither is an oversight.
///
/// It does not judge whether a replacement covers what the refused test would have
/// covered. That is a reading of two things this cannot compare, and the review is
/// where a wrong answer is caught.
///
/// It does not notice an ending that has gone stale. Whether an owed issue has
/// landed is an answer on the tracker, and this suite reaches no network by the rule
/// its neighbour enforces, so an issue that closed yesterday leaves this green until
/// a person moves the line. That is the weaker of the two directions, and it is
/// stated here rather than discovered later.
///
/// Each leg carries a fixture it has to refuse, under <c>Fixtures/refused-tests/</c>,
/// so the proof it bites is in the tree rather than in the memory of whoever last
/// broke the guide on purpose. One fixture per leg, because a leg is proven by a case
/// that trips it AND NO OTHER, and the neighbour that breaks nothing has to stay
/// accepted or a reader refusing every section would pass every leg.
/// </remarks>
public class RefusedTestsTests
{
    private const string Heading = "## Tests that are refused, and what replaces each one";

    private static readonly Regex _replacement = new(@"Replaced by ((?:`[A-Za-z0-9_]+`(?:, )?)+)", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static readonly Regex _owed = new(@"Owed by ((?:#[0-9]+(?:, )?)+)", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static readonly Regex _backticked = new(@"`([A-Za-z0-9_]+)`", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static readonly Regex _issue = new(@"#([0-9]+)", RegexOptions.None, TimeSpan.FromSeconds(5));

    public static TheoryData<string> EveryEntry =>
        new(Entries(Section(Guide())).ToArray());

    [Fact]
    public void The_reader_finds_the_section_and_more_than_one_entry_in_it()
    {
        // Guards every leg below. A reader that matched no section, or matched one
        // and found no lines in it, would report a guide whose every claim resolves,
        // whatever the guide said, and it would do it in green.
        var entries = Entries(Section(Guide()));

        Assert.True(entries.Count > 1, $"the reader found {entries.Count} entries under {Heading}");
        Assert.Contains(entries, entry => entry.Contains("configuration page", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Contains("wall clock", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_stops_at_the_next_section_rather_than_running_to_the_end_of_the_file()
    {
        // Without this the section is the whole rest of the guide, every later
        // bulleted line becomes an entry, and the legs below start judging lines
        // nobody wrote endings for.
        var section = Section(Guide());

        Assert.Contains("A test that requires a GPU", section, StringComparison.Ordinal);
        Assert.DoesNotContain("## Adding a backend", section, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_entry_names_a_replacement_or_the_issue_that_owes_one(string entry)
    {
        Assert.True(
            Replacements(entry).Count > 0 || Owed(entry).Count > 0,
            $"this entry ends with neither a replacement nor an owing issue: {entry}");
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_named_replacement_is_a_class_this_suite_runs(string entry)
    {
        // The clause the issue asked for. A name here is compared against the
        // classes the assembly actually carries tests in, so a replacement that was
        // renamed, moved into another project or never written turns this red
        // instead of going on standing in for something on paper.
        var running = ClassesThisSuiteRunsTestsIn();

        foreach (var named in Replacements(entry))
        {
            Assert.True(
                running.Contains(named),
                $"{named} stands in for a refused test and this assembly runs no tests in a class by that name");
        }
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_owed_issue_is_a_number_a_reader_can_open(string entry)
    {
        foreach (var owed in Owed(entry))
        {
            Assert.True(owed > 0, $"an entry owes issue {owed}, which is not a number on the tracker: {entry}");
        }
    }

    [Fact]
    public void At_least_one_entry_is_carried_by_a_class_rather_than_by_an_issue()
    {
        // The other half of guarding the resolution leg. If every entry were owed,
        // that leg would iterate over nothing and pass without ever resolving a
        // single name, and the day the last replacement was deleted would look
        // exactly like today.
        var entries = Entries(Section(Guide()));

        Assert.Contains(entries, entry => Replacements(entry).Count > 0);
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_names_a_class_the_suite_does_not_have()
    {
        var entry = Assert.Single(Entries(Section(Fixture("names-a-class-that-is-not-there"))));

        Assert.NotEmpty(Replacements(entry));
        Assert.DoesNotContain(Replacements(entry), named => ClassesThisSuiteRunsTestsIn().Contains(named));
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_names_nothing_at_all()
    {
        var entry = Assert.Single(Entries(Section(Fixture("names-nothing-at-all"))));

        Assert.Empty(Replacements(entry));
        Assert.Empty(Owed(entry));
    }

    [Fact]
    public void The_reader_refuses_a_section_with_no_entries_in_it()
    {
        // The fixture for the guard rather than for a rule: a section whose lines
        // stopped being lines the reader recognises reads as a guide with nothing
        // in it, which is the shape that passes every other leg for free.
        Assert.Empty(Entries(Section(Fixture("no-entries-at-all"))));
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        // Without this a reader that refused every section would pass every leg
        // above.
        var entries = Entries(Section(Fixture("clean")));

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => Replacements(entry).Contains(nameof(RefusedTestsTests)));
        Assert.Contains(entries, entry => Owed(entry).Contains(46));
        Assert.All(entries, entry => Assert.True(Replacements(entry).Count > 0 || Owed(entry).Count > 0));
    }

    [Fact]
    public void No_fixture_is_a_document_anything_else_reads()
    {
        // The extension is the whole of what keeps these out of the way of a
        // documentation check that walks the tree for markdown, and a fixture that
        // acquired a plain one would be a second guide saying things about this
        // repository that are deliberately untrue. The README beside them is the
        // one document in that directory that is true, so it is named rather than
        // matched by a pattern that would also let a fixture through.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, path => Assert.EndsWith(".md.fixture", path, StringComparison.Ordinal));
    }

    private static List<string> Replacements(string entry) =>
        Names(_replacement, _backticked, entry).ToList();

    private static List<int> Owed(string entry) =>
        Names(_owed, _issue, entry)
            .Select(number => int.Parse(number, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

    private static IEnumerable<string> Names(Regex ending, Regex item, string entry) =>
        ending.Matches(entry)
            .SelectMany(match => item.Matches(match.Groups[1].Value).Select(inner => inner.Groups[1].Value));

    /// <summary>
    /// The classes this assembly would run a test in, by name.
    /// </summary>
    /// <remarks>
    /// From the loaded assembly rather than from the source files beside it, because
    /// what the guide claims is that the coverage RUNS, and a class excluded from
    /// the compilation still has a file. TheoryAttribute derives from FactAttribute,
    /// so both shapes are found by asking for the one.
    /// </remarks>
    private static HashSet<string> ClassesThisSuiteRunsTestsIn() =>
        typeof(RefusedTestsTests).Assembly
            .GetTypes()
            .Where(type => type.GetMethods().Any(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The bulleted entries of a section, each one joined back into a single line.
    /// </summary>
    /// <remarks>
    /// An entry wraps across several lines in the source, and every ending this reads
    /// sits at the end of one, so an entry read line by line would lose the ending
    /// exactly when the line before it was long enough to wrap.
    /// </remarks>
    private static List<string> Entries(string section)
    {
        var entries = new List<string>();

        foreach (var line in section.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                entries.Add(line[2..].Trim());
            }
            else if (entries.Count == 0)
            {
                // The paragraph that introduces the list. Skipped rather than read,
                // because it is prose about the list and not a line of it.
                continue;
            }
            else if (line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                entries[^1] = entries[^1] + " " + line.Trim();
            }
            else
            {
                // The blank line after the last entry, and everything after it. A
                // reader that ran on would turn the paragraph explaining the list
                // into entries of it.
                break;
            }
        }

        return entries;
    }

    private static string Section(string document)
    {
        var start = document.IndexOf(Heading, StringComparison.Ordinal);

        Assert.True(start >= 0, $"no section headed {Heading} was found");

        var body = document[(start + Heading.Length)..];
        var next = body.IndexOf("\n## ", StringComparison.Ordinal);

        return next < 0 ? body : body[..next];
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "refused-tests");

    /// <summary>
    /// The contributor guide, read out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason its neighbour
    /// gives: sources are not copied beside the assembly, and a path walked upwards
    /// from the assembly depends on the configuration and the framework it was built
    /// for. A route where the compiling machine is not the running one fails loudly
    /// on a missing file rather than quietly finding no section.
    /// </remarks>
    private static string Guide() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!, "CONTRIBUTING.md"));

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
