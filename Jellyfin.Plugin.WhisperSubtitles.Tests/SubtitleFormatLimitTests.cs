using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The limits page says this plugin writes one subtitle format and nothing else,
/// and files that as held today. This reads the claim against the tree rather than
/// taking the marker for a reading.
/// </summary>
/// <remarks>
/// The marker on that entry names an issue and a page and no suite, and every leg
/// <see cref="LimitsPageTests"/> runs over it is satisfied by a name resolving: the
/// entry is in one of the two states, it names an issue to argue with, and the page
/// it points at is a file. None of them asks whether the tree still writes one
/// format, and that class of drift is not hypothetical here. A denial that outlived
/// the work it denied stood in <c>SECURITY.md</c> for four days through an edit to
/// the file that did not re-read it, which is recorded on #85.
///
/// What a second format costs is what makes this worth a check rather than a review
/// note. It is one class in <c>Output/</c> implementing an interface that is already
/// there, so it is the cheapest addition in this tree to make and the easiest to
/// make without opening a page under <c>docs/</c>. The entry would then be a promise
/// about a release the release no longer keeps, in the one document written for
/// somebody deciding whether this plugin does what they need.
///
/// Two directions, and the second is the quieter one. A writer arriving is refused
/// by the census. The extension moving is refused by comparing the one the entry
/// quotes against the one the writer reports, so a page saying one thing over a tree
/// producing another is red rather than believed.
/// <see cref="SubRipWriterTests"/> already holds that extension against a literal
/// typed into the suite; what is new here is that the literal a READER meets is the
/// one compared.
///
/// WHAT THIS DOES NOT DO. Its subject is the assembly this plugin ships, and the
/// stand-in this suite carries for the same interface is deliberately outside it: a
/// test double is not a format this plugin writes, and a census that counted one
/// would refuse the seam it exists to prove is usable. So a format writer added to
/// the test project passes here, which is the right answer rather than a gap.
///
/// It says nothing about whether one format is the RIGHT limit. That is the decision
/// the entry names, and what this refuses is the tree and the page disagreeing about
/// which state the plugin is in.
///
/// It reads what is inside backticks in that one entry. An extension written in
/// plain prose is invisible to it, which is why the reader carries its own guard
/// below rather than being trusted to have compared anything.
///
/// Each leg that reads the page carries a fixture it has to refuse, under
/// <c>Fixtures/one-format-limit/</c>, and one neighbour that breaks nothing and has
/// to stay accepted. The census has no fixture, because its subject is a compiled
/// assembly rather than text: it was proved by putting a second writer in
/// <c>Output/</c>, watching it go red, and taking it out again, and that run is in
/// the pull request.
/// </remarks>
public class SubtitleFormatLimitTests
{
    /// <summary>
    /// The entry on the limits page this reads, by title, because there is no other
    /// way to find it.
    /// </summary>
    private const string Entry = "It writes one subtitle format";

    private static readonly Regex _heading = new(
        @"^## (?<title>.+?)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A backticked token shaped like a file extension, which is how that entry
    /// writes the format a reader will meet on disk.
    /// </summary>
    /// <remarks>
    /// The leading dot is required rather than optional. Without it this would also
    /// match the bare word the writer reports, and the point of the comparison is
    /// that the page and the tree spell one fact two ways and have to be made to
    /// agree anyway.
    /// </remarks>
    private static readonly Regex _extension = new(
        @"`(\.[A-Za-z0-9]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void This_plugin_ships_one_subtitle_format_writer()
    {
        var writers = FormatWritersThisPluginShips();

        Assert.True(
            writers.Count == 1,
            $"this plugin's assembly carries {writers.Count} implementation(s) of {nameof(ISubtitleFormatWriter)}: {string.Join(", ", writers.Select(type => type.FullName))}. The limits page says it writes one subtitle format and nothing else for the first release and files that as held today, so a second one is a page to move rather than a leg to widen.");
    }

    [Fact]
    public void The_extension_the_entry_quotes_is_the_one_that_writer_reports()
    {
        var quoted = ExtensionsQuotedByTheEntry(Page());

        Assert.True(
            quoted.Count == 1,
            $"the limits page entry \"{Entry}\" quotes {quoted.Count} extension(s), and one is what there is to compare");

        Assert.Equal("." + TheOneWriter().FileExtension, quoted[0], StringComparer.Ordinal);
    }

    [Fact]
    public void The_page_carries_the_entry_this_reads()
    {
        // Without this the comparison beside it passes on a page that lost the entry,
        // by finding nothing to quote and reporting that as a count rather than as a
        // page which stopped making the claim.
        Assert.Contains(Entry, Titles(Page()), StringComparer.Ordinal);
    }

    [Fact]
    public void The_reader_refuses_a_page_that_no_longer_carries_the_entry()
    {
        Assert.DoesNotContain(Entry, Titles(Fixture("no-entry-of-that-name")), StringComparer.Ordinal);
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_quotes_no_extension()
    {
        var page = Fixture("an-entry-that-quotes-no-extension");

        Assert.Contains(Entry, Titles(page), StringComparer.Ordinal);
        Assert.Empty(ExtensionsQuotedByTheEntry(page));
    }

    [Fact]
    public void The_reader_refuses_an_extension_this_plugin_does_not_produce()
    {
        var quoted = ExtensionsQuotedByTheEntry(Fixture("an-extension-the-writer-does-not-produce"));

        Assert.Single(quoted);
        Assert.NotEqual("." + TheOneWriter().FileExtension, quoted[0], StringComparer.Ordinal);
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        var page = Fixture("clean");
        var quoted = ExtensionsQuotedByTheEntry(page);

        Assert.Contains(Entry, Titles(page), StringComparer.Ordinal);
        Assert.Single(quoted);
        Assert.Equal("." + TheOneWriter().FileExtension, quoted[0], StringComparer.Ordinal);
    }

    [Fact]
    public void No_fixture_is_a_document_anything_else_reads()
    {
        // The rule the neighbouring fixture directories keep, for the same reason:
        // these are pages about this repository that are deliberately untrue, and a
        // plain extension would put one in front of anything that walks the tree for
        // markdown. The README beside them is the one document in there that is true,
        // so it is named rather than matched by a pattern that would let a fixture
        // through.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, path => Assert.EndsWith(".md.fixture", path, StringComparison.Ordinal));
    }

    /// <summary>
    /// Every implementation of the format writer interface in the assembly this
    /// plugin ships.
    /// </summary>
    /// <remarks>
    /// Anchored on the interface's own assembly rather than on a concrete writer, so
    /// the day the one writer is renamed this still reads the same population.
    /// </remarks>
    private static List<Type> FormatWritersThisPluginShips() =>
        typeof(ISubtitleFormatWriter).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && typeof(ISubtitleFormatWriter).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    private static ISubtitleFormatWriter TheOneWriter()
    {
        var writers = FormatWritersThisPluginShips();

        Assert.True(
            writers.Count == 1,
            $"this plugin's assembly carries {writers.Count} format writer(s), so there is no one extension for the page to be compared against");

        return (ISubtitleFormatWriter)Activator.CreateInstance(writers[0])!;
    }

    private static List<string> ExtensionsQuotedByTheEntry(string page)
    {
        var body = Sections(page)
            .Where(section => section.Title.Equals(Entry, StringComparison.Ordinal))
            .Select(section => section.Body)
            .FirstOrDefault();

        return body is null
            ? new List<string>()
            : _extension.Matches(body).Select(match => match.Groups[1].Value).ToList();
    }

    private static List<string> Titles(string page) =>
        Sections(page).Select(section => section.Title).ToList();

    private static List<Section> Sections(string page)
    {
        var headings = _heading.Matches(page).ToList();
        var sections = new List<Section>();

        for (var i = 0; i < headings.Count; i++)
        {
            var start = headings[i].Index + headings[i].Length;
            var end = i + 1 < headings.Count ? headings[i + 1].Index : page.Length;

            sections.Add(new Section(headings[i].Groups["title"].Value, page[start..end]));
        }

        return sections;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "one-format-limit");

    /// <summary>
    /// The limits page, read out of the checkout rather than out of a copy beside the
    /// assembly, for the reason its neighbours give: sources are not copied there, and
    /// a path walked upwards from the assembly depends on the configuration and the
    /// framework it was built for.
    /// </summary>
    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "limits.md"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private sealed record Section(string Title, string Body);
}
