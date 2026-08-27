using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Nothing in this plugin reads the wall clock. A type that wants to know when
/// something happened is handed the moment by its caller.
/// </summary>
/// <remarks>
/// The suite half of this rule is held already, by <c>DeterminismTests</c>, whose
/// subject is the test project it sits in. The plugin was outside it, and outside
/// everything else: <c>UntrustedInputTests</c> scans the plugin's sources and has no
/// clock among the shapes it forbids. So the first plugin type to stamp itself with
/// the machine's clock would have landed green, and the tests written against it
/// would have been the ones that fail at midnight.
///
/// Nothing reads a clock here today, so this is not a repair. It is the rule kept
/// while the property is still free: the type that most wanted a moment,
/// <see cref="Jellyfin.Plugin.WhisperSubtitles.Calibration.CalibratedThroughput"/>,
/// takes one as a parameter and says why in its own remarks, and this is that
/// decision made refusable rather than restated.
///
/// The permitted set is EMPTY, and a leg below asserts that it is. This is the
/// tightest rule of this family the repository could carry: the neighbouring scan
/// permits one file to start a process and one to build an HTTP client, because
/// something has to, and here nothing does. Widening it is therefore a change to
/// this file's own claim about itself rather than a line added to a list.
///
/// The subject is the SOURCE rather than the behaviour, for the reason the
/// neighbouring scanners give: a type that stamps itself with the clock passes every
/// test on the machine it was written on, and that is the whole problem. The
/// vocabulary is assembled from fragments so this file holds none of the literals it
/// looks for; a scanner excluded from its own scan is a hole exactly where somebody
/// would put the thing it forbids, and here it would also break the neighbouring
/// rule that reads this project.
///
/// WHAT IS NOT ASSERTED. The scan reads tokens, so a moment reached through
/// reflection, through a helper under another name, or through a server interface
/// that returns one walks past it. It says nothing about whether a moment a caller
/// handed in is the right moment. And its subject is this plugin's sources: a clock
/// read inside a package this plugin calls is not something any reading of this tree
/// can see.
/// </remarks>
public class PluginClockTests
{
    private const string Dot = ".";

    /// <summary>
    /// Ways to acquire a moment from the machine rather than from a caller. The
    /// elapsed-time readings sit beside the dated ones because they are the same
    /// dependency wearing another name: a type that times itself is a type whose
    /// answer is about the machine it ran on.
    /// </summary>
    private static readonly string[] _wallClock =
    [
        "DateTime" + Dot + "Now",
        "DateTime" + Dot + "UtcNow",
        "DateTimeOffset" + Dot + "Now",
        "DateTimeOffset" + Dot + "UtcNow",
        "Stopwatch" + Dot + "StartNew",
        "Environment" + Dot + "Tick" + "Count",
        "TimeProvider" + Dot + "System"
    ];

    /// <summary>
    /// The plugin files allowed to read one. Nothing does, and the emptiness is the
    /// rule rather than a state it happens to be in.
    /// </summary>
    private static readonly string[] _permitted = [];

    public static TheoryData<string> EveryPluginSourceFile =>
        new(PluginSourceFiles().Select(Path.GetFileName).ToArray()!);

    [Fact]
    public void The_scanner_can_see_the_plugin_sources_it_judges()
    {
        // Guards every leg below. A scanner that found no files would report a plugin
        // that reads no clock whatever the plugin did, and it would do it in green.
        var files = PluginSourceFiles();

        Assert.True(files.Count > 40, $"only {files.Count} plugin source files were found beside {ThisFile()}");
        Assert.Contains("CalibratedThroughput.cs", files.Select(Path.GetFileName));
        Assert.Contains("Plugin.cs", files.Select(Path.GetFileName));
    }

    [Fact]
    public void The_scanner_would_see_a_shape_it_was_shown()
    {
        // The other half of guarding it. The vocabulary is assembled from fragments,
        // so a typo in that assembly would leave the rule matching nothing and
        // passing forever.
        foreach (var forbidden in _wallClock)
        {
            Assert.False(string.IsNullOrWhiteSpace(forbidden));
            Assert.Contains(forbidden, "        var x = " + forbidden + "something;", StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_plugin_file_is_permitted_to_read_one()
    {
        // The rule stated as an assertion rather than as an empty array somebody could
        // fill in passing. A file added there has to be argued for in the change that
        // adds it, because this leg is what it breaks first.
        Assert.Empty(_permitted);
    }

    [Theory]
    [MemberData(nameof(EveryPluginSourceFile))]
    public void Nothing_in_the_plugin_reads_a_wall_clock(string fileName)
    {
        if (_permitted.Contains(fileName, StringComparer.Ordinal))
        {
            return;
        }

        var source = Read(fileName);

        foreach (var token in _wallClock)
        {
            Assert.False(
                source.Contains(token, StringComparison.Ordinal),
                $"{fileName} names {token}, and a moment this plugin needs arrives from its caller");
        }
    }

    [Fact]
    public void The_fixture_that_stamps_itself_from_the_machine_is_refused()
    {
        Assert.True(Trips(Fixture("reads-the-wall-clock")), "the fixture trips no token in the vocabulary");
    }

    [Fact]
    public void The_neighbour_that_takes_the_moment_it_was_given_is_accepted()
    {
        // The near miss rather than a distant one. This fixture differs from the one
        // above in where the moment comes from and in nothing else, so a rule coarse
        // enough to refuse any source naming a moment would pass its own fixture and
        // fail here.
        var neighbour = Fixture("takes-the-moment-it-was-given");

        Assert.False(Trips(neighbour));
        Assert.Contains("measuredAt", neighbour, StringComparison.Ordinal);
        Assert.Contains("DateTimeOffset", neighbour, StringComparison.Ordinal);
    }

    [Fact]
    public void No_fixture_is_compiled_into_the_suite_or_counted_as_a_plugin_source()
    {
        // A fixture that acquired a plain extension would be a permanently red scan
        // with no legal repair, so the extension is the whole of what keeps these out
        // of the build, and it is checked rather than trusted.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".cs.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
        Assert.DoesNotContain(PluginSourceFiles(), path => path.StartsWith(FixtureDirectory(), StringComparison.Ordinal));
    }

    private static bool Trips(string source) =>
        _wallClock.Any(token => source.Contains(token, StringComparison.Ordinal));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".cs.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "plugin-clock");

    private static string Read(string fileName) =>
        File.ReadAllText(PluginSourceFiles().Single(path => Path.GetFileName(path) == fileName));

    /// <summary>
    /// The plugin's own sources, out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason the neighbouring
    /// scanners give: sources are not copied beside the assembly, and a path walked
    /// upwards from one depends on the configuration and the framework it was built
    /// for. The build directories are left out because what is in them is generated
    /// and a scan of them would judge the compiler's work.
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

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
