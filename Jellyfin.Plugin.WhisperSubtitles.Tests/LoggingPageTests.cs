using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The logging page carries a table of levels and a sentence saying nothing in the
/// plugin logs yet, so the table is a decision written down rather than a property
/// anything holds. That sentence is a claim about the tree, and it stops being true
/// at the first change that logs.
/// </summary>
/// <remarks>
/// The failure it is written against is the one the page itself argues about: a
/// table in a document that reads like a guarantee is worse than no table. Today
/// the page is safe because it says plainly that nothing holds it. The change that
/// adds the first logger is the change that makes the same page read as a
/// description of a running server, and it is exactly the change least likely to
/// remember a document two directories away.
///
/// So the sentence is read rather than trusted, in both directions. A page still
/// saying nothing logs while the plugin names a logger is the loud direction. A
/// page that quietly dropped the sentence while nothing logs is the other one, and
/// it leaves the table looking held by something.
///
/// WHAT THIS DOES NOT DO. It does not judge the table. Whether a run uses the level
/// a row names is what the tests in that issue's own done-condition are for, and
/// none of them can be written before something logs. This refuses one sentence
/// against one search, which is the whole of what a document can owe before the
/// thing it describes exists.
///
/// The population is the plugin project rather than the tree, and that is
/// deliberate rather than convenient. What the table is about is what the plugin
/// says in the server's log; a test double that mentions a logger is not the plugin
/// logging, and a check reading the whole tree would also read the file it is
/// written in.
/// </remarks>
public class LoggingPageTests
{
    /// <summary>
    /// The sentence the page states its own standing with, and the whole of what is
    /// machine-read out of that page.
    /// </summary>
    private const string NothingLogsYet = "This plugin does not log at all";

    private const string LoggerType = "ILogger";

    [Fact]
    public void The_page_says_nothing_logs_yet_exactly_while_nothing_does()
    {
        var naming = FilesNamingALogger();
        var says = Page().Contains(NothingLogsYet, StringComparison.Ordinal);

        Assert.True(
            !says || naming.Count == 0,
            $"docs/logging.md says \"{NothingLogsYet}\" and the plugin names {LoggerType} in {naming.Count} file(s): {string.Join(", ", naming)}. The table on that page now reads as a description of what a server logs, and no test holds a line of it.");

        Assert.True(
            says || naming.Count > 0,
            $"nothing under the plugin project names {LoggerType} and docs/logging.md no longer says so, so its table reads as a property something holds when it is still a decision written down.");
    }

    [Fact]
    public void The_page_and_the_search_it_quotes_are_about_the_same_files()
    {
        // The page hands a reader the command behind its own claim. A command
        // asking a different question from the one this leg asks would leave the
        // two disagreeing while both looked right, and the reader trusts the one
        // they can run.
        Assert.Contains("Jellyfin.Plugin.WhisperSubtitles/", Page(), StringComparison.Ordinal);
        Assert.Contains(LoggerType, Page(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The plugin source files that name a logger type, by path relative to the
    /// project.
    /// </summary>
    /// <remarks>
    /// Read off the checkout rather than off the compiled assembly, because a
    /// reference in a comment or in a remark counts here: the question is whether
    /// this plugin has acquired a logger, and a file that names one in prose is a
    /// file where somebody has started.
    /// </remarks>
    private static System.Collections.Generic.List<string> FilesNamingALogger()
    {
        var project = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles");

        Assert.True(Directory.Exists(project), $"the plugin project was not found at {project}");

        var sources = Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);

        return sources
            .Where(path => File.ReadAllText(path).Contains(LoggerType, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(project, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The logging page, read out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason its
    /// neighbours give: sources are not copied beside the assembly, and it is also
    /// the route that lets the plugin project be walked at all.
    /// </remarks>
    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "logging.md"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
