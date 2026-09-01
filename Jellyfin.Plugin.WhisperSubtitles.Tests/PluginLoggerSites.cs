using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The one reading of whether this plugin has acquired a logger.
/// </summary>
/// <remarks>
/// Two documents state the same absence and each of them is read against this. A
/// second implementation of the search would let the two pages be judged by two
/// slightly different questions, which is how one of them gets repaired and the
/// other does not on the day a logger arrives - the failure the pages are checked
/// against in the first place.
///
/// The population is the plugin project rather than the tree, and deliberately so.
/// What both pages are about is what this plugin says in the server's log; a test
/// double naming a logger is not the plugin logging, and a search over the tree
/// would also read the files these checks are written in.
/// </remarks>
internal static class PluginLoggerSites
{
    /// <summary>
    /// The type a logger arrives as, which is what both pages' claims are about.
    /// </summary>
    internal const string LoggerType = "ILogger";

    /// <summary>
    /// The plugin source files that name a logger type, by path relative to the
    /// project.
    /// </summary>
    /// <remarks>
    /// Read off the checkout rather than off the compiled assembly, because a
    /// reference in a comment or in a remark counts here: the question is whether
    /// this plugin has acquired a logger, and a file that names one in prose is a
    /// file where somebody has started. Build output is left out because it is not
    /// what a search over tracked files returns and not what a reader running the
    /// command beside either page would see.
    /// </remarks>
    /// <returns>Relative paths with forward slashes, ordinal ordered.</returns>
    internal static List<string> All()
    {
        var project = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles");

        Assert.True(Directory.Exists(project), $"the plugin project was not found at {project}");

        var sources = Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);

        return [.. sources
            .Select(path => (Path: path, Relative: Path.GetRelativePath(project, path).Replace('\\', '/')))
            .Where(file => !file.Relative.StartsWith("bin/", StringComparison.Ordinal)
                && !file.Relative.StartsWith("obj/", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file.Path).Contains(LoggerType, StringComparison.Ordinal))
            .Select(file => file.Relative)
            .Order(StringComparer.Ordinal)];
    }

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
