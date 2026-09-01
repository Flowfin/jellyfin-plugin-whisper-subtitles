using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The README says the arithmetic that folds a measured item into a throughput
/// factor is built and that nothing calls it. This reads both halves of that
/// sentence against the tree rather than trusting it.
/// </summary>
/// <remarks>
/// A denial about what this plugin does not do yet is true when it is written and
/// stops being true without anybody editing it. That has already happened on this
/// board: <c>SECURITY.md</c> denied that anything in the plugin's own source called
/// the item selection, a caller arrived, and the sentence survived four days and one
/// edit to the file that did not re-read it. It is recorded on #85. What makes this
/// paragraph the same case is that the thing it denies is exactly what #183 is for,
/// so the sentence goes stale on the day that issue lands rather than at some
/// unpredictable moment.
///
/// The half a reader trusts most is the quiet one. "Nothing calls it" is what tells
/// somebody reading the cost section that no number here came from a measurement,
/// and it is the sentence a person would go on believing while a run recorded
/// measurements every night.
///
/// Both directions, because they fail differently. A caller arriving while the
/// sentence stands is the drift above. The sentence disappearing while nothing calls
/// it leaves the section claiming less than the tree can promise, and it is the
/// direction that would let the paragraph be quietly rewritten into an assurance.
///
/// The other half is read too. "Is built" is a claim about the tree as much as
/// "nothing calls it" is, and a denial about a thing that is not there would be a
/// different sentence saying something weaker. So the folding entry points are
/// resolved rather than assumed.
///
/// WHAT THIS DOES NOT DO, and the first bound is the one every search of this shape
/// here carries. It matches names in source text. A caller that reached the folding
/// through an interface, a delegate or a name of its own would be invisible to it,
/// exactly as <c>SecurityPolicyClaimTests</c> and <c>LimitsPageAbsenceTests</c> say
/// of their own searches. What it is written against is the ordinary case, which is
/// a run calling the ledger by name.
///
/// It reads the plugin project and never this one. A test calling the folding is how
/// that arithmetic is proved to work, so a search counting one would refuse the
/// suite that holds it.
///
/// It has no opinion about the rest of that section. The claim that no throughput
/// number is quoted, the disk arithmetic and the extraction ceiling are three
/// separate readings, and none of them is made here.
///
/// Each leg carries a fixture it has to refuse, under
/// <c>Fixtures/readme-run-cost/</c>, and one neighbour that breaks nothing. The
/// neighbour is the shape the tree really carries: <c>Estimation/DryRun.cs</c> names
/// the ledger and reads a measurement out of it, which is not folding one in, and a
/// search that could not tell those apart would refuse this plugin today.
/// </remarks>
public class ReadmeRunCostTests
{
    /// <summary>
    /// The section the sentence lives in, by title.
    /// </summary>
    private const string Section = "What a run costs";

    /// <summary>
    /// The sentence itself, as the page writes it.
    /// </summary>
    private const string Denial = "is built and nothing calls it";

    /// <summary>
    /// The folding arithmetic, by the names a caller would have to write.
    /// </summary>
    /// <remarks>
    /// The type that holds the arithmetic, and the ledger method that reaches it.
    /// Reading a measurement back out is a different member on the same ledger and is
    /// deliberately not here, because the paragraph denies the folding and not the
    /// reading.
    /// </remarks>
    private const string FoldingType = "ThroughputFactor";

    private const string FoldingLedger = "CalibrationLedger";

    private const string FoldingCall = ".Record(";

    /// <summary>
    /// Where the arithmetic itself lives, and the one place naming it is not a call
    /// from outside.
    /// </summary>
    private const string HomeDirectory = "Calibration";

    [Fact]
    public void Nothing_outside_the_calibration_folder_folds_a_measured_item()
    {
        var callers = PluginSources()
            .Where(path => !Inside(path, HomeDirectory))
            .Where(path => Folds(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            callers.Count == 0,
            $"README.md says the arithmetic that folds measured items into a factor \"{Denial}\", and these call it: {string.Join(", ", callers)}. The sentence is what tells a reader that no number in that section came from a measurement.");
    }

    /// <summary>
    /// The other direction. A sentence removed while the tree still answers nothing
    /// leaves the section claiming less than this plugin can promise, and nothing
    /// else would notice.
    /// </summary>
    [Fact]
    public void The_section_still_denies_a_caller_the_tree_does_not_have()
    {
        var section = SectionOnThePage();

        Assert.Contains(Denial, section, StringComparison.Ordinal);
    }

    /// <summary>
    /// The half of the sentence the leg above does not read. A denial about
    /// arithmetic that is not in the tree would be a weaker sentence than the one
    /// the page makes, and the search would pass over an absence either way.
    /// </summary>
    [Fact]
    public void The_arithmetic_the_section_says_is_built_is_in_the_tree()
    {
        var home = PluginSources().Where(path => Inside(path, HomeDirectory)).ToList();

        Assert.Contains(home, path => File.ReadAllText(path).Contains(FoldingType + " Measured(", StringComparison.Ordinal));
        Assert.Contains(home, path => File.ReadAllText(path).Contains("public CalibratedThroughput Record(", StringComparison.Ordinal));
    }

    [Fact]
    public void The_search_finds_a_caller_in_a_source_that_folds_one_in()
    {
        Assert.True(Folds(Fixture("records-a-measured-item")));
    }

    /// <summary>
    /// The neighbour that has to stay accepted, and it is the shape the tree really
    /// carries rather than an invented one. Without it a search matching the ledger's
    /// name alone would look right and would refuse this plugin today.
    /// </summary>
    [Fact]
    public void The_search_accepts_a_source_that_only_reads_a_measurement_back()
    {
        Assert.False(Folds(Fixture("reads-a-measurement-back")));
    }

    /// <summary>
    /// Guards the reach of the search rather than the sources, so a search reading an
    /// empty set cannot pass the first leg.
    /// </summary>
    [Fact]
    public void The_search_reads_the_plugin_project_outside_that_folder()
    {
        var read = PluginSources()
            .Where(path => !Inside(path, HomeDirectory))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Contains("DryRun.cs", read, StringComparer.Ordinal);
    }

    /// <summary>
    /// Guards the reader rather than the page, for the reason its neighbours in
    /// <see cref="ReadmeClaimsTests"/> carry one: a reader that stopped finding the
    /// section would make the leg that reads it pass over an empty string.
    /// </summary>
    [Fact]
    public void The_section_reader_returns_one_section_and_stops_at_the_next_heading()
    {
        var section = SectionOnThePage();

        Assert.NotEmpty(section);
        Assert.DoesNotContain("\n## ", section, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a source folds a measured item in, as opposed to reading one back.
    /// </summary>
    private static bool Folds(string source) =>
        source.Contains(FoldingType, StringComparison.Ordinal)
        || (source.Contains(FoldingLedger, StringComparison.Ordinal)
            && source.Contains(FoldingCall, StringComparison.Ordinal));

    private static string SectionOnThePage()
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        var heading = "## " + Section + "\n";
        var start = page.IndexOf(heading, StringComparison.Ordinal);

        if (start < 0)
        {
            return string.Empty;
        }

        var body = page[(start + heading.Length)..];
        var next = body.IndexOf("\n## ", StringComparison.Ordinal);

        return next < 0 ? body : body[..next];
    }

    private static bool Inside(string path, string directory) =>
        path.Contains(
            string.Concat(Path.DirectorySeparatorChar, directory, Path.DirectorySeparatorChar),
            StringComparison.Ordinal);

    private static List<string> PluginSources() =>
        Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !Inside(path, "obj") && !Inside(path, "bin"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(ThisFile())!,
            "Fixtures",
            "readme-run-cost",
            name + ".cs.fixture"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
