using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend guide is the page an operator reads before they configure anything,
/// and it makes two claims a run can check: that the values it offers are the values
/// this plugin answers to, and that no run of this plugin has yet produced a
/// subtitle.
/// </summary>
/// <remarks>
/// The failure the first half is written against is the one a guide is worst at
/// surviving. A backend added to the code is added by somebody reading the code, and
/// a page two directories away that lists the old set then tells an operator that
/// the value they were given does not exist. The reverse costs more: a page offering
/// a name nothing answers to sends somebody to type a setting that reaches
/// selection and transcribes nothing, and the page is the only place that said the
/// name was real.
///
/// The second half is the shape <c>LoggingPageTests</c> holds over its own page and
/// it is read in both directions for the same reason. A guide still saying nothing
/// has ever run, while a run does, reads as out of date and is caught by the reader.
/// A guide that quietly dropped the sentence while nothing runs is the other
/// direction and the worse one: everything above that sentence then reads as a
/// walkthrough of something that works.
///
/// WHAT THIS DOES NOT DO. It does not judge a figure on the page. The model sizes
/// are the upstream project's own table and the licences are what each upstream
/// repository declares, and both are readings of somewhere else: every test here
/// runs with the machine offline, so what sources them is the command printed beside
/// each one and a person running it. Nor does it judge the prose. Whether the guide
/// is followable by somebody who has never seen a Whisper model is what the issue's
/// first done-condition is for, and it cannot be met while the sentence this class
/// holds is still true.
///
/// Its subject is one page. The three names are compared against
/// <see cref="BackendNames"/> rather than against the configuration page, because
/// that set is what the code answers to and the configuration page is held against
/// the same set by <c>BackendChoicePageTests</c>. Two documents compared to each
/// other would agree with each other while both drifted from the code.
/// </remarks>
public class BackendGuidePageTests
{
    /// <summary>
    /// The heading the offered values are read from under. The reader stops at the
    /// next heading of the same level, so a table added elsewhere on the page is
    /// somebody else's table.
    /// </summary>
    private const string OfferedUnder = "## What you set the backend to";

    /// <summary>
    /// The sentence the page states its own standing with, and the whole of what is
    /// machine-read out of that page besides the table.
    /// </summary>
    private const string NothingHasRunYet = "No run of this plugin has ever produced a subtitle";

    /// <summary>
    /// The file the page's own command reads, relative to the repository root.
    /// </summary>
    private const string TaskSource =
        "Jellyfin.Plugin.WhisperSubtitles/Scheduling/SubtitleGenerationTask.cs";

    /// <summary>
    /// The parts a run over an item would have to reach. The same list the page
    /// prints its command with, so the two cannot ask different questions while both
    /// look right.
    /// </summary>
    private static readonly string[] PipelineParts =
    {
        "ItemSelection",
        "AudioExtractor",
        "BoundedRun",
        "SubtitlePublisher",
        "TranscriptionRequest",
        "TemporaryAudioSweep",
        "AttemptLedger",
        "DurationWeightedProgress",
    };

    /// <summary>
    /// A row of the offered-values table: the first cell, in backticks.
    /// </summary>
    private static readonly Regex OfferedValue =
        new(@"^\|\s*`([^`]+)`\s*\|", RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_offers_every_backend_this_plugin_answers_to_and_no_other()
    {
        var offered = Offered(Page());
        var known = BackendNames.Known.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(known, offered);
    }

    [Fact]
    public void The_reader_finds_the_table_it_judges()
    {
        // Without this the comparison above passes on a page whose table was
        // renamed, reformatted or deleted, by finding nothing on the page side and
        // nothing to disagree with it.
        Assert.NotEmpty(Offered(Page()));
    }

    [Fact]
    public void The_neighbour_that_offers_exactly_the_right_set_is_accepted()
    {
        // Without this a reader that returned nothing whatever it was shown would
        // satisfy both refusal legs below and say nothing about the real page.
        Assert.Equal(
            BackendNames.Known.Order(StringComparer.Ordinal).ToList(),
            Offered(Fixture("clean")));
    }

    [Fact]
    public void The_reader_refuses_a_page_offering_a_value_this_plugin_does_not_answer_to()
    {
        var offered = Offered(Fixture("a-value-this-plugin-does-not-answer-to"));

        Assert.Contains("Whisperx", offered, StringComparer.Ordinal);
        Assert.False(
            BackendNames.IsKnown("Whisperx"),
            "the fixture's extra value is one this plugin answers to, so it proves nothing");
    }

    [Fact]
    public void The_reader_refuses_a_page_that_leaves_a_backend_out()
    {
        var offered = Offered(Fixture("a-value-the-table-leaves-out"));

        Assert.DoesNotContain(
            Jellyfin.Plugin.WhisperSubtitles.Backends.Remote.RemoteWhisperBackend.BackendName,
            offered,
            StringComparer.Ordinal);
        Assert.NotEmpty(offered);
    }

    [Fact]
    public void The_reader_reads_its_own_section_and_not_a_table_beside_it()
    {
        // The near miss. A page that lists backend-shaped values in a second table
        // further down is the shape a reader bounded by nothing accepts, and it
        // would then pass whatever the real table said.
        var fixture = Fixture("a-table-in-another-section");

        Assert.Contains("Whisperx", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("Whisperx", Offered(fixture), StringComparer.Ordinal);
    }

    [Fact]
    public void A_page_carrying_the_heading_and_no_rows_offers_nothing()
    {
        Assert.Empty(Offered(Fixture("no-table-at-all")));
    }

    [Fact]
    public void The_page_says_nothing_has_run_yet_exactly_while_nothing_has()
    {
        var reached = PartsTheTaskReaches();
        var says = Page().Contains(NothingHasRunYet, StringComparison.Ordinal);

        Assert.True(
            !says || reached.Count == 0,
            $"docs/choosing-a-backend.md says \"{NothingHasRunYet}\" and {TaskSource} now reaches {string.Join(", ", reached)}. Everything above that sentence reads as a walkthrough of a run that happens.");

        Assert.True(
            says || reached.Count > 0,
            $"{TaskSource} reaches no part of a run and docs/choosing-a-backend.md no longer says so, so a reader is left to discover that the page cannot be followed to a subtitle.");
    }

    [Fact]
    public void The_page_and_the_search_it_quotes_are_about_the_same_file()
    {
        // The page hands a reader the command behind its own claim. A command asking
        // a different question from the one the leg above asks would leave the two
        // disagreeing while both looked right, and the reader trusts the one they
        // can run.
        var page = Page();

        Assert.Contains(TaskSource, page, StringComparison.Ordinal);

        Assert.All(
            PipelineParts,
            part => Assert.Contains(part, page, StringComparison.Ordinal));
    }

    [Fact]
    public void No_fixture_is_a_page_anything_else_reads()
    {
        // Every fixture here is a page that is deliberately wrong, and each one is
        // kept under an extension no reader of docs/ walks.
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
    /// The values a page offers, read out of the one section that offers them.
    /// </summary>
    /// <param name="page">The page text.</param>
    /// <returns>The values, ordered, with no duplicates.</returns>
    private static List<string> Offered(string page)
    {
        var start = page.IndexOf(OfferedUnder, StringComparison.Ordinal);

        Assert.True(start >= 0, $"no section headed \"{OfferedUnder}\"");

        var after = start + OfferedUnder.Length;
        var end = page.IndexOf("\n## ", after, StringComparison.Ordinal);
        var section = end < 0 ? page[after..] : page[after..end];

        return OfferedValue.Matches(section)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The parts of a run the scheduled task names, by the same reading the page's
    /// own command makes.
    /// </summary>
    /// <remarks>
    /// The whole file rather than its code, comments included, because the question
    /// is whether the join has started: a file that names one of these in a remark
    /// is a file where somebody has begun, and the page's command reads the same
    /// bytes.
    /// </remarks>
    private static List<string> PartsTheTaskReaches()
    {
        var source = Path.Combine(RepositoryRoot(), TaskSource.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(source), $"the scheduled task was not found at {source}");

        var text = File.ReadAllText(source);

        return PipelineParts
            .Where(part => text.Contains(part, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// The guide, read out of the checkout rather than out of a copy beside the
    /// assembly.
    /// </summary>
    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "choosing-a-backend.md"));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "backend-guide");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
