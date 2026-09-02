using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend guide is the only place in this tree that says which licence this
/// plugin ships under. It states one, and it quotes the head of <c>LICENSE</c> as
/// the reading that decides it. This runs that reading again and refuses a
/// statement the file no longer answers with.
/// </summary>
/// <remarks>
/// WHY THIS PAGE AND NOT ANOTHER. A search for the name returns one site outside
/// the licence text itself, and it is the sentence this class judges, so a reader
/// wanting to know what the plugin is licensed as has exactly one place to go and
/// nothing was comparing it against the file beside it.
///
/// <c>GuidePasteTests</c> re-runs the searches this page quotes and is the obvious
/// place to look for this. It is not there: what it recognises is a <c>git grep -n</c>
/// over tracked files, and the licence section quotes a <c>git show</c> of one file
/// and a network call. So the paste of the licence header sat under the one guard
/// over this page's pastes and outside what it reads.
///
/// The failure this is written against is a relicensing, which is the change that
/// moves <c>LICENSE</c> and touches nothing else. The page would go on naming the
/// old licence under a paste of the old header, and the sentence a reader trusts
/// for this would be wrong in the direction nobody re-reads.
///
/// THE TWO DIRECTIONS ARE BOTH REFUSED. The paste has to reproduce, and the name
/// in the sentence has to be the one the pasted header declares. Neither alone is
/// enough: a paste kept fresh under a sentence naming the old licence passes the
/// first, and a sentence naming what the file says under a stale paste passes the
/// second.
///
/// WHAT THIS DOES NOT DO.
///
/// It says nothing about whether the three upstream licences the same section
/// states are the licences those projects declare. Those come from a network call
/// this suite does not make, and a reading of this tree cannot make it. The clause
/// of #56 asking that the upstream statement be checked rather than remembered is
/// untouched by this, and stays a reading somebody does by hand.
///
/// WHAT IT DOES SAY ABOUT THEM IS HOW MANY THERE ARE. The section opens with a
/// figure for how many separate statements it is about, and that figure is one for
/// this plugin plus one per project the loop beside it names. A project added to
/// the loop moves it, which is the change the sentence is at risk of, so the second
/// reader below derives it rather than reading it twice. The loop and its paste are
/// compared first: a figure derived from a reading whose answers are not all there
/// would be a number nobody read.
///
/// It names a licence from the header of the file rather than auditing the file.
/// The header shapes it knows are the GNU family, so a <c>LICENSE</c> carrying any
/// other text is refused as one it cannot name rather than passed. That is the
/// fail-closed direction and it is also the bound: it reads two lines and has no
/// opinion about the nine hundred below them.
///
/// It reads the checkout rather than what git tracks, for the reason its
/// neighbours give: the bytes a reader is handed are the bytes in the file they
/// open. The command the page quotes reads the mainline, so a page edited on a
/// branch is judged here against that branch's own files.
/// </remarks>
public sealed class LicenceStatementTests
{
    /// <summary>
    /// The page this reads, relative to the repository root.
    /// </summary>
    private const string PageName = "docs/choosing-a-backend.md";

    /// <summary>
    /// The file the page quotes, and the one that decides the answer.
    /// </summary>
    private const string LicenceName = "LICENSE";

    /// <summary>
    /// The heading this class is about. What it holds is the text from that line to
    /// the next heading of the same level.
    /// </summary>
    private const string Heading = "## The licences";

    /// <summary>
    /// A quoted reading of one file at the mainline, as the page indents it: the
    /// path after the colon and the line range the page asked for.
    /// </summary>
    private static readonly Regex _quotedReading = new(
        @"^ {4}git show origin/master:(?<path>\S+) \| sed -n '(?<from>[0-9]+),(?<to>[0-9]+)p'$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The sentence that states this plugin's own licence. It is matched rather
    /// than described because what has to move on the day the licence does is a
    /// name in a sentence.
    /// </summary>
    private static readonly Regex _claim = new(
        @"this plugin is (?<id>[A-Za-z0-9.+-]+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The reading of the upstream projects the section quotes: a loop naming one
    /// repository per statement it is about.
    /// </summary>
    private static readonly Regex _upstreamReading = new(
        @"^ {4}for r in (?<repositories>[^;]+); do gh api ",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The figure the section opens with, which is how many licence statements it
    /// is about.
    /// </summary>
    /// <remarks>
    /// The gap is whitespace rather than a space, because the sentence is wrapped
    /// and a reader that required a space would call the section silent for where
    /// its line broke.
    /// </remarks>
    private static readonly Regex _separateStatements = new(
        @"(?<count>[A-Za-z]+)\s+separate\s+statements",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The number words the section may write its figure in.
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
        ["None"] = 0,
        ["none"] = 0,
        ["One"] = 1,
        ["one"] = 1,
        ["Two"] = 2,
        ["two"] = 2,
        ["Three"] = 3,
        ["three"] = 3,
        ["Four"] = 4,
        ["four"] = 4,
        ["Five"] = 5,
        ["five"] = 5,
        ["Six"] = 6,
        ["six"] = 6,
        ["Seven"] = 7,
        ["seven"] = 7,
        ["Eight"] = 8,
        ["eight"] = 8,
        ["Nine"] = 9,
        ["nine"] = 9,
        ["Ten"] = 10,
        ["ten"] = 10
    };

    /// <summary>
    /// The version the header of a GNU licence states.
    /// </summary>
    private static readonly Regex _version = new(
        @"VERSION (?<v>[0-9]+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_reader_finds_the_section_the_reading_and_the_file_it_is_about()
    {
        // Guards every leg below. A reader that found no section, or no quoted
        // reading in it, would agree with the page whatever either said and would
        // do it in green.
        var section = SectionOf(Read(PageName));

        Assert.True(
            section.Length > 0,
            $"{PageName} no longer carries \"{Heading}\", so the section this judges is not there to judge");

        Assert.True(
            _quotedReading.IsMatch(section),
            $"\"{Heading}\" quotes no reading of {LicenceName} in the shape this reads, so the statement under it rests on nothing this run can re-run");

        Assert.True(
            Lines(Read(LicenceName)).Count > 2,
            $"{LicenceName} gave fewer than three lines, so the header the name is derived from is not there");
    }

    [Fact]
    public void The_page_states_the_licence_the_file_beside_it_declares()
    {
        Assert.Empty(Complaints(Read(PageName), Read(LicenceName)));
    }

    [Fact]
    public void The_figure_the_section_opens_with_is_the_number_of_statements_it_holds()
    {
        Assert.Empty(CountComplaints(Read(PageName)));
    }

    [Fact]
    public void The_reader_refuses_a_section_whose_figure_is_not_the_number_of_statements()
    {
        // The failure this exists against: a fourth upstream project is added to the
        // loop and the sentence above it goes on saying four.
        var complaints = CountComplaints(
            PageWith(
                "    for r in a/one b/two c/three d/four; do gh api repos/$r --jq '.x'; done",
                "    a/one MIT",
                "    b/two MIT",
                "    c/three MIT",
                "    d/four MIT",
                string.Empty,
                "Four separate statements, and none of them is about the model file."));

        Assert.Contains(complaints, complaint => complaint.Contains("is about Four separate statements and it holds 5", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_loop_that_names_more_repositories_than_it_pasted()
    {
        // The half a reader would otherwise take on trust. The figure is derived from
        // the loop, so a loop naming a project whose answer never came back would
        // move the figure to a number nobody read.
        var complaints = CountComplaints(
            PageWith(
                "    for r in a/one b/two c/three; do gh api repos/$r --jq '.x'; done",
                "    a/one MIT",
                "    b/two MIT",
                string.Empty,
                "Four separate statements, and none of them is about the model file."));

        Assert.Contains(complaints, complaint => complaint.Contains("names 3 repositories and pastes 2", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_section_that_states_no_figure_at_all()
    {
        // The fail-closed direction. A sentence rewritten without its number leaves a
        // comparison with one side, and an unreadable word is no figure rather than
        // nought.
        var complaints = CountComplaints(
            PageWith(
                "    for r in a/one b/two c/three; do gh api repos/$r --jq '.x'; done",
                "    a/one MIT",
                "    b/two MIT",
                "    c/three MIT",
                string.Empty,
                "Several separate statements, and none of them is about the model file."));

        Assert.Contains(complaints, complaint => complaint.Contains("states no figure", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_section_that_quotes_no_reading_of_the_upstream_projects()
    {
        var complaints = CountComplaints(
            PageWith(
                "    git show origin/master:LICENSE | sed -n '1,2p'",
                "                        GNU GENERAL PUBLIC LICENSE",
                "                           Version 3, 29 June 2007",
                string.Empty,
                "Four separate statements, and none of them is about the model file."));

        Assert.Contains(complaints, complaint => complaint.Contains("quotes no reading of the upstream", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_paste_the_licence_file_does_not_answer_with()
    {
        // The relicensing that moves the file and leaves the page alone, in the
        // half a reader checks first.
        var complaints = Complaints(
            PageWith(
                "    git show origin/master:LICENSE | sed -n '1,2p'",
                "                        GNU GENERAL PUBLIC LICENSE",
                "                           Version 2, June 1991",
                string.Empty,
                "So this plugin is GPL-2.0."),
            Read(LicenceName));

        Assert.Contains(complaints, complaint => complaint.Contains("pastes", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_section_that_quotes_no_reading_of_the_file()
    {
        var complaints = Complaints(
            PageWith("So this plugin is GPL-3.0."),
            Read(LicenceName));

        Assert.Contains(complaints, complaint => complaint.Contains("quotes no reading", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_reading_of_some_other_file()
    {
        // A reading that reproduces, of a file that decides nothing. The page would
        // carry a command, a paste under it and a licence name, and the three would
        // be about two different things.
        var complaints = Complaints(
            PageWith(
                "    git show origin/master:NOTICE.md | sed -n '1,2p'",
                "                        GNU GENERAL PUBLIC LICENSE",
                "                           Version 3, 29 June 2007",
                string.Empty,
                "So this plugin is GPL-3.0."),
            Read(LicenceName));

        Assert.Contains(complaints, complaint => complaint.Contains("reads NOTICE.md", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_page_naming_a_licence_the_file_does_not_declare()
    {
        // The other direction: the paste is kept fresh and the sentence over it is
        // not, which is what an editor re-running the command and not re-reading the
        // paragraph produces.
        var complaints = Complaints(
            PageWith(
                "    git show origin/master:LICENSE | sed -n '1,2p'",
                "                        GNU GENERAL PUBLIC LICENSE",
                "                           Version 3, 29 June 2007",
                string.Empty,
                "So this plugin is MIT."),
            Read(LicenceName));

        Assert.Contains(complaints, complaint => complaint.Contains("names MIT", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_section_that_names_no_licence_for_this_plugin()
    {
        // A section that quotes the file, pastes it correctly and never says what
        // the plugin is. It is the fail-closed direction: an absent statement is
        // refused rather than read as agreement.
        var complaints = Complaints(
            PageWith(
                "    git show origin/master:LICENSE | sed -n '1,2p'",
                "                        GNU GENERAL PUBLIC LICENSE",
                "                           Version 3, 29 June 2007",
                string.Empty,
                "The model file is not covered by any of it."),
            Read(LicenceName));

        Assert.Contains(complaints, complaint => complaint.Contains("states no licence", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_licence_file_whose_header_it_cannot_name()
    {
        // The bound stated in the remarks, executed. A file outside the family this
        // derives from stops the run rather than passing whatever the page says.
        var complaints = Complaints(
            PageWith(
                "    git show origin/master:LICENSE | sed -n '1,2p'",
                "    MIT License",
                "    Copyright (c) 2026",
                string.Empty,
                "So this plugin is MIT."),
            "MIT License\nCopyright (c) 2026\n");

        Assert.Contains(complaints, complaint => complaint.Contains("no licence this reader can name", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_range_the_file_is_too_short_to_answer()
    {
        var complaints = Complaints(
            PageWith(
                "    git show origin/master:LICENSE | sed -n '1,4000p'",
                "                        GNU GENERAL PUBLIC LICENSE",
                "                           Version 3, 29 June 2007",
                string.Empty,
                "So this plugin is GPL-3.0."),
            Read(LicenceName));

        Assert.Contains(complaints, complaint => complaint.Contains("asks for lines 1 to 4000", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // One clone checks these files out with carriage returns and another does
        // not, and neither is wrong: `.gitattributes` stores a line feed and lets
        // the checkout decide. What has to be true is that the answer does not move.
        var page = Read(PageName).Replace("\r\n", "\n", StringComparison.Ordinal);
        var licence = Read(LicenceName).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Empty(Complaints(page, licence));
        Assert.Empty(Complaints(
            page.Replace("\n", "\r\n", StringComparison.Ordinal),
            licence.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    /// <summary>
    /// What is wrong between a page and the licence file it states a name out of.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <param name="licence">The licence file, as its clone checked it out.</param>
    /// <returns>One line per disagreement, empty where the two agree.</returns>
    private static List<string> Complaints(string page, string licence)
    {
        var complaints = new List<string>();
        var section = SectionOf(page);

        if (section.Length == 0)
        {
            complaints.Add($"{PageName} carries no \"{Heading}\" section, so nothing states which licence this plugin ships under.");

            return complaints;
        }

        var reading = _quotedReading.Match(section);

        if (!reading.Success)
        {
            complaints.Add($"\"{Heading}\" quotes no reading of {LicenceName}, so the licence it names rests on nothing a run can reproduce.");

            return complaints;
        }

        var path = reading.Groups["path"].Value;

        if (!string.Equals(path, LicenceName, StringComparison.Ordinal))
        {
            complaints.Add($"\"{Heading}\" reads {path} and the file that decides this plugin's licence is {LicenceName}.");

            return complaints;
        }

        var from = int.Parse(reading.Groups["from"].Value, CultureInfo.InvariantCulture);
        var to = int.Parse(reading.Groups["to"].Value, CultureInfo.InvariantCulture);
        var lines = Lines(licence);

        if (from < 1 || to < from || to > lines.Count)
        {
            complaints.Add($"\"{Heading}\" asks for lines {from} to {to} of {LicenceName} and that file has {lines.Count}.");

            return complaints;
        }

        var answered = lines.Skip(from - 1).Take(to - from + 1).Select(line => line.TrimEnd()).ToList();
        var pasted = PastedAfter(section, reading.Index);

        if (!pasted.SequenceEqual(answered, StringComparer.Ordinal))
        {
            complaints.Add($"\"{Heading}\" pastes {Show(pasted)} under its reading of {LicenceName} and that file answers {Show(answered)}.");
        }

        var declared = NamedBy(answered);

        if (declared is null)
        {
            complaints.Add($"the head of {LicenceName} is no licence this reader can name, so the name the page states is compared against nothing.");

            return complaints;
        }

        var claimed = _claim.Match(section);

        if (!claimed.Success)
        {
            complaints.Add($"\"{Heading}\" states no licence for this plugin, and {LicenceName} declares {declared}. This is the one place in the tree that says which licence the plugin ships under.");

            return complaints;
        }

        var named = claimed.Groups["id"].Value.TrimEnd('.', ',');

        if (!string.Equals(named, declared, StringComparison.Ordinal))
        {
            complaints.Add($"\"{Heading}\" names {named} and {LicenceName} declares {declared}.");
        }

        return complaints;
    }

    /// <summary>
    /// What is wrong between the figure the section opens with and the statements it
    /// actually holds.
    /// </summary>
    /// <remarks>
    /// The count is one for this plugin's own licence, which the section reads out of
    /// <c>LICENSE</c>, plus one for each upstream project the loop beside it names.
    /// The loop and its paste are compared first, so a figure derived from a loop
    /// whose answers are not all there is refused rather than believed.
    /// </remarks>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <returns>One line per disagreement, empty where the figure and the section agree.</returns>
    private static List<string> CountComplaints(string page)
    {
        var complaints = new List<string>();
        var section = SectionOf(page);

        if (section.Length == 0)
        {
            complaints.Add($"{PageName} carries no \"{Heading}\" section, so there is no figure to compare.");

            return complaints;
        }

        var upstream = _upstreamReading.Match(section);

        if (!upstream.Success)
        {
            complaints.Add($"\"{Heading}\" quotes no reading of the upstream projects, so the figure it opens with is derived from nothing.");

            return complaints;
        }

        var named = upstream.Groups["repositories"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;
        var pasted = PastedAfter(section, upstream.Index).Count;

        if (named != pasted)
        {
            complaints.Add($"\"{Heading}\" names {named} repositories and pastes {pasted} answer(s) under them, so the two sides of that reading disagree before the figure is compared to either.");

            return complaints;
        }

        var stated = _separateStatements.Match(section);

        if (!stated.Success || !_figures.TryGetValue(stated.Groups["count"].Value, out var figure))
        {
            complaints.Add($"\"{Heading}\" states no figure this reader can name for how many statements it is about, and it holds {named + 1}.");

            return complaints;
        }

        if (figure != named + 1)
        {
            complaints.Add($"\"{Heading}\" is about {stated.Groups["count"].Value} separate statements and it holds {named + 1}: this plugin's own, and one for each of the {named} projects the loop names.");
        }

        return complaints;
    }

    /// <summary>
    /// The licence a file's own header declares, as this repository writes such a
    /// name elsewhere.
    /// </summary>
    /// <remarks>
    /// It knows the GNU family and nothing else, which is what the remarks above
    /// state as its bound. Anything it does not recognise comes back as no answer,
    /// so the comparison stops rather than passing a page it could not judge.
    /// </remarks>
    /// <param name="header">The lines the page pasted, which are the head of the file.</param>
    /// <returns>The identifier, or null where the header is not one it can name.</returns>
    private static string? NamedBy(IEnumerable<string> header)
    {
        var text = string.Join(' ', header).ToUpperInvariant();

        if (!text.Contains("GENERAL PUBLIC LICENSE", StringComparison.Ordinal))
        {
            return null;
        }

        var version = _version.Match(text);

        if (!version.Success)
        {
            return null;
        }

        var family = text.Contains("LESSER", StringComparison.Ordinal)
            ? "LGPL"
            : text.Contains("AFFERO", StringComparison.Ordinal) ? "AGPL" : "GPL";

        return $"{family}-{version.Groups["v"].Value}.0";
    }

    /// <summary>
    /// The indented block under a command, from the character after it to the first
    /// line that is not part of the paste.
    /// </summary>
    /// <param name="section">The section, with its line endings normalised.</param>
    /// <param name="at">Where in that section the command line starts.</param>
    /// <returns>Each pasted line, with the page's own indent taken off.</returns>
    private static List<string> PastedAfter(string section, int at)
    {
        var after = section[at..].Split('\n').Skip(1);
        var pasted = new List<string>();

        foreach (var line in after)
        {
            if (!line.StartsWith("    ", StringComparison.Ordinal) || line.Trim().Length == 0)
            {
                break;
            }

            pasted.Add(line[4..].TrimEnd());
        }

        return pasted;
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
        var lines = new List<string> { "# A guide", string.Empty, Heading, string.Empty };

        lines.AddRange(body);
        lines.Add(string.Empty);
        lines.Add("## What the tree holds today");
        lines.Add(string.Empty);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// The lines of a file, with its line endings normalised and a trailing empty
    /// line dropped, so a range names the line a reader counts.
    /// </summary>
    /// <param name="text">The file.</param>
    /// <returns>Its lines.</returns>
    private static List<string> Lines(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();

        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string Show(IEnumerable<string> lines)
    {
        var listed = string.Join(" | ", lines);

        return listed.Length == 0 ? "nothing" : listed;
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
