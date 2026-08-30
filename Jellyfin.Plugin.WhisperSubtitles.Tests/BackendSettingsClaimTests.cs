using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Two texts in this tree say what the configuration does not hold for the
/// backends: the security policy, under the heading that tells a reporter what is
/// actually running, and the remark beside the line that builds the local
/// backend's settings. Both are claims about the schema, and the schema is a file
/// either of them can be left behind by.
/// </summary>
/// <remarks>
/// IT HAS ALREADY HAPPENED, WHICH IS WHY THIS EXISTS. The remark said the schema
/// carried the backend name, the target language and the per-library targets and
/// nothing else, and the policy said no configuration field held a tool path. Both
/// were true when they were written. `LocalToolPath` and `LocalModelPath` landed on
/// 2026-08-29 in a change that touched the schema, the page an operator types into
/// and the backend guide, and neither of these two files was among them, so both
/// went on denying a setting an operator could already save.
///
/// The direction that costs the most is the one that happened. A reporter reading
/// the policy is told this plugin has no field to type a path into, so a surface
/// they might look at is one they have been told does not exist. That understates
/// the tree, which is the class the policy itself names further down as worth an
/// issue rather than a report, and here it understates it in the paragraph a
/// reporter reads first.
///
/// THE TWO LEGS FAIL IN OPPOSITE DIRECTIONS AND THAT IS THE POINT. While nothing
/// carries the configuration to the backends, the registrator has to name each path
/// the schema declares and drops, so a third path added to the schema turns this red
/// rather than being dropped in silence. The moment somebody does carry them, that
/// construction stops matching what the policy pastes under its command, and the
/// policy is held by nothing until it has been re-read. Neither state passes both
/// legs while the text is wrong.
///
/// WHAT THIS DOES NOT DO. It compares a paste and a set of names, and it has no
/// opinion about the prose around either: a paste that reproduces exactly, under a
/// sentence drawing the wrong conclusion from it, passes here. It reads the schema
/// file rather than the type, so a property added by a partial class elsewhere is
/// not seen. And it says nothing about the remote backend's settings, which have no
/// field in the schema to be left behind by yet.
/// </remarks>
public sealed class BackendSettingsClaimTests
{
    private const string PluginProject = "Jellyfin.Plugin.WhisperSubtitles";

    private const string SchemaFile = "Configuration/PluginConfiguration.cs";

    private const string Registrator = "PluginServiceRegistrator.cs";

    private const string Construction = "new LocalBackendOptions";

    private const string PolicyCommand =
        "$ git grep 'new LocalBackendOptions' -- 'Jellyfin.Plugin.WhisperSubtitles/*.cs'";

    private const string Policy = "SECURITY.md";

    // A settable property of the configuration, which is how the schema declares
    // every field the server deserialises into it.
    private static readonly Regex _declared = new(
        @"public\s+[\w<>\[\]?,\s]+?\s+(?<name>\w+)\s*\{\s*get;\s*set;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The settings the local backend is built out of are paths, and the schema names
    // them for what they are. Matching the shape rather than the two names is what
    // makes a third one this file's business on the day it lands.
    private static readonly Regex _backendPath = new(
        @"^Local\w*Path$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_reader_finds_the_schema_and_the_paste_rather_than_comparing_nothing()
    {
        // Guards every leg below. A reader that found no properties would report a
        // registrator naming every path the schema declares, whatever either of them
        // said, and it would do it in green.
        var declared = DeclaredSettings();
        var paths = BackendPaths();
        var pasted = PastedUnderPolicyCommand();

        Assert.True(declared.Count > 1, $"{SchemaFile} gave {declared.Count} settable property or properties");

        Assert.True(
            paths.Count > 1,
            $"the schema declares {paths.Count} backend path setting or settings, so the registrator leg below has nothing to look for");

        Assert.True(
            pasted.Count > 0,
            $"the security policy pastes {pasted.Count} lines under \"{PolicyCommand}\"");
    }

    [Fact]
    public void The_policy_paste_prints_what_the_command_prints()
    {
        var found = ConstructionsInThePlugin();
        var pasted = PastedUnderPolicyCommand();

        Assert.True(
            pasted.SequenceEqual(found, StringComparer.Ordinal),
            $"{Policy} pastes {Show(pasted)} under \"{PolicyCommand}\" and the plugin answers {Show(found)}. The paragraph around that paste tells a reporter what a saved tool path reaches, so the two disagreeing is that paragraph describing a different tree from the one it is in.");
    }

    [Fact]
    public void The_registrator_names_every_backend_path_the_schema_declares_while_it_carries_none_of_them()
    {
        var registrator = Read(Path.Combine(PluginProject, Registrator));

        if (!registrator.Contains(Construction + "(null, null)", StringComparison.Ordinal))
        {
            // Somebody carried the configuration through. What this file owes then is
            // nothing, and the policy's paste above is what goes red until the
            // paragraph around it has been read against that change.
            return;
        }

        foreach (var name in BackendPaths())
        {
            Assert.True(
                registrator.Contains(name, StringComparison.Ordinal),
                $"the schema declares {name} and {Registrator} builds the local backend's settings out of nothing without naming it, so a path an operator saves is dropped there and the file beside the line that drops it does not say so.");
        }
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // One clone checks these files out with carriage returns and another does
        // not, and neither is wrong: `.gitattributes` stores a line feed and lets the
        // checkout decide. What has to be true is that the answer does not move
        // between the two.
        var asLineFeeds = Read(Policy).Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        var fromLineFeeds = PastedUnder(asLineFeeds, PolicyCommand);
        var fromCarriageReturns = PastedUnder(asCarriageReturns, PolicyCommand);

        Assert.NotEmpty(fromLineFeeds);
        Assert.Equal(fromLineFeeds, fromCarriageReturns);
    }

    /// <summary>
    /// Every settable property the configuration file declares.
    /// </summary>
    /// <returns>The names, in the order the file writes them.</returns>
    private static List<string> DeclaredSettings() =>
        [.. _declared
            .Matches(Read(Path.Combine(PluginProject, SchemaFile.Replace('/', Path.DirectorySeparatorChar))))
            .Select(match => match.Groups["name"].Value)];

    /// <summary>
    /// The settings the local backend would be built out of.
    /// </summary>
    /// <returns>The names, sorted, so a message reads the same on every machine.</returns>
    private static List<string> BackendPaths() =>
        [.. DeclaredSettings().Where(name => _backendPath.IsMatch(name)).Order(StringComparer.Ordinal)];

    /// <summary>
    /// What the command the policy quotes returns, as that command prints it.
    /// </summary>
    /// <remarks>
    /// The line number is deliberately not part of this. A paste carrying one goes
    /// stale on any edit anywhere above the line it quotes, which is a direction that
    /// drifts without anything having changed about the subject, and the same lesson
    /// is already written on the README's own comparison.
    /// </remarks>
    /// <returns>One entry per matching line, path first.</returns>
    private static List<string> ConstructionsInThePlugin()
    {
        var root = Path.Combine(RepositoryRoot(), PluginProject);

        Assert.True(Directory.Exists(root), $"there is nothing to walk at {root}");

        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);

        return [.. sources
            .Select(path => (Path: path, Relative: System.IO.Path.GetRelativePath(root, path).Replace('\\', '/')))
            .Where(file => !file.Relative.Contains("/bin/", StringComparison.Ordinal)
                && !file.Relative.Contains("/obj/", StringComparison.Ordinal))
            .SelectMany(file => File
                .ReadAllLines(file.Path)
                .Where(line => line.Contains(Construction, StringComparison.Ordinal))
                .Select(line => PluginProject + "/" + file.Relative + ":" + line.TrimEnd()))
            .Order(StringComparer.Ordinal)];
    }

    private static List<string> PastedUnderPolicyCommand() =>
        PastedUnder(Read(Policy), PolicyCommand);

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

    // The files a clone checked out, rather than a copy carried next to the test
    // assembly. What these legs are about is the bytes a reporter and a reader of the
    // source are given, and the compiler is what knows where those are.
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
