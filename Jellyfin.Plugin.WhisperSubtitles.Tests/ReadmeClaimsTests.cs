using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The README separates what this tree holds from what it does not by handing the
/// reader a command and pasting what that command prints. Three of those pastes are
/// read here against the checkout they are about.
/// </summary>
/// <remarks>
/// The failure this is written against has already happened twice on the same page,
/// in the same direction, and neither instance was noticed by anybody working on the
/// change that caused it. The page said no file implemented a server task and no file
/// named a network type, printed an empty result under each claim, and both searches
/// had been returning files for weeks. It was eventually recorded in `SECURITY.md`,
/// which is a different document noticing that this one had gone stale.
///
/// That is the shape to prevent rather than the two sentences. A paste is a claim
/// about another artefact made at the moment of writing, and the artefact keeps
/// moving afterwards; the change that moves it is a change to source, and the person
/// making it has no reason to open a document at the root. So the paste is compared
/// against the search rather than trusted, which catches drift in both directions at
/// once: a file that appeared and is not listed, and a file that is listed and has
/// gone.
///
/// WHAT THIS DOES NOT DO. It reads three pastes and not every claim on the page. The
/// release and tag readings above them are about a server this suite may not reach,
/// which is the rule the whole suite is held to, so they stay a reader's job. And it
/// says nothing about whether the prose around a paste is right; a paragraph that
/// lists the correct files under a sentence drawing the wrong conclusion from them
/// passes here.
///
/// The search terms of the two whole-tree searches are assembled from parts rather
/// than written whole, which looks fussy and is not. A test naming the token it
/// searches for is a file containing that token, and the first of those two walks
/// the whole tree, so a literal here would put this file into its own result set and
/// make the page wrong for having been checked. `DeterminismTests` splits a literal
/// for the same reason. The third search reads one named file and this is not it, so
/// its tokens are written whole.
/// </remarks>
public class ReadmeClaimsTests
{
    /// <summary>
    /// The interface a Jellyfin server task implements, assembled rather than
    /// written, for the reason in the remarks above.
    /// </summary>
    private const string TaskType = "IScheduled" + "Task";

    /// <summary>
    /// The project the network search is scoped to, which is also the scope the page
    /// prints beside the command.
    /// </summary>
    private const string PluginProject = "Jellyfin.Plugin.WhisperSubtitles";

    /// <summary>
    /// The type behind the remote backend paragraph, and the file the paragraph has
    /// to name so a reader can go and look.
    /// </summary>
    /// <remarks>
    /// Written whole rather than assembled, unlike the two searches above. Those
    /// walk the whole tree and would otherwise return this file; these two are
    /// scoped to the plugin project and to one source under it, and this file is in
    /// neither.
    /// </remarks>
    private const string RemoteBackendType = "class RemoteWhisperBackend";

    /// <summary>
    /// The file the remote backend lives in, as the paragraph has to write it.
    /// </summary>
    private const string RemoteBackendFile =
        "Jellyfin.Plugin.WhisperSubtitles/Backends/Remote/RemoteWhisperBackend.cs";

    /// <summary>
    /// The words that pick the remote backend's paragraph out of the page.
    /// </summary>
    private const string RemoteBackendSubject = "OpenAI audio transcription API";

    /// <summary>
    /// The source holding the scheduled task, which is where the joining #183 owes
    /// would appear.
    /// </summary>
    private const string TaskSource = "Scheduling/SubtitleGenerationTask.cs";

    /// <summary>
    /// The heading whose opening paragraph says what can be walked today.
    /// </summary>
    private const string InstallPathHeading = "## From an install to a first subtitle";

    /// <summary>
    /// The issue that holds the joining, named so a reader meets it where the
    /// absence is stated rather than having to search for it.
    /// </summary>
    private const string JoiningIssue = "#183";

    /// <summary>
    /// The page an operator chooses a backend on, named on the page so a reader
    /// can go and look rather than take the claim on trust.
    /// </summary>
    private const string ConfigurationPageFile =
        "Jellyfin.Plugin.WhisperSubtitles/Configuration/configPage.html";

    /// <summary>
    /// The words the install path filed that page under while it was still to come.
    /// </summary>
    private const string PageFiledAsStillToCome = "The page is #36";

    /// <summary>
    /// The character a line is split on, named rather than escaped so this file
    /// does not carry one inside a literal. Every line is trimmed afterwards, so a
    /// clone that put carriage returns back parses the page the same way, which is
    /// the case `TroubleshootingPageTests` states at length for its own page.
    /// </summary>
    private const char LineFeed = (char)10;

    /// <summary>
    /// The file the two server line pins are written in, which the page's third
    /// paste is a reading of.
    /// </summary>
    private const string PinFile = "Directory.Build.props";

    /// <summary>
    /// The network types the page's second search names, each assembled for the same
    /// reason as the one above.
    /// </summary>
    private static readonly string[] NetworkTypes =
    [
        "Http" + "Client",
        "Web" + "Request",
        "Sock" + "et",
    ];

    /// <summary>
    /// The words the page uses to say a thing is not here. Read against the two
    /// paragraphs below rather than against the page, because the same words are
    /// correct wherever they describe something that really is absent.
    /// </summary>
    private static readonly string[] Absences =
    [
        "does not exist",
        "does not yet exist",
        "is not there",
        "not yet built",
    ];

    /// <summary>
    /// The parts of the pipeline a joined run reaches, which is the list #183 uses
    /// to say the task reaches none of them.
    /// </summary>
    private static readonly string[] PipelineTypes =
    [
        "ItemSelection",
        "AudioExtractor",
        "BoundedRun",
        "SubtitlePublisher",
        "TranscriptionRequest",
    ];

    /// <summary>
    /// The properties that third paste searches for.
    /// </summary>
    /// <remarks>
    /// Written whole rather than assembled. That search is scoped to one file and
    /// this one is not it, so a literal here cannot put this file into the result
    /// set the way the two whole-tree searches above would.
    /// </remarks>
    private static readonly string[] PinProperties =
    [
        "SupportedServerLines",
        "JellyfinServerLine",
        "JellyfinPackageVersion",
    ];

    /// <summary>
    /// The first search, quoted as the page quotes it. Built from the same constant
    /// the walk below uses, so a command repointed on the page stops being found
    /// here rather than being found and answered from somewhere else.
    /// </summary>
    private static string TaskCommand => "$ git grep -l " + TaskType + " -- '*.cs'";

    /// <summary>
    /// The second search, built the same way.
    /// </summary>
    private static string NetworkCommand =>
        "$ git grep -ln '" + string.Join("\\|", NetworkTypes) + "' -- '" + PluginProject + "/*'";

    /// <summary>
    /// The third search, built the same way.
    /// </summary>
    private static string PinCommand =>
        "$ grep '" + string.Join("\\|", PinProperties) + "' " + PinFile;

    [Fact]
    public void The_task_search_prints_what_the_readme_pastes_under_it()
    {
        var found = SourcesNaming(RepositoryRoot(), [TaskType]);
        var pasted = PastedUnder(TaskCommand);

        Assert.True(
            pasted.SequenceEqual(found, StringComparer.Ordinal),
            $"README.md pastes {Show(pasted)} under \"{TaskCommand}\" and the tree answers {Show(found)}. The page is describing a different tree from the one it is in.");
    }

    [Fact]
    public void The_network_search_prints_what_the_readme_pastes_under_it()
    {
        var project = Path.Combine(RepositoryRoot(), PluginProject);
        var found = SourcesNaming(project, NetworkTypes)
            .Select(path => PluginProject + "/" + path)
            .ToList();
        var pasted = PastedUnder(NetworkCommand);

        Assert.True(
            pasted.SequenceEqual(found, StringComparer.Ordinal),
            $"README.md pastes {Show(pasted)} under \"{NetworkCommand}\" and the plugin answers {Show(found)}. A file that reaches a network without the page listing it and a file the page lists that does not are both this, and the two lists say which.");
    }

    /// <summary>
    /// The page's third paste is a reading of the file the two server line pins are
    /// written in, and this reads it against that file rather than trusting it.
    /// </summary>
    /// <remarks>
    /// IT HAD ALREADY GONE STALE, WHICH IS WHY IT IS HERE. The paste was true when it
    /// was written and stopped being true five days later, when a change to the pin
    /// file moved every line the paste quoted a number for. A second change moved
    /// them again a week after that. Neither had a reason to open a page at the root,
    /// which is the shape the remarks on this class already describe for the two
    /// pastes above; this is the one it left unread.
    ///
    /// THE NUMBERS WENT WITH THE REPAIR AND THAT IS PART OF IT. A paste carrying line
    /// numbers goes stale on any edit anywhere above the lines it quotes, so the
    /// command drops them and the page quotes the matching lines alone. That leaves
    /// this comparing what the file says rather than where it says it, which is what
    /// the paragraph around it is about, and it removes the direction that drifts
    /// without anything having changed about the pins.
    ///
    /// WHAT THIS DOES NOT DO. It compares the matching lines and has no opinion about
    /// the prose beside them: a paste that reproduces exactly, under a sentence
    /// drawing the wrong conclusion from it, passes here. It reads the pin file and
    /// not the build, so a pin the page and the file agree on that no build ever used
    /// is not this test's subject.
    /// </remarks>
    [Fact]
    public void The_server_line_search_prints_what_the_readme_pastes_under_it()
    {
        var found = LinesNaming(PinFile, PinProperties);
        var pasted = PastedUnder(PinCommand);

        Assert.NotEmpty(found);

        Assert.True(
            pasted.SequenceEqual(found, StringComparer.Ordinal),
            $"README.md pastes {Show(pasted)} under \"{PinCommand}\" and {PinFile} answers {Show(found)}. A pin raised in the file and not on the page, and a pin the page carries that the file does not, are both this.");
    }

    /// <summary>
    /// The page filed the remote backend as decided and not yet built for two weeks
    /// after it landed, under a closed issue, and this reads that paragraph against
    /// the plugin project instead.
    /// </summary>
    /// <remarks>
    /// It is the safer of the two directions a state marker can be wrong in, and it
    /// still costs something. A backend filed as unbuilt is one a reader stops
    /// looking for, and the disclosure #81 asks for is then read as a plan rather
    /// than as something owed about code an operator can already select.
    ///
    /// WHAT THIS DOES NOT DO. It reads one direction. A page that goes on saying the
    /// backend is here after the type is deleted would need the type gone to be
    /// exercised, which is a build failure rather than a red test, so that arm is
    /// stated and not proved. It has no opinion about the rest of the paragraph:
    /// sentences filed correctly around a false one pass. And it asks the question of
    /// a paragraph rather than of a sentence, which is the hole `docs/limits.md` and
    /// its checks already carry one grain further down.
    /// </remarks>
    [Fact]
    public void The_page_files_the_remote_backend_in_the_state_the_tree_holds_it_in()
    {
        var paragraph = ParagraphSaying(RemoteBackendSubject);
        var inTree = SourcesNaming(PluginProjectPath(), [RemoteBackendType]);

        Assert.NotEmpty(inTree);

        foreach (var denial in Absences)
        {
            Assert.False(
                paragraph.Contains(denial, StringComparison.Ordinal),
                $"README.md says \"{denial}\" of the remote backend while the plugin project holds it, in {Show(inTree)}. A backend filed as unbuilt is one a reader stops looking for, and the disclosure #81 asks for is about code that is here rather than about a plan.");
        }

        Assert.True(
            paragraph.Contains(RemoteBackendFile, StringComparison.Ordinal),
            $"README.md says the remote backend is here and does not say where. A reader is asked to take it on trust instead of being sent to the file: {paragraph}");
    }

    /// <summary>
    /// The page pasted a search listing the scheduled task and then opened a later
    /// section by saying the task does not exist. This refuses the second while the
    /// first is true.
    /// </summary>
    /// <remarks>
    /// This is the dangerous direction rather than the safe one. The two sentences
    /// are ninety lines apart, both read as statements about the same tree, and the
    /// one a reader meets when they are deciding whether to install is the wrong one.
    /// The paste above it is already compared against the tree, so the page was
    /// disagreeing with a claim this suite holds.
    ///
    /// The second arm is the other half of the same sentence. What is missing is the
    /// run, so while the task source reaches no part of the pipeline the paragraph
    /// has to name the issue that holds the joining. An absence stated with nothing
    /// beside it is one a reader cannot follow up.
    ///
    /// WHAT THIS DOES NOT DO. It reads the opening paragraph of one section and no
    /// other, so the same denial written further down passes. It matches the words a
    /// denial is written in rather than the meaning, so a sentence saying the same
    /// thing in other words passes and a rewording of the repaired sentence turns
    /// this red instead. And the arm that would fire once the run exists, a page
    /// still saying none of it can be walked, is not this: what lifts here is only
    /// the requirement to name the issue.
    /// </remarks>
    [Fact]
    public void The_install_path_does_not_deny_a_task_the_page_has_already_listed()
    {
        var paragraph = OpeningParagraphOf(InstallPathHeading);
        var task = SourcesNaming(RepositoryRoot(), [TaskType]);

        Assert.NotEmpty(task);

        foreach (var denial in Absences)
        {
            Assert.False(
                paragraph.Contains(denial, StringComparison.Ordinal),
                $"README.md opens \"{InstallPathHeading}\" by saying \"{denial}\" while the page's own search lists {Show(task)}. The page is disagreeing with the paste it prints ninety lines above.");
        }

        var joined = SourcesNaming(PluginProjectPath(), PipelineTypes)
            .Where(path => path.EndsWith(TaskSource, StringComparison.Ordinal))
            .ToList();

        if (joined.Count == 0)
        {
            Assert.True(
                paragraph.Contains(JoiningIssue, StringComparison.Ordinal),
                $"README.md says the path cannot be walked and names nothing holding the joining, while {TaskSource} reaches none of {string.Join(", ", PipelineTypes)}. An absence with no issue beside it is one a reader cannot follow up: {paragraph}");
        }
    }

    /// <summary>
    /// The page said what this tree holds and left the configuration page out of
    /// it, then told an operator to open that page and filed it as an issue.
    /// </summary>
    /// <remarks>
    /// This is the direction that costs the reader something. A thing filed as
    /// still to come is one they stop looking for, and here it is the surface the
    /// step they are reading asks them to use. It also makes what #15 and #81 owe
    /// read as plans for a page that has not arrived, when the page is there and
    /// what is missing is what it shows.
    ///
    /// Both arms are needed because the two sites fail differently. The list is an
    /// omission, which no wording rule catches; the step is a claim, and it is
    /// matched by the words it was written in.
    ///
    /// WHAT THIS DOES NOT DO. The tree side is the line the page saves the setting
    /// with, so nothing here boots a server or opens a dashboard. It matches the
    /// step's old sentence rather than its meaning, so the same claim in other
    /// words passes and a rewording of the repaired sentence turns this red
    /// instead. And it asks that the list name the file, never that what it says
    /// about it is true.
    /// </remarks>
    [Fact]
    public void The_page_does_not_file_the_configuration_page_as_something_still_to_come()
    {
        ConfigurationPageSource.RefuseUnlessAnOperatorChoosesTheBackendOnIt();

        var holdings = ParagraphSaying("What exists is");
        var step = ParagraphSaying("Open the plugin's page in the dashboard");

        Assert.True(
            holdings.Contains(ConfigurationPageFile, StringComparison.Ordinal),
            $"README.md lists what this tree holds and does not name {ConfigurationPageFile}, which this plugin registers and an operator chooses a backend on: {holdings}");

        Assert.False(
            step.Contains(PageFiledAsStillToCome, StringComparison.Ordinal),
            $"README.md tells an operator to open the plugin's page and then says \"{PageFiledAsStillToCome}\", which files the page they were just sent to as something still to come: {step}");
    }

    [Fact]
    public void The_page_and_the_search_it_quotes_are_about_the_same_files()
    {
        // The page hands a reader a command and this reads the tree by walking it.
        // Where the two ask different questions they can both look right and
        // disagree, and the reader trusts the one they can run, so the scope the
        // second command prints is asserted against the directory that is walked.
        Assert.Contains(PluginProject + "/*", NetworkCommand, StringComparison.Ordinal);
        Assert.True(
            Directory.Exists(Path.Combine(RepositoryRoot(), PluginProject)),
            $"the network search is scoped to {PluginProject} and there is no such directory to walk");
    }

    /// <summary>
    /// The paragraph of the page containing a given phrase.
    /// </summary>
    /// <remarks>
    /// A paragraph rather than a sentence, because the state a thing is filed in and
    /// the reason for it are written in neighbouring sentences and a reader takes
    /// them together. A second claim inside one paragraph is covered by whatever the
    /// paragraph says about the first, which is a hole this shares with the state
    /// markers on `docs/limits.md` and does not close.
    /// </remarks>
    /// <param name="phrase">A phrase only the wanted paragraph carries.</param>
    /// <returns>The paragraph, with its line breaks turned into spaces.</returns>
    private static string ParagraphSaying(string phrase)
    {
        var paragraphs = Blocks()
            .Where(block => block.Contains(phrase, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            paragraphs.Count == 1,
            $"README.md has {paragraphs.Count} paragraphs saying \"{phrase}\" and this reads one. A phrase that stopped being unique leaves whichever paragraph it picked unread.");

        return Flatten(paragraphs[0]);
    }

    /// <summary>
    /// The first paragraph under a heading.
    /// </summary>
    /// <param name="heading">The heading line, as the page writes it.</param>
    /// <returns>The paragraph, with its line breaks turned into spaces.</returns>
    private static string OpeningParagraphOf(string heading)
    {
        var blocks = Blocks();
        var at = blocks.FindIndex(block => block.Trim().Equals(heading, StringComparison.Ordinal));

        Assert.True(
            at >= 0,
            $"README.md no longer carries the heading \"{heading}\", so the paragraph under it is held by nothing.");
        Assert.True(at + 1 < blocks.Count, $"\"{heading}\" is the last thing on the page and has no paragraph under it.");

        return Flatten(blocks[at + 1]);
    }

    /// <summary>
    /// The page split into blank-line separated blocks, with the line endings a
    /// checkout may have put back taken off first, for the reason the class remarks
    /// give for the same normalisation above.
    /// </summary>
    /// <returns>The blocks, in the order the page writes them.</returns>
    private static List<string> Blocks() =>
        [.. Lines().Aggregate(
            new List<string> { string.Empty },
            (blocks, line) =>
            {
                if (line.Length == 0)
                {
                    blocks.Add(string.Empty);
                }
                else
                {
                    blocks[^1] = blocks[^1].Length == 0 ? line : blocks[^1] + " " + line;
                }

                return blocks;
            })];

    /// <summary>
    /// The page as lines, with any carriage return removed.
    /// </summary>
    /// <returns>Every line of the page.</returns>
    private static IEnumerable<string> Lines() =>
        Page().Split(LineFeed).Select(line => line.Trim());

    private static string Flatten(string block) => block.Trim();

    private static string PluginProjectPath() => Path.Combine(RepositoryRoot(), PluginProject);

    /// <summary>
    /// The lines pasted immediately under a command on the page, trimmed, in the
    /// order they appear.
    /// </summary>
    /// <remarks>
    /// The page indents a command and its output together, so the output is the run
    /// of indented lines after the command line and stops at the first line that is
    /// not one. An empty run is a paste claiming the command prints nothing, which is
    /// a claim like any other and is compared like one.
    /// </remarks>
    /// <param name="command">The command line as the page writes it.</param>
    /// <returns>Each pasted line, trimmed.</returns>
    private static List<string> PastedUnder(string command)
    {
        var lines = Page().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var at = Array.FindIndex(lines, line => line.Trim().Equals(command, StringComparison.Ordinal));

        Assert.True(
            at >= 0,
            $"README.md no longer quotes \"{command}\". Either the command moved or its wording did, and the paste under it is then held by nothing.");

        var pasted = new List<string>();

        for (var i = at + 1; i < lines.Length; i++)
        {
            var line = lines[i];

            if (!line.StartsWith("    ", StringComparison.Ordinal) || line.Trim().Length == 0)
            {
                break;
            }

            pasted.Add(line.Trim());
        }

        return pasted;
    }

    /// <summary>
    /// The source files under a root that name any of the given tokens, by path
    /// relative to that root, ordered as a search over the tree orders them.
    /// </summary>
    /// <remarks>
    /// Read off the checkout rather than off a compiled assembly, because a mention
    /// in a comment counts: the question the page asks is whether this tree holds the
    /// thing at all, and a file naming it in prose is a file where somebody started.
    /// Build output is excluded because it is not what a search over tracked files
    /// returns and it is not what a reader running the command would see.
    /// </remarks>
    /// <param name="root">The directory to walk.</param>
    /// <param name="tokens">The tokens a file has to name one of.</param>
    /// <returns>Relative paths with forward slashes, ordinal ordered.</returns>
    private static List<string> SourcesNaming(string root, IReadOnlyList<string> tokens)
    {
        Assert.True(Directory.Exists(root), $"there is nothing to walk at {root}");

        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);

        return sources
            .Select(path => (Path: path, Relative: Path.GetRelativePath(root, path).Replace('\\', '/')))
            .Where(file => !file.Relative.Contains("/bin/", StringComparison.Ordinal)
                && !file.Relative.Contains("/obj/", StringComparison.Ordinal))
            .Where(file => tokens.Any(token =>
                File.ReadAllText(file.Path).Contains(token, StringComparison.Ordinal)))
            .Select(file => file.Relative)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The lines of one file at the repository root that name any of the given
    /// tokens, trimmed, in the order the file writes them.
    /// </summary>
    /// <remarks>
    /// Trimmed on both sides, because the page indents a paste under the command it
    /// pastes and the reader of that paste trims it the same way. What survives the
    /// trim is the text of the line, which is what the paragraph is about.
    /// </remarks>
    /// <param name="name">The file, relative to the repository root.</param>
    /// <param name="tokens">The tokens a line has to name one of.</param>
    /// <returns>Each matching line, trimmed.</returns>
    private static List<string> LinesNaming(string name, IReadOnlyList<string> tokens)
    {
        var path = Path.Combine(RepositoryRoot(), name);

        Assert.True(File.Exists(path), $"the page reads {name} and there is no such file to read");

        return [.. File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(LineFeed)
            .Where(line => tokens.Any(token => line.Contains(token, StringComparison.Ordinal)))
            .Select(line => line.Trim())];
    }

    private static string Show(IEnumerable<string> paths)
    {
        var listed = string.Join(", ", paths);

        return listed.Length == 0 ? "nothing" : listed;
    }

    /// <summary>
    /// The README, read out of the checkout rather than out of a copy beside the
    /// assembly, for the reason its neighbours in this suite give.
    /// </summary>
    /// <returns>The whole page.</returns>
    private static string Page() => File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
