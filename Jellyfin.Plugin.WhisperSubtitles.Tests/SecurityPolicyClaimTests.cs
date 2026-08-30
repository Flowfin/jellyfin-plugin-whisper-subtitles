using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The security policy opens by telling a reporter what a server built from this
/// tree actually reaches, and names three parts of the pipeline it says nothing
/// here calls. Both halves of that are facts about the plugin's sources, and the
/// sources are what the paragraph can be left behind by.
/// </summary>
/// <remarks>
/// IT HAD ALREADY HAPPENED WHEN THIS WAS WRITTEN. The section said nothing in the
/// plugin's own source called the audio extractor, the item selection or the
/// subtitle publisher, and that those were reached only from the test suite. It was
/// true the day it was written. The dry run landed on 2026-08-26 calling
/// <c>ItemSelection.Select</c>, the policy was edited again on 2026-08-30 without
/// that sentence being re-read, and it went on denying a call this plugin makes.
///
/// The direction that costs the most is the one that happened, and it is the same
/// one <c>BackendSettingsClaimTests</c> is about: a reporter reading this file is
/// told a code path is reached only from tests, so a surface they might have looked
/// at is one they have been told does not exist. Understating the tree is the class
/// the policy itself names further down as worth an issue rather than a report, and
/// both times it has understated it in the paragraph a reporter reads first.
///
/// THE TWO LEGS FAIL IN OPPOSITE DIRECTIONS AND THAT IS THE POINT. While a caller
/// exists, this section has to name it, so a second one arriving turns this red
/// rather than being dropped in silence. While the task reaches none of the three,
/// this section has to go on denying a run; the moment it reaches one, the denial
/// has to be gone, so the sentence cannot survive the arrival of the run it is
/// about. Neither state passes both legs while the text is wrong.
///
/// WHAT THIS DOES NOT DO, and the second bound is the one to read.
///
/// It reads names in source text. A line whose trimmed form opens a comment is
/// dropped, which is how the two doc comments mentioning the extractor stay out of
/// the caller set, and a mention written any other way would be counted as a call.
/// It cannot see a call made by reflection, and it cannot see one made through an
/// interface whose implementation nothing names.
///
/// The walk is per TYPE and never per member, so a type the task reaches for one
/// reason is reached for all of them. Both real backends are in its answer, because
/// selection asks each candidate what it is missing; that says nothing about their
/// transcription entry points, and this class has no opinion about the policy's own
/// sentences on child processes and requests. What it holds is the three names it
/// lists and the denial sentence, and the prose around them is a reading rather
/// than a comparison.
///
/// It reads the checkout rather than what git tracks, for the reason its
/// neighbours give: the bytes a reporter is handed are the bytes in the file they
/// open.
/// </remarks>
public sealed class SecurityPolicyClaimTests
{
    private const string Policy = "SECURITY.md";

    private const string PluginProject = "Jellyfin.Plugin.WhisperSubtitles";

    /// <summary>
    /// The heading this class is about. What it holds is the text from that line to
    /// the next heading of the same level.
    /// </summary>
    private const string Heading = "## What is actually running today";

    /// <summary>
    /// The type a server calls, and the one name the walk below starts from.
    /// </summary>
    private const string Entry = "SubtitleGenerationTask";

    /// <summary>
    /// The clause saying a run reaches none of the pipeline. It is quoted rather
    /// than described because what has to disappear on the day a run exists is a
    /// sentence and not an idea.
    /// </summary>
    private const string Denial = "no audio is extracted";

    /// <summary>
    /// The call the policy quotes, and the one this compares its paste against.
    /// </summary>
    private const string Call = "ItemSelection.Select";

    private const string PolicyCommand =
        "$ git grep 'ItemSelection.Select' -- 'Jellyfin.Plugin.WhisperSubtitles/*.cs'";

    /// <summary>
    /// The three the section names. They are the parts that read a library item,
    /// turn it into audio and put a file back, which is the whole of what a reporter
    /// is being told nothing reaches.
    /// </summary>
    private static readonly string[] _pipeline = ["AudioExtractor", "ItemSelection", "SubtitlePublisher"];

    // A type name as this tree writes one. It matches more than types, which costs
    // nothing: only a name that is also the base name of a source file in this
    // project is followed.
    private static readonly Regex _name = new(
        @"\b[A-Z][A-Za-z0-9_]*\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_reader_finds_the_section_and_the_sources_rather_than_comparing_nothing()
    {
        // Guards every leg below. A reader that found an empty section, or that
        // followed no name out of the task, would report a policy agreeing with the
        // tree whatever either of them said, and it would do it in green.
        var section = Section();
        var sources = PluginSources();
        var reached = ReachedFromTheTask();

        Assert.True(
            section.Length > 0,
            $"{Policy} no longer carries \"{Heading}\", so the section this judges is not there to judge");

        Assert.True(sources.Count > 1, $"the plugin project gave {sources.Count} source file name(s) to walk");

        foreach (var part in _pipeline)
        {
            Assert.True(
                sources.ContainsKey(part),
                $"the section names {part} and this project declares no such type, so the name it denies a caller of means nothing");
        }

        Assert.True(
            reached.Count > 1,
            $"following names out of {Entry} reached {reached.Count} type(s) of this plugin, so the walk below is answering about nothing");
    }

    [Fact]
    public void The_section_names_every_plugin_source_that_reaches_a_pipeline_entry_point()
    {
        var section = Section();

        foreach (var caller in CallersOfAPipelineEntryPoint())
        {
            Assert.True(
                section.Contains(caller, StringComparison.Ordinal),
                $"{caller} names one of {string.Join(", ", _pipeline)} on a line that is not a comment, and \"{Heading}\" does not mention it. That section tells a reporter which of this plugin's parts are reached only from the suite, so a caller it does not name is a code path somebody has been told is not there.");
        }
    }

    [Fact]
    public void The_section_denies_a_run_only_while_the_task_reaches_none_of_the_pipeline()
    {
        var section = Section();
        var reached = ReachedFromTheTask();
        var joined = _pipeline.Where(reached.Contains).ToList();

        if (joined.Count == 0)
        {
            Assert.True(
                section.Contains(Denial, StringComparison.Ordinal),
                $"nothing the task reaches is one of {string.Join(", ", _pipeline)}, and \"{Heading}\" has stopped saying \"{Denial}\". A reporter is then owed that reading and does not get it, which is a negative disclosure disappearing while it is still true.");

            return;
        }

        Assert.False(
            section.Contains(Denial, StringComparison.Ordinal),
            $"{Entry} now reaches {string.Join(", ", joined)}, and \"{Heading}\" still says \"{Denial}\". That sentence is the one a reporter decides on, and a run arriving under it is exactly the change that has to take this section with it.");
    }

    [Fact]
    public void The_policy_paste_prints_what_the_command_prints()
    {
        var found = CallSitesOfTheSelection();
        var pasted = PastedUnder(Read(Policy), PolicyCommand);

        Assert.True(
            pasted.SequenceEqual(found, StringComparer.Ordinal),
            $"{Policy} pastes {Show(pasted)} under \"{PolicyCommand}\" and the plugin answers {Show(found)}. The sentence above that paste is what tells a reporter the selection sits behind no route a server takes, so the two disagreeing is that sentence describing a different tree.");
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // One clone checks this file out with carriage returns and another does not,
        // and neither is wrong: `.gitattributes` stores a line feed and lets the
        // checkout decide. What has to be true is that the answer does not move.
        var asLineFeeds = Read(Policy).Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.NotEmpty(SectionOf(asCarriageReturns));
        Assert.Equal(SectionOf(asLineFeeds), SectionOf(asCarriageReturns));

        Assert.Equal(
            PastedUnder(asLineFeeds, PolicyCommand),
            PastedUnder(asCarriageReturns, PolicyCommand));
    }

    /// <summary>
    /// The section of the policy this class judges, out of the file a clone checked
    /// out.
    /// </summary>
    /// <returns>The text from the heading to the next heading of the same level, or an empty string where the heading is gone.</returns>
    private static string Section() => SectionOf(Read(Policy));

    /// <summary>
    /// The same section, out of text handed in.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <returns>The section, with its line endings normalised.</returns>
    private static string SectionOf(string page)
    {
        var lines = page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var at = Array.FindIndex(lines, line => line.Trim().Equals(Heading, StringComparison.Ordinal));

        if (at < 0)
        {
            return string.Empty;
        }

        var taken = new List<string>();

        for (var index = at + 1; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            taken.Add(lines[index].TrimEnd());
        }

        return string.Join('\n', taken);
    }

    /// <summary>
    /// Every source file of the plugin project, by the name it declares.
    /// </summary>
    /// <remarks>
    /// A list of paths per name rather than one path, because two files sharing a
    /// base name is a thing this tree could grow and silently dropping one of them
    /// would take a branch of the walk with it.
    /// </remarks>
    /// <returns>The file base name against the paths carrying it.</returns>
    private static Dictionary<string, List<string>> PluginSources()
    {
        var root = Path.Combine(RepositoryRoot(), PluginProject);

        Assert.True(Directory.Exists(root), $"there is nothing to walk at {root}");

        var sources = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');

            if (relative.Contains("/bin/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);

            if (!sources.TryGetValue(name, out var paths))
            {
                paths = [];
                sources[name] = paths;
            }

            paths.Add(path);
        }

        return sources;
    }

    /// <summary>
    /// The types of this plugin a reader reaches by following names out of the task.
    /// </summary>
    /// <returns>Their names, sorted, so a message reads the same on every machine.</returns>
    private static SortedSet<string> ReachedFromTheTask()
    {
        var sources = PluginSources();
        var reached = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();

        pending.Push(Entry);

        while (pending.Count > 0)
        {
            var name = pending.Pop();

            if (!sources.TryGetValue(name, out var paths) || !reached.Add(name))
            {
                continue;
            }

            foreach (var found in paths.SelectMany(path => _name.Matches(CodeIn(path)).Select(match => match.Value)))
            {
                if (sources.ContainsKey(found) && !reached.Contains(found))
                {
                    pending.Push(found);
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// Every source of this plugin naming one of the pipeline entry points outside
    /// the file that declares it.
    /// </summary>
    /// <returns>Their file names, sorted.</returns>
    private static SortedSet<string> CallersOfAPipelineEntryPoint()
    {
        var callers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var source in PluginSources())
        {
            foreach (var code in source.Value.Select(CodeIn))
            {
                foreach (var part in _pipeline)
                {
                    if (string.Equals(part, source.Key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(
                        code,
                        @"\b" + part + @"\b",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromSeconds(5)))
                    {
                        callers.Add(source.Key);
                    }
                }
            }
        }

        return callers;
    }

    /// <summary>
    /// What the command the policy quotes returns, as that command prints it.
    /// </summary>
    /// <remarks>
    /// The line number is deliberately not part of this, for the reason the README's
    /// comparison and the backend settings claim both give: a paste carrying one
    /// goes stale on an edit anywhere above the line it quotes, which is a direction
    /// that drifts without anything having changed about the subject.
    /// </remarks>
    /// <returns>One entry per matching line, path first.</returns>
    private static List<string> CallSitesOfTheSelection()
    {
        var root = Path.Combine(RepositoryRoot(), PluginProject);

        return [.. PluginSources()
            .SelectMany(source => source.Value)
            .Select(path => (Path: path, Relative: System.IO.Path.GetRelativePath(root, path).Replace('\\', '/')))
            .SelectMany(file => File
                .ReadAllLines(file.Path)
                .Where(line => line.Contains(Call, StringComparison.Ordinal))
                .Select(line => PluginProject + "/" + file.Relative + ":" + line.TrimEnd()))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// A source file with its comment lines taken out.
    /// </summary>
    /// <remarks>
    /// A line whose trimmed form opens a comment, and nothing finer. The two doc
    /// comments naming the extractor are written that way and so is every other
    /// comment in this project, and a mention parked at the end of a line of code
    /// would be counted as a call, which errs towards asking rather than towards
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
    /// The indented block a page writes under a command it quotes.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <param name="command">The command line, as the page writes it.</param>
    /// <returns>The pasted lines, trimmed of their indentation.</returns>
    private static List<string> PastedUnder(string page, string command)
    {
        var lines = page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var at = Array.FindIndex(lines, line => line.Trim().Equals(command, StringComparison.Ordinal));

        Assert.True(
            at >= 0,
            $"{Policy} no longer quotes \"{command}\". Either the command moved or its wording did, and the paste under it is then held by nothing.");

        var pasted = new List<string>();

        for (var index = at + 1; index < lines.Length; index++)
        {
            var line = lines[index];

            if (!line.StartsWith("    ", StringComparison.Ordinal) || line.Trim().Length == 0)
            {
                break;
            }

            pasted.Add(line.Trim());
        }

        return pasted;
    }

    private static string Show(List<string> lines) =>
        lines.Count == 0 ? "nothing" : "[" + string.Join(", ", lines) + "]";

    // The file a clone checked out, rather than a copy carried next to the test
    // assembly. What this is about is the bytes a reporter is given.
    private static string Read(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath);

        Assert.True(File.Exists(path), $"{relativePath} was not found, looked in {path}");

        return File.ReadAllText(path);
    }

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
