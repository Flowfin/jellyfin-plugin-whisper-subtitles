using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The boundary list in <c>docs/untrusted-input.md</c> is what a third backend or a
/// new surface is checked against, and a list like that is worth reading only while
/// it is true.
/// </summary>
/// <remarks>
/// Two halves, and they fail differently on purpose.
///
/// The first resolves the endings of the list. Every entry names the type that holds
/// its bound and the class that feeds it the hostile case, and both are looked up
/// rather than believed: the type against the plugin assembly, the class against the
/// tests this assembly runs. A bound that was renamed or a hostile case that was
/// never written turns this red instead of going on standing for something nobody
/// holds.
///
/// The second reads the plugin's own source for the shapes that list forbids. The
/// first half cannot see them: a second process launch beside the injected runner
/// breaks no existing test, because the seam it went around is still there and still
/// passes. What has to be refused is the code.
///
/// The vocabularies are assembled from fragments so this file holds none of the
/// literals it looks for, which is the neighbouring scanner's reasoning and the same
/// trap: a scanner excluded from its own scan is a hole exactly where somebody would
/// put the thing it forbids.
///
/// WHAT IS NOT ASSERTED. Whether a bound is the right bound, and whether the hostile
/// case is hostile enough, are judgements no reading of this tree makes; the review
/// is where a wrong one is caught. And the source rules read tokens, so a launch
/// reached through reflection or a client built by a helper under another name walks
/// past them. That is stated in the document as well, because a reader who trusts
/// this to be complete is worse off than one who knows its edge.
/// </remarks>
public class UntrustedInputTests
{
    private const string Heading = "## The boundaries, and what bounds each one";

    private const string Dot = ".";

    /// <summary>
    /// Starting a process anywhere but the one injected runner. A second launch is
    /// a program run with arguments no test can see.
    /// </summary>
    private static readonly string[] _processLaunch =
    [
        "new Process" + "(",
        "new Process" + " {",
        "new Process" + "Start" + "Info",
        "Process" + Dot + "Start" + "("
    ];

    /// <summary>
    /// Handing a program one string instead of a vector, or going through a shell.
    /// The bound on the first two entries of the list is the argument vector, and it
    /// is a bound only while the alternative cannot be expressed.
    /// </summary>
    private static readonly string[] _commandLine =
    [
        "Info" + Dot + "Arguments",
        "UseShellExecute" + " = true",
        "cmd" + Dot + "exe",
        "/bin/" + "sh"
    ];

    /// <summary>
    /// Reaching the network from outside the backend that owns the endpoint, past
    /// the size ceiling, the declared-type check and the injected handler.
    /// </summary>
    private static readonly string[] _httpClient =
    [
        "new Http" + "Client" + "(",
        "Http" + "ClientHandler",
        "Web" + "Request",
        "Web" + "Client" + "("
    ];

    /// <summary>
    /// The one file allowed to start a process, and the one folder allowed to build
    /// an HTTP client. Named rather than pattern matched, so widening the permission
    /// is a change somebody makes on purpose.
    /// </summary>
    private static readonly Dictionary<string, string[]> _allowed = new(StringComparer.Ordinal)
    {
        [nameof(_processLaunch)] = ["SystemProcessRunner.cs"],
        [nameof(_commandLine)] = [],
        [nameof(_httpClient)] = ["RemoteWhisperBackend.cs"],
    };

    private static readonly Regex _bound = new(@"Bounded by `([A-Za-z0-9_]+)`", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static readonly Regex _hostile = new(@"Hostile case in `([A-Za-z0-9_]+)`", RegexOptions.None, TimeSpan.FromSeconds(5));

    public static TheoryData<string> EveryEntry =>
        new(Entries(Section(Document())).ToArray());

    public static TheoryData<string> EveryPluginSourceFile =>
        new(PluginSourceFiles().Select(Path.GetFileName).ToArray()!);

    public static TheoryData<string, string> EveryRuleAndItsFixture =>
        new()
        {
            { nameof(_processLaunch), "launches-its-own-process" },
            { nameof(_commandLine), "builds-a-command-line" },
            { nameof(_httpClient), "makes-its-own-http-client" }
        };

    [Fact]
    public void The_reader_finds_the_section_and_every_boundary_in_it()
    {
        // Guards every leg below. A reader that matched no section, or matched one
        // and found no lines in it, would report a list whose every claim resolves,
        // whatever the list said, and it would do it in green.
        var entries = Entries(Section(Document()));

        Assert.True(entries.Count > 5, $"the reader found {entries.Count} entries under {Heading}");
        Assert.Contains(entries, entry => entry.Contains("model path", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Contains("configuration file", StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_stops_at_the_next_section_rather_than_running_to_the_end_of_the_file()
    {
        var section = Section(Document());

        Assert.Contains("The configuration file", section, StringComparison.Ordinal);
        Assert.DoesNotContain("## The shapes this list forbids", section, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_boundary_names_the_type_that_holds_it_and_the_test_that_attacks_it(string entry)
    {
        Assert.True(
            Bounds(entry).Count > 0,
            $"this entry names no type holding its bound: {entry}");

        Assert.True(
            HostileCases(entry).Count > 0,
            $"this entry names no test feeding it the hostile case: {entry}");
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_named_bound_is_a_type_this_plugin_carries(string entry)
    {
        var carried = TypesThePluginCarries();

        foreach (var named in Bounds(entry))
        {
            Assert.True(
                carried.Contains(named),
                $"{named} is named as holding a bound and this plugin's assembly carries no type by that name");
        }
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_named_hostile_case_is_a_class_this_suite_runs(string entry)
    {
        var running = ClassesThisSuiteRunsTestsIn();

        foreach (var named in HostileCases(entry))
        {
            Assert.True(
                running.Contains(named),
                $"{named} is named as feeding a hostile case and this assembly runs no tests in a class by that name");
        }
    }

    [Fact]
    public void The_reader_refuses_an_entry_naming_a_type_this_plugin_does_not_carry()
    {
        var entry = Assert.Single(Entries(Section(Fixture("names-a-type-that-is-not-there", "md"))));

        Assert.NotEmpty(Bounds(entry));
        Assert.DoesNotContain(Bounds(entry), named => TypesThePluginCarries().Contains(named));
    }

    [Fact]
    public void The_reader_refuses_an_entry_naming_a_hostile_case_the_suite_does_not_run()
    {
        var entry = Assert.Single(Entries(Section(Fixture("names-a-hostile-case-that-is-not-there", "md"))));

        Assert.NotEmpty(HostileCases(entry));
        Assert.DoesNotContain(HostileCases(entry), named => ClassesThisSuiteRunsTestsIn().Contains(named));
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_names_neither()
    {
        var entry = Assert.Single(Entries(Section(Fixture("names-neither", "md"))));

        Assert.Empty(Bounds(entry));
        Assert.Empty(HostileCases(entry));
    }

    [Fact]
    public void The_reader_refuses_a_section_with_no_entries_in_it()
    {
        // The fixture for the guard rather than for a rule. A section whose lines
        // stopped being lines the reader recognises reads as a list with nothing in
        // it, which is the shape that passes every other leg for free.
        Assert.Empty(Entries(Section(Fixture("no-entries-at-all", "md"))));
    }

    [Fact]
    public void The_document_that_breaks_no_rule_is_accepted()
    {
        // Without this a reader that refused every section would pass every leg
        // above.
        var entries = Entries(Section(Fixture("clean", "md")));

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.NotEmpty(Bounds(entry)));
        Assert.All(entries, entry => Assert.NotEmpty(HostileCases(entry)));
        Assert.Contains(entries, entry => HostileCases(entry).Contains(nameof(UntrustedInputTests)));
    }

    [Fact]
    public void The_scanner_can_see_the_plugin_sources_it_judges()
    {
        // The same guard the reader has. A scanner that found no files would report
        // a plugin with one process launch and one HTTP client whatever the plugin
        // did.
        var files = PluginSourceFiles();

        Assert.True(files.Count > 40, $"only {files.Count} plugin source files were found beside {ThisFile()}");
        Assert.Contains("SystemProcessRunner.cs", files.Select(Path.GetFileName));
        Assert.Contains("RemoteWhisperBackend.cs", files.Select(Path.GetFileName));
    }

    [Fact]
    public void The_scanner_would_see_a_shape_it_was_shown()
    {
        // The vocabularies are assembled from fragments, so a typo in the assembly
        // would leave a rule matching nothing and passing forever.
        foreach (var forbidden in _processLaunch.Concat(_commandLine).Concat(_httpClient))
        {
            Assert.False(string.IsNullOrWhiteSpace(forbidden));
            Assert.Contains(forbidden, "        var x = " + forbidden + "something;", StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(EveryPluginSourceFile))]
    public void Nothing_starts_a_process_outside_the_injected_runner(string fileName)
    {
        AssertNoneOf(fileName, nameof(_processLaunch), "starts a process, and one launch behind IProcessRunner is what makes the argument vector a bound");
    }

    [Theory]
    [MemberData(nameof(EveryPluginSourceFile))]
    public void Nothing_hands_a_program_one_string_or_a_shell(string fileName)
    {
        AssertNoneOf(fileName, nameof(_commandLine), "builds a command line or reaches a shell, which puts a quoting rule between an operator's path and what runs");
    }

    [Theory]
    [MemberData(nameof(EveryPluginSourceFile))]
    public void Nothing_builds_an_http_client_outside_the_remote_backend(string fileName)
    {
        AssertNoneOf(fileName, nameof(_httpClient), "reaches the network past the backend that owns the endpoint and the handler every test injects");
    }

    [Theory]
    [MemberData(nameof(EveryRuleAndItsFixture))]
    public void Each_rule_refuses_its_own_fixture_and_no_other(string rule, string fixtureName)
    {
        var fixture = Fixture(fixtureName, "cs");

        Assert.True(Trips(Vocabulary(rule), fixture), $"{fixtureName} trips no token in {rule}");

        foreach (var other in _allowed.Keys.Where(name => !string.Equals(name, rule, StringComparison.Ordinal)))
        {
            Assert.False(
                Trips(Vocabulary(other), fixture),
                $"{fixtureName} also trips {other}, so neither rule is proven by it");
        }
    }

    [Fact]
    public void The_source_that_breaks_no_rule_is_accepted()
    {
        var clean = Fixture("clean", "cs");

        Assert.All(_allowed.Keys, rule => Assert.False(Trips(Vocabulary(rule), clean)));
    }

    [Fact]
    public void No_fixture_is_a_document_or_a_source_anything_else_reads()
    {
        // The extension is the whole of what keeps these out of the way of a check
        // that walks the tree for markdown or compiles the sources beside it, and a
        // fixture that acquired a plain one would be a second boundary list saying
        // things about this repository that are deliberately untrue. The README
        // beside them is the one file in that directory that is true.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".md.fixture", StringComparison.Ordinal)
                || path.EndsWith(".cs.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    private static void AssertNoneOf(string fileName, string rule, string why)
    {
        if (_allowed[rule].Contains(fileName, StringComparer.Ordinal))
        {
            return;
        }

        var source = Read(fileName);

        foreach (var forbidden in Vocabulary(rule))
        {
            Assert.False(
                source.Contains(forbidden, StringComparison.Ordinal),
                $"{fileName} {why}: it carries {forbidden}");
        }
    }

    private static bool Trips(IEnumerable<string> vocabulary, string source) =>
        vocabulary.Any(forbidden => source.Contains(forbidden, StringComparison.Ordinal));

    private static string[] Vocabulary(string rule) => rule switch
    {
        nameof(_processLaunch) => _processLaunch,
        nameof(_commandLine) => _commandLine,
        nameof(_httpClient) => _httpClient,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "no vocabulary by that name"),
    };

    private static List<string> Bounds(string entry) => Named(_bound, entry);

    private static List<string> HostileCases(string entry) => Named(_hostile, entry);

    private static List<string> Named(Regex ending, string entry) =>
        ending.Matches(entry).Select(match => match.Groups[1].Value).ToList();

    private static HashSet<string> TypesThePluginCarries() =>
        typeof(Plugin).Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ClassesThisSuiteRunsTestsIn() =>
        typeof(UntrustedInputTests).Assembly
            .GetTypes()
            .Where(type => type.GetMethods().Any(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The bulleted entries of a section, each one joined back into a single line.
    /// </summary>
    /// <remarks>
    /// An entry wraps across several lines in the source and both endings this reads
    /// can sit at the end of any of them, so an entry read line by line would lose an
    /// ending exactly when the line before it happened to be long.
    /// </remarks>
    private static List<string> Entries(string section)
    {
        var entries = new List<string>();

        foreach (var line in section.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                entries.Add(line[2..].Trim());
            }
            else if (entries.Count == 0)
            {
                continue;
            }
            else if (line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                entries[^1] = entries[^1] + " " + line.Trim();
            }
            else
            {
                break;
            }
        }

        return entries;
    }

    private static string Section(string document)
    {
        var start = document.IndexOf(Heading, StringComparison.Ordinal);

        Assert.True(start >= 0, $"no section headed {Heading} was found");

        var body = document[(start + Heading.Length)..];
        var next = body.IndexOf("\n## ", StringComparison.Ordinal);

        return next < 0 ? body : body[..next];
    }

    private static string Fixture(string name, string kind) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + "." + kind + ".fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "untrusted-input");

    private static string Read(string fileName) =>
        File.ReadAllText(PluginSourceFiles().Single(path => Path.GetFileName(path) == fileName));

    /// <summary>
    /// The plugin's own sources, out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason the
    /// neighbouring scanners give: sources are not copied beside the assembly, and a
    /// path walked upwards from one depends on the configuration and the framework
    /// it was built for. The build directories are left out because what is in them
    /// is generated and a scan of them would judge the compiler's work.
    /// </remarks>
    private static List<string> PluginSourceFiles()
    {
        var root = Path.Combine(
            Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!,
            "Jellyfin.Plugin.WhisperSubtitles");

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string Document() =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!,
            "docs",
            "untrusted-input.md"));

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
