using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend guide states how far the floor on what this plugin will believe is
/// a model sits below the smallest model it publishes a size for. Both numbers are
/// on that page already, one in a pasted table and one in a pasted constant, and
/// the sentence between them was kept by hand. This derives it instead.
/// </summary>
/// <remarks>
/// THE FAILURE IT WAS WRITTEN AGAINST HAD ALREADY HAPPENED. The sentence said the
/// floor was three orders of magnitude below the smallest published model. The
/// floor is one mebibyte and the smallest row of the table on the same page is
/// 75 MiB, so the true relation is a factor of 75, under two orders of magnitude
/// and a factor of more than ten away from what the page claimed. Both numbers a
/// reader needs to see that were printed above the sentence.
///
/// <c>GuidePasteTests</c> is the guard over this page's pastes and it says in its
/// own remarks why it did not catch this: it compares the lines and has no opinion
/// about the prose beside them, so a paste that reproduces exactly under a sentence
/// drawing the wrong conclusion from it passes there. This is that direction, over
/// the one sentence on the page that does arithmetic on two of its own pastes.
///
/// WHERE EACH SIDE COMES FROM. The floor is read from
/// <see cref="LocalBackendOptions.SmallestPlausibleModelBytes"/> rather than from
/// the page's paste of it, so a constant changed in the source moves the derived
/// figure whatever the page pasted; the paste itself is already re-run by
/// <c>GuidePasteTests</c> and is not re-run here. The smallest model comes from the
/// table the page quotes from upstream, because that table is what the sentence
/// says "the table above" is and what a reader compares against.
///
/// The <c>Disk</c> column is located by the table's own header rather than by
/// position, so a column inserted upstream moves the reading rather than silently
/// shifting it onto <c>Mem</c>, which is a different measurement of a different
/// thing and would give a number that looks plausible.
///
/// THE COUNT OF ROWS COMPARED IS IN EVERY MESSAGE. A table whose rows this reader
/// could not parse would otherwise report a floor sitting infinitely far below
/// nothing, in green. A table it finds no usable row in is refused instead, and a
/// disagreement says how many rows the figure was derived from.
///
/// WHAT THIS DOES NOT DO.
///
/// It says nothing about whether the upstream table is right, or current. That is a
/// network call this suite does not make, and the page states the blob it was read
/// at rather than claiming it is today's.
///
/// It compares one sentence. The paragraph around it, and every other conclusion
/// the page draws from the same table, are outside what this reads.
///
/// The factor it derives is a whole number, the largest whose product with the
/// floor still fits under the smallest model, so a relation that is not a whole
/// multiple is compared against the value a reader would round to rather than
/// refused. The page has no reason to write a fraction there and this would not
/// notice if it did.
///
/// It reads the checkout rather than what git tracks, for the reason its neighbours
/// in this suite give: the bytes a reader is handed are the bytes in the file they
/// open.
/// </remarks>
public sealed class ModelFloorFigureTests
{
    /// <summary>
    /// The page this reads, relative to the repository root.
    /// </summary>
    private const string PageName = "docs/choosing-a-backend.md";

    /// <summary>
    /// The heading this class is about. What it holds is the text from that line to
    /// the next heading at that level or above.
    /// </summary>
    private const string Heading = "### The models, and what the figures are";

    /// <summary>
    /// The column of the upstream table that gives the size of the file an operator
    /// downloads. The other column is a memory figure for upstream's own runtime.
    /// </summary>
    private const string SizeColumn = "Disk";

    /// <summary>
    /// The sentence that states the relation, matched rather than described because
    /// what has to move on the day either number does is a figure inside it.
    /// </summary>
    /// <remarks>
    /// Every gap is whitespace rather than a space, because the sentence is wrapped
    /// and a reader that required a space would call the page silent for where its
    /// line happened to break.
    /// </remarks>
    private static readonly Regex _relation = new(
        @"smallest\s+model\s+in\s+the\s+table\s+above\s+is\s+(?<factor>[0-9]+)\s+times\s+that",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A size as the upstream table writes one, with the binary units it uses.
    /// </summary>
    private static readonly Regex _size = new(
        @"^~?(?<value>[0-9]+(\.[0-9]+)?)\s*(?<unit>B|KiB|MiB|GiB|TiB)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// How many bytes each unit the table may write is worth.
    /// </summary>
    private static readonly Dictionary<string, long> _units = new(StringComparer.Ordinal)
    {
        ["B"] = 1L,
        ["KiB"] = 1024L,
        ["MiB"] = 1024L * 1024,
        ["GiB"] = 1024L * 1024 * 1024,
        ["TiB"] = 1024L * 1024 * 1024 * 1024
    };

    [Fact]
    public void The_reader_finds_the_section_the_table_and_the_floor_it_compares()
    {
        // Guards every leg below. A reader that found no section, no table or no
        // floor would agree with whatever the page said, in green.
        var section = SectionOf(Read(PageName));

        Assert.True(
            section.Length > 0,
            $"{PageName} no longer carries \"{Heading}\", so the sentence this judges is not there to judge");

        Assert.NotEmpty(PublishedSizes(section));

        Assert.True(
            LocalBackendOptions.SmallestPlausibleModelBytes > 0,
            "the floor this plugin holds is not a positive number of bytes, so there is nothing for the page to be a multiple of");
    }

    [Fact]
    public void The_figure_the_page_states_is_the_floor_against_the_smallest_published_model()
    {
        Assert.Empty(Complaints(Read(PageName), LocalBackendOptions.SmallestPlausibleModelBytes));
    }

    [Fact]
    public void The_reader_refuses_a_page_stating_a_relation_the_two_numbers_do_not_make()
    {
        // The failure this class was written against, in the shape it actually had:
        // a sentence describing a much larger gap than the numbers above it make.
        var complaints = Complaints(PageWith("is 1000 times that"), 1024L * 1024);

        Assert.Contains(
            complaints,
            complaint => complaint.Contains("says 1000 times and the two numbers make 75", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_page_whose_floor_moved_under_a_sentence_that_did_not()
    {
        // The other direction, and the one no reading of the page alone would catch:
        // the constant in the source changes and every byte of the page stays put.
        var complaints = Complaints(PageWith("is 75 times that"), 4L * 1024 * 1024);

        Assert.Contains(
            complaints,
            complaint => complaint.Contains("says 75 times and the two numbers make 18", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_page_that_states_no_figure_at_all()
    {
        // The fail-closed direction. A sentence rewritten without its number leaves
        // a comparison with one side, and this refuses rather than passing it.
        var complaints = Complaints(PageWith("is a long way above that"), 1024L * 1024);

        Assert.Contains(
            complaints,
            complaint => complaint.Contains("states no figure", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_table_it_could_not_take_a_single_size_out_of()
    {
        // A run that compared nothing must not read as a run that compared
        // everything and agreed.
        var complaints = Complaints(
            PageWith(
                "is 75 times that",
                "    | Model  | Disk    | Mem     |",
                "    | ------ | ------- | ------- |",
                "    | tiny   | plenty  | ~273 MB |"),
            1024L * 1024);

        Assert.Contains(
            complaints,
            complaint => complaint.Contains("no row whose size this reader can read", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_table_that_names_no_size_column()
    {
        // The column is found by name, so a table reshaped upstream is refused
        // rather than read one column across onto the memory figure.
        var complaints = Complaints(
            PageWith(
                "is 75 times that",
                "    | Model  | Size    | Mem     |",
                "    | ------ | ------- | ------- |",
                "    | tiny   | 75 MiB  | ~273 MB |"),
            1024L * 1024);

        Assert.Contains(
            complaints,
            complaint => complaint.Contains("names no Disk column", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_a_section_that_holds_no_table_at_all()
    {
        var complaints = Complaints(
            string.Join(
                '\n',
                "# A guide",
                string.Empty,
                Heading,
                string.Empty,
                "One mebibyte, and the smallest model in the table above is 75 times that.",
                string.Empty,
                "## The remote backend",
                string.Empty),
            1024L * 1024);

        Assert.Contains(
            complaints,
            complaint => complaint.Contains("quotes no table of published model sizes", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_takes_the_smallest_row_rather_than_the_first_one()
    {
        // The sentence is about the smallest model, and the table is upstream's to
        // order. A reader keyed on position would follow a reordering into a figure
        // that is right about the wrong row.
        var complaints = Complaints(
            PageWith(
                "is 75 times that",
                "    | Model  | Disk    | Mem     |",
                "    | ------ | ------- | ------- |",
                "    | large  | 2.9 GiB | ~3.9 GB |",
                "    | tiny   | 75 MiB  | ~273 MB |"),
            1024L * 1024);

        Assert.Empty(complaints);
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // One clone checks this page out with carriage returns and another does not,
        // and neither is wrong. What has to be true is that the answer does not move.
        var page = Read(PageName).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Empty(Complaints(page, LocalBackendOptions.SmallestPlausibleModelBytes));
        Assert.Empty(Complaints(
            page.Replace("\n", "\r\n", StringComparison.Ordinal),
            LocalBackendOptions.SmallestPlausibleModelBytes));
    }

    /// <summary>
    /// What is wrong between the relation the page states and the two numbers it
    /// prints above it.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <param name="floorBytes">The smallest file this plugin will believe is a model.</param>
    /// <returns>One line per disagreement, empty where the sentence and the numbers agree.</returns>
    private static List<string> Complaints(string page, long floorBytes)
    {
        var complaints = new List<string>();
        var section = SectionOf(page);

        if (section.Length == 0)
        {
            complaints.Add($"{PageName} carries no \"{Heading}\" section, so nothing states how far the floor sits below the smallest published model.");

            return complaints;
        }

        var header = HeaderOf(section);

        if (header is null)
        {
            complaints.Add($"\"{Heading}\" quotes no table of published model sizes, so the figure in it is derived from nothing.");

            return complaints;
        }

        if (!header.Contains(SizeColumn, StringComparer.Ordinal))
        {
            complaints.Add($"the table \"{Heading}\" quotes names no {SizeColumn} column, and the column beside it measures upstream's own runtime rather than the file an operator downloads.");

            return complaints;
        }

        var sizes = PublishedSizes(section);

        if (sizes.Count == 0)
        {
            complaints.Add($"the table \"{Heading}\" quotes has no row whose size this reader can read, so the figure below it would be compared against nothing.");

            return complaints;
        }

        if (floorBytes <= 0)
        {
            complaints.Add($"the floor this plugin holds is {floorBytes} bytes, so the sentence in \"{Heading}\" is a multiple of nothing.");

            return complaints;
        }

        var smallest = sizes.Min();
        var derived = smallest / floorBytes;
        var stated = _relation.Match(section);

        if (!stated.Success)
        {
            complaints.Add($"\"{Heading}\" states no figure for how far the floor sits below the smallest model in the table it quotes, and the {sizes.Count} row(s) it does quote make that {derived}.");

            return complaints;
        }

        var figure = long.Parse(stated.Groups["factor"].Value, CultureInfo.InvariantCulture);

        if (figure != derived)
        {
            complaints.Add($"\"{Heading}\" says {figure} times and the two numbers make {derived}: a floor of {floorBytes} bytes under a smallest published model of {smallest} bytes, taken over the {sizes.Count} row(s) of the table it quotes.");
        }

        return complaints;
    }

    /// <summary>
    /// The header cells of the first table the section quotes.
    /// </summary>
    /// <param name="section">The section, with its line endings normalised.</param>
    /// <returns>The cells, or null where the section quotes no table.</returns>
    private static List<string>? HeaderOf(string section) =>
        section.Split('\n')
            .Select(Cells)
            .FirstOrDefault(cells => cells.Count > 1);

    /// <summary>
    /// Every size the quoted table gives in its size column.
    /// </summary>
    /// <remarks>
    /// The separator row under the header has as many cells as the header and no
    /// size in any of them, so it falls out here rather than needing a rule of its
    /// own. So does a row upstream leaves blank.
    /// </remarks>
    /// <param name="section">The section, with its line endings normalised.</param>
    /// <returns>One entry per readable row, in bytes.</returns>
    private static List<long> PublishedSizes(string section)
    {
        var rows = section.Split('\n').Select(Cells).Where(cells => cells.Count > 1).ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        var at = rows[0].FindIndex(cell => string.Equals(cell, SizeColumn, StringComparison.Ordinal));

        if (at < 0)
        {
            return [];
        }

        var sizes = new List<long>();

        foreach (var row in rows.Skip(1).Where(row => row.Count > at))
        {
            var size = _size.Match(row[at]);

            if (!size.Success)
            {
                continue;
            }

            var value = double.Parse(size.Groups["value"].Value, CultureInfo.InvariantCulture);

            sizes.Add((long)(value * _units[size.Groups["unit"].Value]));
        }

        return sizes;
    }

    /// <summary>
    /// The cells of one line of a pipe table, as the page indents its pastes.
    /// </summary>
    /// <param name="line">One line of the section.</param>
    /// <returns>Its cells, trimmed, or nothing where the line is not a table row.</returns>
    private static List<string> Cells(string line)
    {
        var text = line.Trim();

        if (!text.StartsWith('|') || !text.EndsWith('|'))
        {
            return [];
        }

        return text[1..^1].Split('|').Select(cell => cell.Trim()).ToList();
    }

    /// <summary>
    /// The section this class judges, out of text handed in.
    /// </summary>
    /// <param name="page">The page, as its clone checked it out.</param>
    /// <returns>The text from the heading to the next heading at that level or above, with its line endings normalised, or an empty string where the heading is gone.</returns>
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
            if (lines[index].StartsWith("## ", StringComparison.Ordinal)
                || lines[index].StartsWith("### ", StringComparison.Ordinal))
            {
                break;
            }

            taken.Add(lines[index]);
        }

        return string.Join('\n', taken);
    }

    /// <summary>
    /// A page carrying the heading this reads, the upstream table and a sentence
    /// ending in the words handed in.
    /// </summary>
    /// <param name="relation">How the sentence states the relation.</param>
    /// <param name="table">The quoted table, where the default one is not what the fixture is about.</param>
    /// <returns>A whole page, so a fixture is judged by the same reader as the real one.</returns>
    private static string PageWith(string relation, params string[] table)
    {
        var lines = new List<string> { "# A guide", string.Empty, Heading, string.Empty };

        lines.AddRange(table.Length > 0
            ? table
            :
            [
                "    | Model  | Disk    | Mem     |",
                "    | ------ | ------- | ------- |",
                "    | tiny   | 75 MiB  | ~273 MB |",
                "    | large  | 2.9 GiB | ~3.9 GB |"
            ]);

        lines.Add(string.Empty);
        lines.Add($"One mebibyte, and the smallest model in the table above {relation}. It");
        lines.Add("catches a download that was refused and saved anyway.");
        lines.Add(string.Empty);
        lines.Add("## The remote backend");
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
