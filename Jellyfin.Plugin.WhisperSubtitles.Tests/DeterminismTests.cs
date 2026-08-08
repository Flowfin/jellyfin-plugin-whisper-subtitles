using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A suite that is green on one machine and red on another teaches contributors to
/// rerun rather than to read, and the three usual causes are ambient network
/// access, the wall clock, and shared temporary directories.
/// </summary>
/// <remarks>
/// None of the three is present in this tree today, so what is here is not a
/// repair. It is the check that keeps the property once it stops being obvious: the
/// first test that opens a socket, sleeps to make time pass, or leaves a directory
/// behind will be written by somebody who has a good local reason and no way of
/// knowing the rule.
///
/// The subject is the SOURCE of this project rather than its behaviour, and that is
/// deliberate rather than second best. A test that opens a socket passes on the
/// machine it was written on; that is the whole problem. What has to be refused is
/// the code, before it ever runs somewhere without a network.
///
/// The vocabularies below are assembled from fragments so this file holds none of
/// the literals it looks for. Otherwise the scanner would be its own first
/// violation, and the alternative - excluding this file from the scan - is a hole
/// exactly where somebody would put a test they knew was against the rule.
///
/// Each rule carries a fixture the scan has to refuse, under
/// <c>Fixtures/determinism/</c>, so the proof that it bites is in the tree rather
/// than in the memory of whoever last broke the suite on purpose. One fixture per
/// rule, because a rule is proven by a case that trips it AND NO OTHER: a file
/// breaking three of them cannot tell a scan that refuses the right thing from one
/// that refuses everything. The neighbour that has to stay accepted is there for the
/// same reason.
///
/// WHAT IS NOT ASSERTED HERE is that the system temporary directory is empty when a
/// run ends. A leg inside this suite runs while other classes are still using their
/// directories, and it cannot tell one of those from one a finished class forgot
/// without asking how old it is, which is the wall clock two legs below refuse. So
/// the discipline is checked as source, above, and the leftovers are counted from
/// outside the suite. The count and the command that produced it are in the pull
/// request that added this file.
/// </remarks>
public class DeterminismTests
{
    private const string Dot = ".";

    /// <summary>
    /// Ways to reach the network. A socket by any of its names, and the resolver in
    /// front of it.
    /// </summary>
    private static readonly string[] _network =
    [
        "new Socket" + "(",
        "new TcpClient" + "(",
        "new TcpListener" + "(",
        "new UdpClient" + "(",
        "new HttpListener" + "(",
        "Dns" + Dot,
        "Network" + "Stream"
    ];

    /// <summary>
    /// Ways to read the wall clock. A test that reads it compares against a moment
    /// that will never come again, and the failure is a run at midnight or on the
    /// last day of a month.
    /// </summary>
    private static readonly string[] _wallClock =
    [
        "DateTime" + Dot + "Now",
        "DateTime" + Dot + "UtcNow",
        "DateTimeOffset" + Dot + "Now",
        "DateTimeOffset" + Dot + "UtcNow",
        "Stopwatch" + Dot + "StartNew",
        "Environment" + Dot + "TickCount"
    ];

    /// <summary>
    /// Ways to make time pass by waiting for it. A suite that sleeps is a suite
    /// whose duration is a setting, and one that sleeps to let something else happen
    /// is a suite that fails on a loaded machine and nowhere else.
    /// </summary>
    private static readonly string[] _sleeping =
    [
        "Thread" + Dot + "Sleep",
        "Task" + Dot + "Delay",
        "SpinWait" + Dot + "SpinUntil"
    ];

    public static TheoryData<string> EverySourceFile =>
        new(SourceFiles().Select(Path.GetFileName).ToArray()!);

    public static TheoryData<string, string> EveryRuleAndItsFixture =>
        new()
        {
            { nameof(_network), "reaches-the-network" },
            { nameof(_wallClock), "reads-the-wall-clock" },
            { nameof(_sleeping), "waits-for-time-to-pass" }
        };

    [Fact]
    public void The_scanner_can_see_the_sources_it_judges()
    {
        // Guards every leg below. A scanner that found no files would report a
        // suite with no network access, no clock and no leftovers, whatever the
        // suite actually did, and it would do it in green.
        var files = SourceFiles();

        Assert.True(files.Count > 20, $"only {files.Count} source files were found beside {ThisFile()}");
        Assert.Contains(nameof(DeterminismTests) + ".cs", files.Select(Path.GetFileName));
        Assert.Contains("AtomicSubtitleFileTests.cs", files.Select(Path.GetFileName));
    }

    [Fact]
    public void The_scanner_would_see_a_violation_it_was_shown()
    {
        // And the other half of guarding it: the vocabularies are assembled from
        // fragments, so a typo in that assembly would leave a rule matching nothing
        // and passing forever. This feeds each vocabulary a line it must object to.
        foreach (var forbidden in _network.Concat(_wallClock).Concat(_sleeping))
        {
            Assert.Contains(forbidden, "        var x = " + forbidden + "something;", StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(forbidden));
        }
    }

    [Theory]
    [MemberData(nameof(EverySourceFile))]
    public void No_test_reaches_the_network(string fileName)
    {
        AssertNoneOf(fileName, _network, "reaches the network, and the suite has to pass on a machine with none");
    }

    [Theory]
    [MemberData(nameof(EverySourceFile))]
    public void No_test_reads_the_wall_clock(string fileName)
    {
        AssertNoneOf(fileName, _wallClock, "reads the wall clock, so it compares against a moment that will not come again");
    }

    [Theory]
    [MemberData(nameof(EverySourceFile))]
    public void No_test_waits_for_time_to_pass(string fileName)
    {
        AssertNoneOf(fileName, _sleeping, "waits for time to pass, which fails on a loaded machine and nowhere else");
    }

    [Theory]
    [MemberData(nameof(EverySourceFile))]
    public void A_test_that_creates_a_temporary_directory_removes_it(string fileName)
    {
        // The rule is per file rather than per test. A file that creates a directory
        // under the system temporary one and never deletes it leaves the machine a
        // little fuller every run, and the next contributor's suite is slower for
        // reasons nobody connects to a test.
        //
        // Two shapes put the removal on a path the test cannot leave without taking:
        // a Dispose the whole class shares, and a finally around the one test that
        // needed a directory. The second is what a single such test is written as,
        // and demanding the first refused a file that removes what it made.
        var source = Read(fileName);

        if (!source.Contains("Path" + Dot + "GetTempPath", StringComparison.Ordinal)
            || !source.Contains("Directory" + Dot + "CreateDirectory", StringComparison.Ordinal))
        {
            return;
        }

        Assert.Contains("Directory" + Dot + "Delete", source, StringComparison.Ordinal);
        Assert.True(
            source.Contains("IDisposable", StringComparison.Ordinal)
            || source.Contains("finally", StringComparison.Ordinal),
            $"{fileName} deletes the directory somewhere a thrown test would walk past");
    }

    [Theory]
    [MemberData(nameof(EveryRuleAndItsFixture))]
    public void Each_rule_refuses_its_own_fixture_and_no_other(string rule, string fixtureName)
    {
        var fixture = Fixture(fixtureName);

        Assert.True(Trips(Vocabulary(rule), fixture), $"{fixtureName} trips no token in {rule}");

        foreach (var other in new[] { nameof(_network), nameof(_wallClock), nameof(_sleeping) })
        {
            if (!string.Equals(other, rule, StringComparison.Ordinal))
            {
                Assert.False(
                    Trips(Vocabulary(other), fixture),
                    $"{fixtureName} also trips {other}, so neither rule is proven by it");
            }
        }
    }

    [Fact]
    public void The_fixture_that_leaves_a_directory_behind_is_refused()
    {
        // Its own leg rather than a row above, because the rule is not a vocabulary:
        // it asks whether a file that creates a directory also removes it, so what
        // trips it is the absence of something.
        var fixture = Fixture("leaves-a-directory-behind");

        Assert.Contains("Path" + Dot + "GetTempPath", fixture, StringComparison.Ordinal);
        Assert.Contains("Directory" + Dot + "CreateDirectory", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory" + Dot + "Delete", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("IDisposable", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("finally", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        // Without this a scan that refused every file would pass every leg above.
        var clean = Fixture("clean");

        Assert.False(Trips(_network, clean));
        Assert.False(Trips(_wallClock, clean));
        Assert.False(Trips(_sleeping, clean));
        Assert.DoesNotContain("Directory" + Dot + "CreateDirectory", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void No_fixture_is_compiled_into_the_suite_it_is_a_fixture_for()
    {
        // A fixture that acquired a .cs extension would be a permanently red scan
        // with no legal repair, so the extension is the whole of what keeps these out
        // of the build and it is checked rather than trusted.
        var fixtures = Directory.GetFiles(FixtureDirectory());

        Assert.NotEmpty(fixtures);
        Assert.DoesNotContain(fixtures, path => Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(SourceFiles(), path => path.StartsWith(FixtureDirectory(), StringComparison.Ordinal));
    }

    private static void AssertNoneOf(string fileName, IEnumerable<string> forbidden, string why)
    {
        var source = Read(fileName);

        foreach (var token in forbidden)
        {
            Assert.False(
                source.Contains(token, StringComparison.Ordinal),
                $"{fileName} names {token}, which {why}");
        }
    }

    private static string[] Vocabulary(string rule) => rule switch
    {
        nameof(_network) => _network,
        nameof(_wallClock) => _wallClock,
        nameof(_sleeping) => _sleeping,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "There is no such rule.")
    };

    private static bool Trips(IEnumerable<string> vocabulary, string source) =>
        vocabulary.Any(token => source.Contains(token, StringComparison.Ordinal));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".cs.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(ProjectDirectory(), "Fixtures", "determinism");

    private static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(ProjectDirectory(), fileName));

    private static List<string> SourceFiles() =>
        Directory.GetFiles(ProjectDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The directory this project's sources are in.
    /// </summary>
    /// <remarks>
    /// From the compiler rather than from the output directory, because sources are
    /// not copied beside the assembly and a path walked upwards from the assembly
    /// depends on the configuration and the framework it was built for. The same
    /// machine compiles and runs the suite on every route this repository has, so
    /// the path the compiler recorded is a path that exists when this reads it. On a
    /// route where that stops being true this fails loudly on a missing directory
    /// rather than quietly finding no files, which is what the first leg above is
    /// for.
    /// </remarks>
    private static string ProjectDirectory() => Path.GetDirectoryName(ThisFile())!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
