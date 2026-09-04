using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend guide hands a reader a search over this tree and pastes what it
/// returned. This runs each of those searches again and refuses a paste the tree no
/// longer answers with.
/// </summary>
/// <remarks>
/// The failure it is written against happened twice on that page in one piece of
/// work, and both times the commit that broke a paste was the commit that edited the
/// page. The concurrency limits landed as configuration properties, the same change
/// added a second paste of the same file further down, and the first paste went on
/// saying the configuration holds four properties while the file held six. The other
/// two were line numbers in a file that gained a field above them. Nothing read
/// either, because the checks over this page hold the table of offered values and
/// one sentence, and <c>ReadmeClaimsTests</c> compares pastes on a different page.
///
/// The population is derived from the page rather than listed here. A paste added
/// tomorrow is asked about without anybody remembering to add a line, and a paste
/// deleted is caught by the floor below rather than passing as an empty theory.
///
/// WHAT THIS DOES NOT DO, and the first two matter most.
///
/// It re-runs the search over the WORKING TREE, while the command it quotes reads
/// what git tracks. A file that is present and untracked is read here and not there,
/// and a tracked file deleted from the checkout is the other way round. Build output
/// is excluded by path because it is neither tracked nor what a reader running the
/// command would see, and a file carrying a zero byte is skipped because that is
/// what the real search does with one.
///
/// It reads the pattern as a .NET regular expression, with <c>\|</c> turned into an
/// alternation for the searches written in the basic dialect. The two dialects
/// disagree about <c>+</c>, <c>?</c> and grouping, so a pattern using those would be
/// judged against a question the page does not ask. No paste on the page uses one,
/// and a paste that starts to would need this translation widened rather than
/// trusted.
///
/// A paste of no lines at all is refused by the comparison rather than by a leg of
/// its own, because a search that returns nothing is answered here with the line the
/// page writes for that case rather than with none, so an empty block never matches.
///
/// It compares the lines and has no opinion about the prose beside them. A paste
/// that reproduces exactly under a sentence drawing the wrong conclusion from it
/// passes here, which is exactly what the count of four did until the paste under it
/// also went wrong. And its subject is one page: the same drift on the other
/// documents in <c>docs/</c> is outside what this reads.
/// </remarks>
public class GuidePasteTests
{
    /// <summary>
    /// The page this reads, relative to the repository root.
    /// </summary>
    private const string PageName = "docs/choosing-a-backend.md";

    /// <summary>
    /// What a paste writes where the search returned nothing. The page carries the
    /// convention rather than an empty block, because a block with no lines in it is
    /// indistinguishable from a paste somebody forgot.
    /// </summary>
    private const string PrintedNothing = "exit=1";

    /// <summary>
    /// A quoted search over tracked files, as the page indents it: the dialect flag,
    /// the pattern between single quotes, and the pathspec after the separator.
    /// </summary>
    private static readonly Regex QuotedSearch = new(
        @"^ {4}git grep -n(?<extended>E?) '(?<pattern>.+)' -- (?<pathspec>\S+)$",
        RegexOptions.Multiline,
        TimeSpan.FromSeconds(5));

    public static TheoryData<string> EverySearch =>
        new(Searches(Page()).Select(search => search.Command).ToArray());

    [Fact]
    public void The_check_can_see_the_searches_it_judges()
    {
        // An empty population passes every theory under it while claiming nothing,
        // and it is what a page rewritten into fenced blocks, or a page renamed,
        // would produce. The floor is what the page carries, so a paste deleted is a
        // red rather than a quieter suite.
        //
        // It moves with the page, and it has to be moved by hand. It stood at ten
        // while the page grew to eleven, and for that long any one of the eleven
        // could have been deleted with every leg here green - which is the property
        // the comment above claims, lost by an addition that had no reason to open
        // this file.
        var searches = Searches(Page());

        Assert.True(
            searches.Count >= 11,
            $"{PageName} quotes {searches.Count} search(es) of tracked files and this was written against eleven. A paste that stopped being recognised is held by nothing.");
    }

    [Theory]
    [MemberData(nameof(EverySearch))]
    public void Every_search_the_guide_quotes_prints_what_the_guide_pastes_under_it(string command)
    {
        var search = Searches(Page()).Single(candidate => string.Equals(candidate.Command, command, StringComparison.Ordinal));
        var answered = Answer(search);

        Assert.True(
            search.Pasted.SequenceEqual(answered, StringComparer.Ordinal),
            $"{PageName} pastes {Show(search.Pasted)} under \"{command}\" and this tree answers {Show(answered)}. A line the tree returns that the page does not carry, and a line the page carries that the tree does not return, are both this.");
    }

    /// <summary>
    /// What the tree returns for one of the page's searches, in the form the command
    /// prints it: the path, the line number and the line.
    /// </summary>
    /// <param name="search">The search, as the page quotes it.</param>
    /// <returns>Each matching line, or the single line a paste writes for none.</returns>
    private static List<string> Answer(QuotedFileSearch search)
    {
        var root = RepositoryRoot();
        var target = Path.Combine(root, search.Pathspec.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(
            File.Exists(target) || Directory.Exists(target),
            $"{PageName} searches {search.Pathspec} and there is no such file or directory to search.");

        var files = File.Exists(target)
            ? new[] { target }
            : Directory.GetFiles(target, "*", SearchOption.AllDirectories);

        var pattern = new Regex(
            search.Extended ? search.Pattern : search.Pattern.Replace(@"\|", "|", StringComparison.Ordinal),
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        var answered = files
            .Select(path => (Path: path, Relative: Path.GetRelativePath(root, path).Replace('\\', '/')))
            .Where(file => !file.Relative.Contains("/bin/", StringComparison.Ordinal)
                && !file.Relative.Contains("/obj/", StringComparison.Ordinal))
            .OrderBy(file => file.Relative, StringComparer.Ordinal)
            .SelectMany(file => Matches(file.Path, file.Relative, pattern))
            .ToList();

        return answered.Count == 0 ? [PrintedNothing] : answered;
    }

    /// <summary>
    /// The matching lines of one file, numbered from one.
    /// </summary>
    /// <remarks>
    /// The carriage return a checkout may have put back is taken off first, because
    /// the search the page quotes reads what git holds and git holds none. A file
    /// carrying a zero byte is skipped rather than read, which is what that search
    /// does with a file it takes for binary.
    /// </remarks>
    /// <param name="path">The file to read.</param>
    /// <param name="relative">Its path relative to the repository root.</param>
    /// <param name="pattern">The pattern a line has to match.</param>
    /// <returns>Each matching line, in the form the command prints it.</returns>
    private static IEnumerable<string> Matches(string path, string relative, Regex pattern)
    {
        var bytes = File.ReadAllBytes(path);

        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            yield break;
        }

        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (pattern.IsMatch(lines[i]))
            {
                yield return $"{relative}:{i + 1}:{lines[i].TrimEnd()}";
            }
        }
    }

    /// <summary>
    /// Every search of tracked files the page quotes, with what it pastes under each.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The searches, in the order the page writes them.</returns>
    private static List<QuotedFileSearch> Searches(string page)
    {
        var lines = page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var searches = new List<QuotedFileSearch>();

        for (var i = 0; i < lines.Length; i++)
        {
            var quoted = QuotedSearch.Match(lines[i]);

            if (!quoted.Success)
            {
                continue;
            }

            searches.Add(new QuotedFileSearch(
                lines[i].Trim(),
                quoted.Groups["pattern"].Value,
                quoted.Groups["pathspec"].Value,
                quoted.Groups["extended"].Value.Length > 0,
                PastedAfter(lines, i)));
        }

        return searches;
    }

    /// <summary>
    /// The indented block under a command line.
    /// </summary>
    /// <param name="lines">The page as lines.</param>
    /// <param name="at">The index of the command line.</param>
    /// <returns>Each pasted line, with the page's own indent taken off.</returns>
    private static List<string> PastedAfter(string[] lines, int at)
    {
        var pasted = new List<string>();

        for (var i = at + 1; i < lines.Length; i++)
        {
            var line = lines[i];

            if (!line.StartsWith("    ", StringComparison.Ordinal) || line.Trim().Length == 0)
            {
                break;
            }

            pasted.Add(line[4..].TrimEnd());
        }

        return pasted;
    }

    private static string Show(IEnumerable<string> lines)
    {
        var listed = string.Join(" | ", lines);

        return listed.Length == 0 ? "nothing" : listed;
    }

    /// <summary>
    /// The page, read out of the checkout rather than out of a copy beside the
    /// assembly, for the reason its neighbours in this suite give: the thing the
    /// claim is about is the file a reader opens.
    /// </summary>
    /// <returns>The whole page.</returns>
    private static string Page() => File.ReadAllText(Path.Combine(RepositoryRoot(), PageName));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    /// <summary>
    /// One search the page quotes, and the paste under it.
    /// </summary>
    /// <param name="Command">The command line, as the page writes it.</param>
    /// <param name="Pattern">The pattern between its quotes.</param>
    /// <param name="Pathspec">What it is scoped to.</param>
    /// <param name="Extended">Whether it asked for the extended dialect.</param>
    /// <param name="Pasted">The lines pasted under it.</param>
    private sealed record QuotedFileSearch(
        string Command,
        string Pattern,
        string Pathspec,
        bool Extended,
        List<string> Pasted);
}
