using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// <c>docs/subtitle-format.md</c> tells a reader which format this plugin writes, what
/// the bytes look like and that there is one format and nothing else. Nothing read that
/// page against the tree, and this does.
/// </summary>
/// <remarks>
/// It was the one page under <c>docs/</c> that no class in this project named. Every
/// claim on it is true today; what is missing is anything that stays true. The same
/// limit is written twice in this repository, on the limits page and here, and only the
/// first copy was compared against the tree. That is the shape that has already cost
/// something twice on this board: <c>docs/logging.md</c>'s denial was held in both
/// directions while the identical sentence on <c>docs/troubleshooting.md</c> was held by
/// nothing, so a logger arriving would have been repaired on one page only, which is
/// recorded on #73.
///
/// What a second format costs is why the second copy is worth reading rather than
/// trusting. It is one class in <c>Output/</c> implementing an interface that is already
/// there, so it is the cheapest addition in this tree to make and the easiest to make
/// without opening a page. <see cref="SubtitleFormatLimitTests"/> refuses the limits
/// page going stale that way. This refuses the page a reader is sent to from there for
/// the reasoning, which is where the claim is argued rather than filed.
///
/// The sample is the part with the most to lose. <see cref="SubRipWriterTests"/> asserts
/// the bytes against a block typed into the suite, and this page prints the same block
/// for a reader. Two copies of one fact, and only the suite's copy was compared with
/// what the writer does, so a change to the index base, to the timestamp spelling or to
/// the separator would have moved the suite and left the page describing a file the
/// plugin no longer writes. So this does not assert the sample: it PARSES the cues out
/// of the page, hands them to the writer, and requires what comes back to be the block
/// the page prints.
///
/// WHAT THIS DOES NOT DO. It compares blocks rather than bytes, having normalised the
/// line endings, so the page's two byte claims - UTF-8 with no byte order mark, and a
/// carriage return and a line feed at every line ending - are NOT held here. They are
/// held by <see cref="SubRipWriterTests"/>, and a tracked text file cannot carry a
/// carriage return in this repository anyway, so the page could not print the ending it
/// describes even if this read for it.
///
/// It reads the paragraph before the first heading for the extension and for the limit,
/// which is where the page makes both claims. A claim moved into a later section is
/// invisible to it, and two fixtures carry the token further down for exactly that
/// reason.
///
/// It says nothing about whether one format is the right limit, or whether SubRip is
/// the right one. Those are the decisions the page argues, and what this refuses is the
/// page and the tree disagreeing about which state the plugin is in.
///
/// Each leg that reads the page carries a fixture it has to refuse, under
/// <c>Fixtures/subtitle-format-page/</c>, and one neighbour that breaks nothing and has
/// to stay accepted. The direction where the assembly grows a second writer has no
/// fixture, because its subject is a compiled assembly rather than text: it was proved
/// by putting a second writer in <c>Output/</c>, watching it go red, and taking it out
/// again, and that run is in the pull request.
/// </remarks>
public class SubtitleFormatPageTests
{
    /// <summary>
    /// The section the sample block sits in, by title, because there is no other way to
    /// find it.
    /// </summary>
    private const string SampleSection = "What the files look like";

    /// <summary>
    /// The words the page states the one-format limit with.
    /// </summary>
    /// <remarks>
    /// The limits page files the same limit as an entry with a state marker beside it.
    /// Here it is a clause in a sentence, so the phrase is what there is to match.
    /// </remarks>
    private const string LimitClause = "and nothing else";

    private static readonly Regex _heading = new(
        @"^## (?<title>.+?)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A backticked token shaped like a file extension, which is how the opening
    /// paragraph writes what a reader will meet on disk.
    /// </summary>
    /// <remarks>
    /// The leading dot is required rather than optional, for the reason the neighbouring
    /// reader gives: without it this would also match the bare word the writer reports,
    /// and the point of the comparison is that the page and the tree spell one fact two
    /// ways and have to be made to agree anyway.
    /// </remarks>
    private static readonly Regex _extension = new(
        @"`(\.[A-Za-z0-9]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// One SubRip timing line, in the spelling the page prints and the writer produces.
    /// </summary>
    /// <remarks>
    /// This pattern is the format's own rather than this plugin's choice, which is why
    /// it is written here as a literal: it is what lets the cues be recovered from the
    /// page at all. What the comparison beside it judges is the block the writer returns
    /// for those cues, so a change to how this plugin spells a timestamp fails the
    /// comparison rather than this parse.
    /// </remarks>
    private static readonly Regex _timing = new(
        @"^(?<fromHours>\d{2}):(?<fromMinutes>\d{2}):(?<fromSeconds>\d{2}),(?<fromMilliseconds>\d{3}) --> (?<toHours>\d{2}):(?<toMinutes>\d{2}):(?<toSeconds>\d{2}),(?<toMilliseconds>\d{3})$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_sample_the_page_prints_is_what_the_writer_produces_for_those_cues()
    {
        var printed = SampleBlocks(Page());

        Assert.Equal(printed, BlocksTheWriterProduces(printed), StringComparer.Ordinal);
    }

    [Fact]
    public void The_extension_the_page_names_is_the_one_the_writer_reports()
    {
        var named = ExtensionsNamedInTheOpening(Page());

        Assert.True(
            named.Count == 1,
            $"the opening paragraph of the subtitle format page names {named.Count} extension(s), and one is what there is to compare");

        Assert.Equal("." + TheOneWriter().FileExtension, named[0], StringComparer.Ordinal);
    }

    [Fact]
    public void The_page_states_the_one_format_limit_while_this_plugin_ships_one_writer()
    {
        var writers = FormatWritersThisPluginShips();

        Assert.Equal(
            writers.Count == 1,
            Opening(Page()).Contains(LimitClause, StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_sample_the_writer_does_not_produce()
    {
        var printed = SampleBlocks(Fixture("a-sample-the-writer-does-not-produce"));

        Assert.NotEqual(printed, BlocksTheWriterProduces(printed), StringComparer.Ordinal);
    }

    [Fact]
    public void The_reader_refuses_an_extension_the_writer_does_not_report()
    {
        var named = ExtensionsNamedInTheOpening(Fixture("an-extension-the-writer-does-not-report"));

        Assert.Single(named);
        Assert.NotEqual("." + TheOneWriter().FileExtension, named[0], StringComparer.Ordinal);
    }

    [Fact]
    public void The_reader_refuses_an_opening_that_dropped_the_limit()
    {
        Assert.DoesNotContain(
            LimitClause,
            Opening(Fixture("a-page-that-dropped-the-limit")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        var page = Fixture("clean");
        var printed = SampleBlocks(page);
        var named = ExtensionsNamedInTheOpening(page);

        Assert.Equal(printed, BlocksTheWriterProduces(printed), StringComparer.Ordinal);
        Assert.Single(named);
        Assert.Equal("." + TheOneWriter().FileExtension, named[0], StringComparer.Ordinal);
        Assert.Contains(LimitClause, Opening(page), StringComparison.Ordinal);
    }

    [Fact]
    public void No_fixture_is_a_document_anything_else_reads()
    {
        // The rule the neighbouring fixture directories keep, for the same reason: these
        // are pages about this repository that are deliberately untrue, and a plain
        // extension would put one in front of anything that walks the tree for markdown.
        // The README beside them is the one document in there that is true, so it is
        // named rather than matched by a pattern that would let a fixture through.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, path => Assert.EndsWith(".md.fixture", path, StringComparison.Ordinal));
    }

    /// <summary>
    /// The blocks this plugin's writer returns for the cues the page's own sample
    /// describes.
    /// </summary>
    /// <remarks>
    /// The line endings are normalised on the way out. A tracked text file in this
    /// repository may not carry a carriage return, so the page cannot print the ending
    /// the writer emits, and comparing bytes would refuse every page rather than a wrong
    /// one.
    /// </remarks>
    private static List<string> BlocksTheWriterProduces(IReadOnlyList<string> printed)
    {
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var written = strict.GetString(new SubRipWriter().Write(CuesThePageDescribes(printed)));

        return Blocks(written.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static List<TimedSegment> CuesThePageDescribes(IReadOnlyList<string> blocks)
    {
        var cues = new List<TimedSegment>();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n');

            Assert.True(
                lines.Length == 3,
                $"a sample block has {lines.Length} line(s), and a SubRip block a reader could learn the format from is an index, a timing line and the text");

            var timing = _timing.Match(lines[1]);

            Assert.True(timing.Success, $"the sample's timing line does not read as one: {lines[1]}");

            cues.Add(new TimedSegment(At(timing, "from"), At(timing, "to"), lines[2]));
        }

        Assert.NotEmpty(cues);

        return cues;
    }

    private static TimeSpan At(Match timing, string side) =>
        new(
            0,
            int.Parse(timing.Groups[side + "Hours"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(timing.Groups[side + "Minutes"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(timing.Groups[side + "Seconds"].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(timing.Groups[side + "Milliseconds"].Value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// The sample the page prints, as blocks, with the indentation that makes it a code
    /// block in markdown removed.
    /// </summary>
    private static List<string> SampleBlocks(string page)
    {
        var body = Sections(page)
            .Where(section => section.Title.Equals(SampleSection, StringComparison.Ordinal))
            .Select(section => section.Body)
            .FirstOrDefault();

        Assert.True(body is not null, $"the page carries no section titled \"{SampleSection}\", so there is no sample to compare");

        var indented = new List<string>();

        foreach (var line in body!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                indented.Add(line[4..]);
            }
            else if (line.Length == 0 && indented.Count > 0)
            {
                indented.Add(string.Empty);
            }
            else if (indented.Count > 0)
            {
                break;
            }
        }

        return Blocks(string.Join('\n', indented));
    }

    private static List<string> Blocks(string sample) =>
        sample.Split("\n\n", StringSplitOptions.None)
            .Select(block => block.Trim('\n'))
            .Where(block => block.Length > 0)
            .ToList();

    private static List<string> ExtensionsNamedInTheOpening(string page) =>
        _extension.Matches(Opening(page)).Select(match => match.Groups[1].Value).ToList();

    /// <summary>
    /// The paragraph before the first heading, which is where this page makes both of
    /// the claims read here.
    /// </summary>
    private static string Opening(string page)
    {
        var first = _heading.Match(page);

        return first.Success ? page[..first.Index] : page;
    }

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

    /// <summary>
    /// Every implementation of the format writer interface in the assembly this plugin
    /// ships.
    /// </summary>
    /// <remarks>
    /// Anchored on the interface's own assembly rather than on a concrete writer, so the
    /// day the one writer is renamed this still reads the same population. A stand-in
    /// for the same interface inside this test project is deliberately outside the
    /// subject: a test double is not a format this plugin writes.
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

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "subtitle-format-page");

    /// <summary>
    /// The page, read out of the checkout rather than out of a copy beside the assembly,
    /// for the reason its neighbours give: sources are not copied there, and a path
    /// walked upwards from the assembly depends on the configuration and the framework
    /// it was built for.
    /// </summary>
    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "subtitle-format.md"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private sealed record Section(string Title, string Body);
}
