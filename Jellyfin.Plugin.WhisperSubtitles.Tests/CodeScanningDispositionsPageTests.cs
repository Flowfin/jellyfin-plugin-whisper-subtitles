using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The code-scanning register argues in a vocabulary of four states and points at places
/// in this tree, and this reads the half of it a checkout can answer.
/// </summary>
/// <remarks>
/// The comparison that gives the register its force is elsewhere and is a different
/// question: <c>.github/scripts/refuse-an-undisposed-alert.sh</c> asks whether the rules
/// the platform reports open and the rules the page decides about are the same set, and
/// it needs a fetch, so it runs in the workflow rather than here. Every test in this
/// project runs with the machine offline.
///
/// What is left for a checkout is the two ways that comparison can be true and useless.
/// The first is a vocabulary that has drifted: the page declares the states it writes in
/// prose and the script decides them in a <c>case</c>, and a state added to one side and
/// not the other either passes an entry nobody defined or refuses one the page tells a
/// reader to write. The second is a page whose evidence has gone stale - a command
/// pointing at a file this tree no longer has, or a search for a symbol that has been
/// renamed - which is the defect the neighbouring page on this repository carried three
/// times before anything read it.
///
/// The third leg is about a shape rather than a fact. Every command on this page that
/// reads the alert set is handed to the reader with nothing pasted under it, because that
/// set moves every hour and a paste of it is a claim that ages in silence. A frozen count
/// beside a rule is exactly what the register was written instead of.
///
/// WHAT THIS DOES NOT DO. It says nothing about whether a disposition is right, whether a
/// reason still applies, or whether a debt is being paid. It reads a vocabulary, the
/// resolvability of the page's own pointers, and the absence of one shape; a paragraph
/// drawing a wrong conclusion from correct evidence passes here, and the review is where
/// that is caught.
///
/// Each leg carries a fixture it has to refuse, under
/// <c>Fixtures/code-scanning-dispositions/</c>, so the refusal is executed rather than
/// asserted about.
/// </remarks>
public class CodeScanningDispositionsPageTests
{
    /// <summary>
    /// The register, relative to the repository root.
    /// </summary>
    private const string Register = "docs/code-scanning-dispositions.md";

    /// <summary>
    /// The script that decides the same vocabulary one route over.
    /// </summary>
    private const string Guard = ".github/scripts/refuse-an-undisposed-alert.sh";

    /// <summary>
    /// An entry heading, which is a rule id and the state decided about it. A rule id
    /// carries no space, which is what separates an entry from a prose heading that
    /// happens to have a comma in it.
    /// </summary>
    private static readonly Regex Entry = new(
        @"^## (?<rule>[^ ,]+), (?<state>.+)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A state as the page declares it, which is a backticked name opening the paragraph
    /// that says what it claims.
    /// </summary>
    private static readonly Regex Declared = new(
        @"^`(?<state>[a-z ]+)` is a rule",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A state as the script decides it, which is the alternation of its <c>case</c>.
    /// </summary>
    private static readonly Regex Decided = new(
        @"^\s*""(?<states>[a-z ""|]+)""\)\s*;;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A path the page hands a reader a command over, in the two shapes it writes one:
    /// after the pathspec separator, and as the argument of a search over one file.
    /// </summary>
    private static readonly Regex Pathspec = new(
        @"-- '?(?<path>[A-Za-z0-9._/-]+(?:\.cs|\.md|\.yml|\.yaml|\.sh))'?",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_and_the_script_write_the_same_four_states()
    {
        var declared = StatesThePageDeclares(Lines());
        var decided = StatesTheScriptDecides(GuardLines());

        Assert.True(
            declared.SequenceEqual(decided, StringComparer.Ordinal),
            $"{Register} declares {Show(declared)} and {Guard} decides {Show(decided)}. A state on one side only either passes an entry nobody defined or refuses one the page tells a reader to write.");
    }

    [Fact]
    public void Every_entry_is_in_a_state_the_page_declares()
    {
        var declared = StatesThePageDeclares(Lines());
        var undeclared = Entries(Lines())
            .Where(entry => !declared.Contains(entry.State, StringComparer.Ordinal))
            .Select(entry => $"{entry.Rule} is \"{entry.State}\"")
            .ToList();

        Assert.True(
            undeclared.Count == 0,
            $"{Register} records {Show(undeclared)}, and the states it declares are {Show(declared)}. An entry in a state the page never defined is a heading rather than a decision.");
    }

    [Fact]
    public void Every_place_the_page_points_at_is_in_this_tree()
    {
        var missing = PathsNamed(Lines())
            .Where(path => !File.Exists(Path.Combine(RepositoryRoot(), path)))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{Register} hands a reader a command over {Show(missing)}, and this tree has no such file. The reason above the command then rests on evidence nobody can re-run.");
    }

    [Fact]
    public void The_page_freezes_no_alert_set_under_the_command_that_reads_it()
    {
        var frozen = FrozenFetches(Lines());

        Assert.True(
            frozen.Count == 0,
            $"{Register} pastes output under the alert fetch at {Show(frozen)}. That set moves every hour, this page exists because counting it was the thing nobody could keep up with, and a paste of one ages in silence.");
    }

    [Fact]
    public void A_register_whose_states_the_page_declares_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader refusing every page
        // would satisfy each refusal leg below and say nothing about the real one.
        var lines = Fixture("clean");
        var declared = StatesThePageDeclares(lines);

        Assert.All(Entries(lines), entry => Assert.Contains(entry.State, declared, StringComparer.Ordinal));
        Assert.Empty(FrozenFetches(lines));
    }

    [Fact]
    public void A_register_using_a_state_it_never_declared_is_refused()
    {
        var lines = Fixture("state-the-page-never-declared");

        Assert.Equal(
            ["cs/example-second"],
            Entries(lines)
                .Where(entry => !StatesThePageDeclares(lines).Contains(entry.State, StringComparer.Ordinal))
                .Select(entry => entry.Rule));
    }

    [Fact]
    public void A_register_that_freezes_the_alert_set_is_refused()
    {
        // The shape this register was written instead of. A count under the fetch reads as
        // the state of the board, and it stops being that the next time a guard lands.
        Assert.NotEmpty(FrozenFetches(Fixture("freezes-the-alert-set")));
    }

    [Fact]
    public void A_register_pointing_at_a_file_this_tree_lacks_is_refused()
    {
        Assert.Equal(
            ["docs/a-page-this-tree-does-not-have.md"],
            PathsNamed(Fixture("points-at-a-file-this-tree-lacks"))
                .Where(path => !File.Exists(Path.Combine(RepositoryRoot(), path))));
    }

    [Fact]
    public void No_fixture_is_a_page_anything_else_reads()
    {
        // Every fixture here is a register that is deliberately wrong, and each is kept
        // under an extension no reader of docs walks.
        var fixtures = Directory.GetFiles(FixtureDirectory());

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".md.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// The states the page declares, in the order it declares them.
    /// </summary>
    /// <param name="lines">The page, line by line.</param>
    /// <returns>Each declared state.</returns>
    private static List<string> StatesThePageDeclares(string[] lines) =>
        lines
            .Select(line => Declared.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["state"].Value)
            .ToList();

    /// <summary>
    /// The states the script accepts, taken out of the alternation of its <c>case</c>.
    /// </summary>
    /// <param name="lines">The script, line by line.</param>
    /// <returns>Each accepted state, in the order the script writes them.</returns>
    private static List<string> StatesTheScriptDecides(string[] lines)
    {
        var match = lines
            .Select(line => Decided.Match(line))
            .FirstOrDefault(candidate => candidate.Success);

        Assert.True(
            match is not null,
            $"{Guard} no longer carries the case that decides an entry's state, so the page's vocabulary is compared against nothing.");

        return match!.Groups["states"].Value
            .Split('|')
            .Select(state => state.Trim('"'))
            .ToList();
    }

    /// <summary>
    /// The entries the page records.
    /// </summary>
    /// <param name="lines">The page, line by line.</param>
    /// <returns>The rule and the state of each entry.</returns>
    private static List<(string Rule, string State)> Entries(string[] lines) =>
        lines
            .Select(line => Entry.Match(line))
            .Where(match => match.Success)
            .Select(match => (match.Groups["rule"].Value, match.Groups["state"].Value))
            .ToList();

    /// <summary>
    /// The files in this tree the page hands a reader a command over.
    /// </summary>
    /// <remarks>
    /// Only the commands that read the tree carry a path this can resolve. The alert fetch
    /// names a repository over the network and is not one of them, and the pattern's
    /// requirement of a pathspec separator is what leaves it out.
    /// </remarks>
    /// <param name="lines">The page, line by line.</param>
    /// <returns>Each path, deduplicated, in the order the page first names it.</returns>
    private static List<string> PathsNamed(string[] lines) =>
        lines
            .SelectMany(line => Pathspec.Matches(line).Select(match => match.Groups["path"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The fenced blocks whose command reads the alert set and which have something pasted
    /// under it.
    /// </summary>
    /// <param name="lines">The page, line by line.</param>
    /// <returns>One entry per such block, naming the line the command sits on.</returns>
    private static List<string> FrozenFetches(string[] lines)
    {
        var fence = "``" + "`";
        var frozen = new List<string>();
        var inside = false;
        var reading = false;
        var opened = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(fence, StringComparison.Ordinal))
            {
                inside = !inside;
                reading = false;
                continue;
            }

            if (!inside)
            {
                continue;
            }

            if (lines[i].StartsWith("gh api", StringComparison.Ordinal))
            {
                reading = true;
                opened = i + 1;
                continue;
            }

            // A continuation is still the command. What is pasted is a line the command
            // did not start and that does not end in the continuation mark above it.
            if (reading && !lines[i - 1].TrimEnd().EndsWith('\\'))
            {
                frozen.Add($"line {opened}");
                reading = false;
            }
        }

        return frozen;
    }

    private static string Show(IEnumerable<string> entries)
    {
        var listed = string.Join(", ", entries);

        return listed.Length == 0 ? "nothing" : listed;
    }

    /// <summary>
    /// A deliberately wrong register, read out of the checkout.
    /// </summary>
    /// <param name="name">The fixture's name.</param>
    /// <returns>The fixture, line by line.</returns>
    private static string[] Fixture(string name) =>
        Split(File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture")));

    private static string FixtureDirectory() =>
        Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles.Tests", "Fixtures", "code-scanning-dispositions");

    /// <summary>
    /// The register, read out of the checkout rather than out of a copy beside the
    /// assembly, for the reason its neighbours in this suite give.
    /// </summary>
    /// <returns>The page, line by line, with the line endings removed.</returns>
    private static string[] Lines() =>
        Split(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "code-scanning-dispositions.md")));

    private static string[] GuardLines() =>
        Split(File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "scripts", "refuse-an-undisposed-alert.sh")));

    private static string[] Split(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
