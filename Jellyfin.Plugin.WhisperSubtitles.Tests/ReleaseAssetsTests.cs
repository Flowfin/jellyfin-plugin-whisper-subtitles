using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The release page says how many files a release carries and then lists them.
/// This refuses a page whose figure is not the length of its own list.
/// </summary>
/// <remarks>
/// The figure is the one a person cutting a release checks a finished release
/// against, and the same number was stated twice on this page: once before the
/// list and once in the paragraph under it, as the state the route cannot reach.
/// The second statement is deleted rather than guarded, because a figure written
/// twice is the same defect one paragraph down.
///
/// The failure this is written against is an asset added or dropped. That change
/// edits the list, because the list is what a reader looks at, and the sentence
/// above it is the part somebody scrolls past. This page has already had a figure
/// about itself go wrong that way, which is why <c>ReleaseRefusalSitesTests</c>
/// exists one section further on.
///
/// WHAT THIS DOES NOT DO, and the bound is the whole of it. It compares a sentence
/// against a list, both of them on this page, and it reaches the publish workflow
/// nowhere. The run attaches whatever the packaging steps left in its directory,
/// so a fifth file arriving there is invisible here and the list is a claim about
/// the route rather than a reading of it. What this buys is that the two halves of
/// that claim cannot come apart in silence, which is the direction that has
/// happened on this page before.
///
/// It reads the checkout rather than what git tracks, for the reason its
/// neighbours give: the bytes a releaser is handed are the bytes in the file they
/// open.
/// </remarks>
public sealed class ReleaseAssetsTests
{
    /// <summary>
    /// The page this reads, relative to the repository root.
    /// </summary>
    private const string PageName = "docs/RELEASING.md";

    /// <summary>
    /// The heading this class is about. What it holds is the text from that line to
    /// the next heading of the same level.
    /// </summary>
    private const string Heading = "## What the run produces";

    /// <summary>
    /// The sentence that states how many files a release carries.
    /// </summary>
    /// <remarks>
    /// The gap is whitespace rather than a space, because the sentence is wrapped
    /// and a reader that required a space would call the section silent for where
    /// its line broke.
    /// </remarks>
    private static readonly Regex _attaches = new(
        @"attaches\s+(?<count>[A-Za-z]+)\s+files",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The number words this page may write its figure in.
    /// </summary>
    /// <remarks>
    /// Bounded on purpose and short of every word English has, which is the shape
    /// the figure readers already in this suite take. A figure this table does not
    /// hold is unreadable rather than nought, so a sentence spelling one in a word
    /// nothing here knows is refused by the leg that requires a figure instead of
    /// being compared against a number nobody wrote.
    /// </remarks>
    private static readonly Dictionary<string, int> _figures = new(StringComparer.Ordinal)
    {
        ["no"] = 0,
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10
    };

    [Fact]
    public void The_reader_finds_the_section_the_sentence_and_the_list()
    {
        // Guards every leg below. A reader that found no section, or a section with
        // no list in it, would agree with the page whatever it said and would do it
        // in green.
        var section = SectionOf(Read(PageName));

        Assert.True(
            section.Length > 0,
            $"{PageName} no longer carries \"{Heading}\", so the section this judges is not there to judge");
        Assert.True(
            _attaches.IsMatch(section),
            $"\"{Heading}\" no longer says how many files a release carries, so there is no figure to read");
        Assert.True(
            Listed(section).Count > 1,
            $"\"{Heading}\" lists {Listed(section).Count} file(s), and the figure above it would be compared against that");
    }

    [Fact]
    public void The_figure_the_page_states_is_the_number_of_files_it_lists()
    {
        Assert.Empty(Complaints(Read(PageName)));
    }

    [Fact]
    public void The_reader_refuses_a_page_whose_figure_is_not_the_number_of_files_listed()
    {
        // The failure this exists against: an asset arrives, the list grows because
        // the list is what a reader looks at, and the sentence above it does not.
        var complaints = Complaints(
            PageWith(
                "The run attaches four files:",
                string.Empty,
                "- the plugin archive",
                "- one `.md5` file",
                "- one `.sha256` file",
                "- the packaging metadata",
                "- a software bill of materials"));

        Assert.Contains(complaints, complaint => complaint.Contains("carries four files and lists 5", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_page_that_lists_the_files_and_states_no_figure()
    {
        // The fail-closed direction. A sentence rewritten without its number leaves
        // a comparison with one side, and an unreadable word is no figure rather
        // than nought.
        var complaints = Complaints(
            PageWith(
                "The run attaches several files:",
                string.Empty,
                "- the plugin archive",
                "- one `.md5` file"));

        Assert.Contains(complaints, complaint => complaint.Contains("states no figure", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_section_whose_list_has_gone()
    {
        // A section that states a figure and lists nothing reads as a page whose
        // every asset is accounted for, and it is the shape that passes a comparison
        // against an empty list for free.
        var complaints = Complaints(
            PageWith(
                "The run attaches four files, and they are described elsewhere.",
                string.Empty,
                "There is no list here any more."));

        Assert.Contains(complaints, complaint => complaint.Contains("lists none", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // One clone checks this file out with carriage returns and another does not,
        // and neither is wrong: `.gitattributes` stores a line feed and lets the
        // checkout decide. What has to be true is that the answer does not move.
        var page = Read(PageName).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Empty(Complaints(page));
        Assert.Empty(Complaints(page.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    /// <summary>
    /// What is wrong between the figure the section states and the list under it.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <returns>One line per disagreement, empty where the two agree.</returns>
    private static List<string> Complaints(string page)
    {
        var complaints = new List<string>();
        var section = SectionOf(page);

        if (section.Length == 0)
        {
            complaints.Add($"{PageName} carries no \"{Heading}\" section, so nothing says what a release carries.");

            return complaints;
        }

        var listed = Listed(section);
        var stated = _attaches.Match(section);

        if (!stated.Success || !_figures.TryGetValue(stated.Groups["count"].Value, out var figure))
        {
            complaints.Add($"\"{Heading}\" states no figure this reader can name for how many files a release carries, and it lists {listed.Count}.");

            return complaints;
        }

        if (listed.Count == 0)
        {
            complaints.Add($"\"{Heading}\" says a release carries {stated.Groups["count"].Value} files and lists none, so the figure is compared against nothing.");

            return complaints;
        }

        if (figure != listed.Count)
        {
            complaints.Add($"\"{Heading}\" says a release carries {stated.Groups["count"].Value} files and lists {listed.Count}: {string.Join("; ", listed)}.");
        }

        return complaints;
    }

    /// <summary>
    /// The files the section lists, which is the first bulleted list in it.
    /// </summary>
    /// <remarks>
    /// The first, because the section carries prose after the list and a reader
    /// running on would take a later bullet for an asset. A continuation line is
    /// folded into the entry above it, so an entry that wraps is one file rather
    /// than two.
    /// </remarks>
    /// <param name="section">The section, with its line endings normalised.</param>
    /// <returns>Each listed file, in the order the page writes them.</returns>
    private static List<string> Listed(string section)
    {
        var listed = new List<string>();

        foreach (var line in section.Split('\n'))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                listed.Add(line[2..].Trim());
            }
            else if (listed.Count == 0)
            {
                continue;
            }
            else if (line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                listed[^1] = listed[^1] + " " + line.Trim();
            }
            else
            {
                break;
            }
        }

        return listed;
    }

    /// <summary>
    /// The section this class judges, out of text handed in.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <returns>The text from the heading to the next heading of the same level, with its line endings normalised, or an empty string where the heading is gone.</returns>
    private static string SectionOf(string page)
    {
        var lines = page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var at = Array.FindIndex(lines, line => line.TrimEnd().Equals(Heading, StringComparison.Ordinal));

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

            taken.Add(lines[index]);
        }

        return string.Join('\n', taken);
    }

    /// <summary>
    /// A page carrying the heading this reads and the lines handed in under it.
    /// </summary>
    /// <param name="body">The section's lines.</param>
    /// <returns>A whole page, so a fixture is judged by the same reader as the real one.</returns>
    private static string PageWith(params string[] body)
    {
        var lines = new List<string> { "# Releasing", string.Empty, Heading, string.Empty };

        lines.AddRange(body);
        lines.Add(string.Empty);
        lines.Add("## What the run notes without failing");
        lines.Add(string.Empty);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// A file of this repository, read out of the checkout rather than out of a copy
    /// beside the assembly, for the reason its neighbours in this suite give: the
    /// thing the claim is about is the file a reader opens.
    /// </summary>
    /// <param name="name">Its path relative to the repository root.</param>
    /// <returns>The whole file.</returns>
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), name.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
