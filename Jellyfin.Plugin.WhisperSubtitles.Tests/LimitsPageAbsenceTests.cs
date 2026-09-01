using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Four sentences on the limits page say the tree does not do something yet. Three
/// of them are the reason an entry beside them is filed as decided rather than
/// held; the fourth is what two entries filed as held today rest on. This reads the
/// tree for all four and refuses the page in both directions.
/// </summary>
/// <remarks>
/// THE PAGE ITSELF NAMES THIS GAP AND THIS IS THREE OF IT. Its closing section
/// says <c>LimitsPageTests</c> cannot say whether a marker is true, because
/// whether a named thing really holds a limit is a reading rather than a
/// comparison. That is right about a marker in general and wrong about a sentence
/// of the shape "nothing here reaches X", which is a search over this project and
/// nothing else. Those are the four taken here, and the rest of the gap is
/// untouched.
///
/// THE FOURTH CARRIES MORE WEIGHT THAN THE OTHER THREE AND ARRIVED LAST. The
/// yielding, sweep and joining sentences each explain why an entry is filed as
/// decided and not yet built, so a reader who ignores one still meets an entry
/// promising nothing. The configuration-location sentence is the whole of why its
/// two entries are filed as HELD TODAY, on the writing list and on the way out, so
/// a reader is told a limit holds on the strength of a denial nothing ran.
///
/// WHAT REACHING THE LOCATION MEANS IS A MEMBER THAT YIELDS A PATH, NOT THE PATHS
/// OBJECT. Plugin.cs takes IApplicationPaths as a constructor parameter and hands
/// it to the server's own base class, which is what makes the server rather than
/// this plugin the thing that writes the file, so a search for that parameter would
/// refuse this page for a plugin that reaches nothing. The set below is the members
/// that return a path instead, and
/// <c>The_reaching_set_is_the_members_that_yield_a_path_and_not_the_paths_object</c>
/// is what refuses that distinction being lost.
///
/// THE FAILURE IT IS WRITTEN AGAINST IS A SENTENCE SURVIVING THE WORK IT DENIES,
/// and it is not hypothetical on this board: the security policy went on saying
/// nothing in the plugin called the item selection for four days after the dry run
/// started calling it, and it was edited once in between without the sentence being
/// re-read. A page that keeps a denial after the thing arrives is worse than one
/// that never made it, because a reader takes the silence for a reading.
///
/// EACH LEG FAILS IN BOTH DIRECTIONS AND THAT IS THE POINT. While the tree answers
/// nothing, the sentence has to be there, so a denial cannot be dropped in silence
/// either. The page's two states are a promise about what a reader may rely on, and
/// an entry that quietly loses the sentence explaining which state it is in has
/// stopped keeping that promise as surely as one that keeps a wrong sentence.
///
/// WHAT THIS DOES NOT DO.
///
/// It holds three sentences and not the entries around them. An entry that
/// reproduces its sentence exactly under prose drawing the wrong conclusion passes
/// here, and every other marker on the page is where the closing section leaves it.
///
/// It matches a phrase rather than a meaning. The page is read with its line
/// breaks collapsed, so reflowing a paragraph moves nothing, but a rewording that
/// says the same thing in other words turns this red and has to be made here as
/// well. That is the price of quoting rather than describing, and the neighbouring
/// page checks pay it for the same reason.
///
/// Its tree side reads names on lines that are not comments, so it cannot see a
/// call made by reflection and it would count a mention parked at the end of a line
/// of code. It asks whether a name appears in a population, never whether it is
/// used, so a using directive or a parameter type is an answer.
///
/// IT ANSWERED ITSELF ONCE AND THE SPLIT BELOW IS WHAT STOPPED IT. The page's
/// closing section names all three sentences, so a reader taking the whole page
/// found every one of them there and passed whatever the entries said. It was found
/// by deleting the joining sentence out of its entry and watching this stay green,
/// which is the only way that failure shows itself: a check that answers itself is
/// green in exactly the state it exists to refuse. What it reads now stops before
/// the closing heading, and
/// <c>The_section_that_describes_this_check_is_not_part_of_what_it_reads</c> is
/// what refuses the split being lost again rather than remembered.
/// </remarks>
public sealed class LimitsPageAbsenceTests
{
    private const string Page = "docs/limits.md";

    private const string PluginProject = "Jellyfin.Plugin.WhisperSubtitles";

    /// <summary>
    /// Where the scheduled task and everything a run is made of live. The sweep
    /// sentence is about a call at the start of a run, so the population is the
    /// directory a run is written in rather than the whole project.
    /// </summary>
    private const string Scheduling = "Scheduling";

    private const string Task = "Scheduling/SubtitleGenerationTask.cs";

    /// <summary>
    /// The heading the entries stop at. What follows it is the section describing
    /// the checks over this page, this class among them, and it quotes the
    /// sentences below - so reading past it is how this reader comes to answer its
    /// own question.
    /// </summary>
    private const string Closing = "## When this list is checked against the code";

    /// <summary>
    /// What the yielding entry says about the server's own answer to whether it is
    /// busy with another plugin's work.
    /// </summary>
    private const string NothingAsksTheServer = "no type here reaches `ITaskManager`";

    /// <summary>
    /// What both halves of the temporary audio entries say about the sweep. The page
    /// writes it twice, once where the file is made and once on the way out, and
    /// either of them is the sentence a reader relies on.
    /// </summary>
    private const string NothingCallsTheSweep = "nothing calls it";

    /// <summary>
    /// What the subtitle entry says about the run that would write one.
    /// </summary>
    private const string NothingJoinsThePipeline = "nothing joins the pipeline into the task";

    /// <summary>
    /// What both configuration entries say about the file the server writes for this
    /// plugin. The page writes it twice, once in the write list and once on the way
    /// out, and it is the whole of why either of them is filed as held today.
    /// </summary>
    private const string NothingReachesTheLocation = "nothing in this plugin reaches the location";

    /// <summary>
    /// The parameter the plugin hands to its base class. It is deliberately outside
    /// the set of names that count as reaching a location.
    /// </summary>
    private const string ThePathsObject = "IApplicationPaths";

    /// <summary>
    /// The names a run is assembled out of. The task naming any of them is the
    /// joining the page denies, which is the same set the troubleshooting page and
    /// the issue that owns the joining both read.
    /// </summary>
    private static readonly string[] _pipeline =
    [
        "ItemSelection",
        "AudioExtractor",
        "BoundedRun",
        "SubtitlePublisher",
        "TranscriptionRequest",
    ];

    /// <summary>
    /// The members that hand out a path under which the server keeps plugin data.
    /// Naming one of these is this plugin reaching the location; taking the object
    /// that carries them is not, which is the distinction the remarks above set out.
    /// </summary>
    private static readonly string[] _reaching =
    [
        "ConfigurationFilePath",
        "PluginConfigurationsPath",
        "ConfigurationDirectoryPath",
        "PluginsPath",
        "DataPath",
        "ApplicationPaths",
    ];

    [Fact]
    public void The_reader_finds_the_page_and_the_sources_rather_than_comparing_nothing()
    {
        // Guards every leg below. A reader that found an empty page, or an empty
        // population to search, would report the page agreeing with the tree
        // whatever either of them said, and it would do it in green.
        Assert.True(Read(Page).Length > 0, $"{Page} is empty, so the sentences this judges are not there to judge");

        Assert.True(
            Entries().Length > 0,
            $"{Page} carries no text before \"{Closing}\", so the entries this reads are not there to read");

        Assert.True(
            SourcesUnder(string.Empty).Count > 1,
            "the plugin project gave one source file or none, so the searches below are answering about nothing");

        Assert.NotEmpty(SourcesUnder(Scheduling));

        Assert.True(
            File.Exists(Path.Combine(RepositoryRoot(), PluginProject, Task.Replace('/', Path.DirectorySeparatorChar))),
            $"{PluginProject}/{Task} was not found, and the joining leg reads that file");
    }

    [Fact]
    public void The_page_says_nothing_asks_the_server_what_it_is_running_only_while_nothing_does()
    {
        var naming = SourcesNaming(SourcesUnder(string.Empty), ["ITaskManager"]);

        AssertTheDenialMatchesTheTree(
            NothingAsksTheServer,
            naming,
            "the yielding entry",
            "a plugin source naming ITaskManager is this plugin asking the server what it is running, which is the half that entry says is decided and not yet built");
    }

    [Fact]
    public void The_page_says_nothing_calls_the_sweep_at_the_start_of_a_run_only_while_nothing_does()
    {
        var naming = SourcesNaming(SourcesUnder(Scheduling), ["TemporaryAudioSweep"]);

        AssertTheDenialMatchesTheTree(
            NothingCallsTheSweep,
            naming,
            "the temporary audio entries, on both the writing and the removal side",
            "the sweep named anywhere a run is written is the call those entries say has not arrived, and a file orphaned by a dead process stops being uncollected on the day it does");
    }

    [Fact]
    public void The_page_says_nothing_joins_the_pipeline_into_the_task_only_while_nothing_does()
    {
        var naming = SourcesNaming([Path.Combine(RepositoryRoot(), PluginProject, Task.Replace('/', Path.DirectorySeparatorChar))], _pipeline);

        AssertTheDenialMatchesTheTree(
            NothingJoinsThePipeline,
            naming,
            "the subtitle entry in the write list",
            $"the task naming one of {string.Join(", ", _pipeline)} is the joining that entry waits on, and a run that writes a subtitle is the thing a reader would then be relying on this page for");
    }

    [Fact]
    public void The_page_says_nothing_reaches_the_configuration_location_only_while_nothing_does()
    {
        var naming = SourcesNaming(SourcesUnder(string.Empty), _reaching);

        AssertTheDenialMatchesTheTree(
            NothingReachesTheLocation,
            naming,
            "both configuration entries, in the write list and on the way out",
            $"a plugin source naming one of {string.Join(", ", _reaching)} is this plugin reaching the place the server keeps its data, and those two entries are filed as held today on the strength of that sentence rather than on a check of their own");
    }

    [Fact]
    public void The_reaching_set_is_the_members_that_yield_a_path_and_not_the_paths_object()
    {
        // The near-miss the set above exists against. Widening it to the parameter
        // the plugin hands to its base class would turn the leg above red for a
        // plugin that reads no location at all, and that parameter is the first
        // thing a reader of the composition root meets. So the boundary is asserted
        // rather than explained: the object is in this tree, and no name in the set
        // reaches it.
        var carrying = SourcesNaming(SourcesUnder(string.Empty), [ThePathsObject]);

        Assert.True(
            carrying.Count > 0,
            $"no source of this plugin names {ThePathsObject}, so the distinction this leg holds has stopped having a subject and the set above is no longer erring where it says it does");

        Assert.DoesNotContain(ThePathsObject, _reaching, StringComparer.Ordinal);

        // Not that the set holds no substring of the parameter - one of them is a
        // substring of it - but that the SEARCH does not answer for the parameter.
        // What separates the two is the word boundary the matcher below applies, so
        // the guard runs that matcher rather than restating what it is expected to
        // do. Widening the set to the parameter turns this red, and so does dropping
        // the boundary out of the matcher.
        Assert.False(
            NamesAny(ThePathsObject, _reaching),
            $"a source whose only mention is {ThePathsObject} is counted as reaching a location, so the leg above refuses this page for a plugin that hands the server's own paths object to the server's own base class and reads no location out of it");
    }

    [Fact]
    public void The_section_that_describes_this_check_is_not_part_of_what_it_reads()
    {
        // The closing section names the three sentences this class holds. A reader
        // taking the whole page finds them there and passes whatever the entries
        // say, which is a check answering its own question, and it is green in
        // exactly the state it exists to refuse. So the split is asserted rather
        // than intended: the page has to carry the joining sentence somewhere this
        // reader does not look.
        var whole = Flattened(Read(Page));
        var entries = Flattened(Entries());

        Assert.DoesNotContain(Closing, entries, StringComparison.Ordinal);

        foreach (var quoted in new[] { NothingJoinsThePipeline, NothingReachesTheLocation })
        {
            Assert.True(
                Occurrences(whole, quoted) > Occurrences(entries, quoted),
                $"{Page} writes \"{quoted}\" {Occurrences(whole, quoted)} time(s) and the part before \"{Closing}\" carries {Occurrences(entries, quoted)} of them. The section after that heading quotes what this class holds, so a reader of the whole page answers its own question, and the two counts being equal means either the split was lost or that section stopped quoting it.");
        }
    }

    [Fact]
    public void The_page_reads_the_same_whatever_the_checkout_did_to_its_line_endings()
    {
        // One clone checks this file out with carriage returns and another does not,
        // and neither is wrong: `.gitattributes` stores a line feed and lets the
        // checkout decide. Collapsing the line breaks is also what makes a reflowed
        // paragraph move nothing here, so the two are one reading.
        var asLineFeeds = Read(Page).Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(Flattened(asLineFeeds), Flattened(asCarriageReturns));
        Assert.NotEmpty(Flattened(asCarriageReturns));
    }

    /// <summary>
    /// Refuses the page in whichever direction it and the tree disagree.
    /// </summary>
    /// <param name="denial">The sentence the page carries while the tree answers nothing.</param>
    /// <param name="naming">The sources that answer, empty where none does.</param>
    /// <param name="entry">Which entries the sentence belongs to, for the message.</param>
    /// <param name="why">What the arrival means, for the message.</param>
    private static void AssertTheDenialMatchesTheTree(
        string denial,
        IReadOnlyList<string> naming,
        string entry,
        string why)
    {
        var page = Flattened(Entries());

        if (naming.Count == 0)
        {
            Assert.True(
                page.Contains(denial, StringComparison.Ordinal),
                $"nothing in the tree answers for this yet and {Page} has stopped saying \"{denial}\" in {entry}. That sentence is what tells a reader which of the two states the entry is in, and dropping it while it is still true leaves the entry looking held.");

            return;
        }

        Assert.False(
            page.Contains(denial, StringComparison.Ordinal),
            $"{string.Join(", ", naming)} answers for this now, and {Page} still says \"{denial}\" in {entry}. {why}. A denial that survives the work it denies is the failure this page's two states exist against.");
    }

    /// <summary>
    /// The sources of this plugin naming any of a set of names on a line that is not
    /// a comment.
    /// </summary>
    /// <param name="sources">The files to read.</param>
    /// <param name="names">The names to look for.</param>
    /// <returns>The paths that answer, relative to the project and sorted.</returns>
    private static IReadOnlyList<string> SourcesNaming(IReadOnlyList<string> sources, IReadOnlyList<string> names)
    {
        var root = Path.Combine(RepositoryRoot(), PluginProject);

        return [.. sources
            .Where(path => NamesAny(CodeIn(path), names))
            .Select(path => PluginProject + "/" + Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Whether a text names any of a set of names as a whole word.
    /// </summary>
    /// <remarks>
    /// The word boundary is the whole of what separates a member that yields a path
    /// from the paths object carrying it, so this is one function rather than a
    /// pattern written twice: the search below it and the guard that holds that
    /// distinction both come through here.
    /// </remarks>
    /// <param name="text">The text to search.</param>
    /// <param name="names">The names to look for.</param>
    /// <returns><c>true</c> where the text names one of them.</returns>
    private static bool NamesAny(string text, IReadOnlyList<string> names) =>
        names.Any(name => Regex.IsMatch(
            text,
            @"\b" + name + @"\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5)));

    /// <summary>
    /// Every source file of the plugin project, or of one directory inside it.
    /// </summary>
    /// <param name="under">A directory relative to the project, or an empty string for the whole of it.</param>
    /// <returns>The paths, in no particular order.</returns>
    private static IReadOnlyList<string> SourcesUnder(string under)
    {
        var root = Path.Combine(RepositoryRoot(), PluginProject);
        var from = under.Length == 0 ? root : Path.Combine(root, under);

        Assert.True(Directory.Exists(from), $"there is nothing to walk at {from}");

        return [.. Directory
            .GetFiles(from, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');

                return !relative.Contains("/bin/", StringComparison.Ordinal)
                    && !relative.Contains("/obj/", StringComparison.Ordinal);
            })];
    }

    /// <summary>
    /// A source file with its comment lines taken out.
    /// </summary>
    /// <remarks>
    /// A line whose trimmed form opens a comment, and nothing finer. Every comment
    /// in this project is written that way, and a mention parked at the end of a
    /// line of code would be counted, which errs towards asking rather than towards
    /// silence.
    /// </remarks>
    /// <param name="path">The file to read.</param>
    /// <returns>Its lines that are not comments, joined.</returns>
    private static string CodeIn(string path) =>
        string.Join(
            '\n',
            File.ReadAllLines(path).Where(line =>
            {
                var trimmed = line.TrimStart();

                return !trimmed.StartsWith("//", StringComparison.Ordinal)
                    && !trimmed.StartsWith('*')
                    && !trimmed.StartsWith("/*", StringComparison.Ordinal);
            }));

    /// <summary>
    /// The part of the page that is entries, which is everything before the section
    /// describing the checks over it.
    /// </summary>
    /// <returns>The text up to the closing heading, or an empty string where that heading is gone.</returns>
    private static string Entries()
    {
        var lines = Read(Page).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var at = Array.FindIndex(lines, line => line.Trim().Equals(Closing, StringComparison.Ordinal));

        return at < 0 ? string.Empty : string.Join('\n', lines.Take(at));
    }

    /// <summary>
    /// How many times one string occurs in another, without overlapping.
    /// </summary>
    /// <param name="text">The text to count in.</param>
    /// <param name="phrase">The phrase to count.</param>
    /// <returns>The count.</returns>
    private static int Occurrences(string text, string phrase)
    {
        var count = 0;

        for (var at = text.IndexOf(phrase, StringComparison.Ordinal); at >= 0;
            at = text.IndexOf(phrase, at + phrase.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// The page as one line, so a sentence the page wraps is still one string.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <returns>Its words, separated by single spaces.</returns>
    private static string Flattened(string page) =>
        string.Join(' ', page.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // The file a clone checked out, rather than a copy carried next to the test
    // assembly. What this is about is the bytes a reader opens.
    private static string Read(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"{relativePath} was not found, looked in {path}");

        return File.ReadAllText(path);
    }

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
