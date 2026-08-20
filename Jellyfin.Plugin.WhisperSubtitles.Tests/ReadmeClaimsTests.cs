using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The README separates what this tree holds from what it does not by handing the
/// reader a command and pasting what that command prints. Two of those pastes are
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
/// WHAT THIS DOES NOT DO. It reads two pastes and not every claim on the page. The
/// release and tag readings above them are about a server this suite may not reach,
/// which is the rule the whole suite is held to, so they stay a reader's job. And it
/// says nothing about whether the prose around a paste is right; a paragraph that
/// lists the correct files under a sentence drawing the wrong conclusion from them
/// passes here.
///
/// The search terms are assembled from parts rather than written whole, which looks
/// fussy and is not. A test naming the token it searches for is a file containing
/// that token, and the first of these two searches walks the whole tree, so a literal
/// here would put this file into its own result set and make the page wrong for
/// having been checked. `DeterminismTests` splits a literal for the same reason.
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
