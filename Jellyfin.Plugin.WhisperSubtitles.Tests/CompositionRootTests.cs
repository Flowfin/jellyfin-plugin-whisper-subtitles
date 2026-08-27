using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A real implementation behind a seam is named for the container in
/// <see cref="PluginServiceRegistrator"/> and constructed in no plugin source at
/// all.
/// </summary>
/// <remarks>
/// The seam half of #71 is held elsewhere and this is the other half of its third
/// clause. <c>SeamDoubleTests</c> asks whether every seam has a stand-in the suite
/// can hand in; that says nothing about whether anything bypasses the seam by
/// building the real thing itself, and reflection cannot answer it either, because
/// it reads signatures and never method bodies.
///
/// The subject is therefore the SOURCE, for the reason the neighbouring scanners
/// give: a type that builds its own real implementation passes every test written
/// against it, because the seam it went around is still there and still works. The
/// cost lands on the server, where the thing built is not the thing the container
/// would have handed out and nothing says the two differ.
///
/// The population is DERIVED rather than listed. It is read out of a collection
/// this plugin's own registrator has just filled, so a seam registered tomorrow is
/// asked about without anybody remembering to add a line here, and a registration
/// removed stops being asked about in the same change that removes it.
///
/// The permitted set is EMPTY, and a leg below asserts that it is. The registrator
/// hands the container a type NAME and lets it do the construction, so the
/// composition root does not build one either and there is nothing to permit. That
/// is the tightest rule of this family available, and widening it is a change to
/// this file's own claim about itself rather than a line added to a list.
///
/// It went red on the tree it was written against. <c>TemporaryAudioSweep</c> held
/// the real removal as a static and its one-argument overload closed over it, and
/// <see cref="PluginServiceRegistrator"/>'s own remarks named that as the one place
/// this rule did not hold. The overload is gone, the removal is registered, and
/// this is what refuses the next one.
///
/// WHAT IS NOT ASSERTED. The scan reads tokens, so an implementation reached
/// through reflection, through a factory under another name, or through a helper
/// that returns one walks past it. Its population is what the registrator registers
/// by TYPE: the two option records and the candidate list arrive through factory
/// lambdas, so nothing here judges those, and a backend constructed inside
/// <c>BackendCandidates</c> is outside this subject by the same rule. And a static
/// method group used as a default - <c>SubtitleOutput</c> takes
/// <c>AtomicSubtitleFile.WriteAsync</c> that way - is not a construction and is not
/// refused here.
/// </remarks>
public class CompositionRootTests
{
    /// <summary>
    /// The plugin files allowed to build one. None is, and the emptiness is the
    /// rule rather than a state it happens to be in.
    /// </summary>
    private static readonly string[] _permitted = [];

    public static TheoryData<string, string> EverySourceAndEveryImplementation
    {
        get
        {
            var rows = new TheoryData<string, string>();

            foreach (var file in PluginSourceFiles().Select(Path.GetFileName))
            {
                foreach (var implementation in RealImplementations())
                {
                    rows.Add(file!, implementation);
                }
            }

            return rows;
        }
    }

    [Fact]
    public void The_registrator_names_implementations_for_the_container_to_build()
    {
        // Guards every leg below. A derivation that returned nothing would report a
        // plugin that builds no implementation whatever the plugin did, and it would
        // do it in green.
        var implementations = RealImplementations();

        Assert.True(implementations.Count > 2, $"the registrator named {implementations.Count} implementation types");
        Assert.Contains("SystemProcessRunner", implementations);
        Assert.Contains("SystemFileRemoval", implementations);
    }

    [Fact]
    public void Every_named_implementation_is_a_type_this_plugin_owns()
    {
        // A registration under a framework implementation would put a name outside
        // this plugin into the scan, and a rule refusing a source for naming
        // somebody else's type is a rule that has stopped being about this plugin.
        var mine = typeof(Plugin).Assembly.GetTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(RealImplementations(), name => Assert.Contains(name, mine));
    }

    [Fact]
    public void The_scanner_can_see_the_plugin_sources_it_judges()
    {
        var files = PluginSourceFiles();

        Assert.True(files.Count > 40, $"only {files.Count} plugin source files were found beside {ThisFile()}");
        Assert.Contains("PluginServiceRegistrator.cs", files.Select(Path.GetFileName));
        Assert.Contains("TemporaryAudioSweep.cs", files.Select(Path.GetFileName));
    }

    [Fact]
    public void No_plugin_file_is_permitted_to_build_one()
    {
        // The rule stated as an assertion rather than as an empty array somebody
        // could fill in passing. A file added there has to be argued for in the
        // change that adds it, because this leg is what it breaks first.
        Assert.Empty(_permitted);
    }

    [Theory]
    [MemberData(nameof(EverySourceAndEveryImplementation))]
    public void Nothing_in_the_plugin_builds_a_real_implementation(string fileName, string implementation)
    {
        if (_permitted.Contains(fileName, StringComparer.Ordinal))
        {
            return;
        }

        Assert.False(
            Read(fileName).Contains(Construction(implementation), StringComparison.Ordinal),
            $"{fileName} builds {implementation}, and the container is what hands out the one the registrator named");
    }

    [Fact]
    public void The_fixture_that_builds_its_own_implementation_is_refused()
    {
        Assert.True(
            Trips(Fixture("builds-a-real-implementation")),
            "the fixture builds none of the implementations the registrator names");
    }

    [Fact]
    public void The_neighbour_that_takes_the_implementation_it_was_given_is_accepted()
    {
        // The near miss rather than a distant one. This fixture differs from the one
        // above in where the removal comes from and in nothing else, so a rule coarse
        // enough to refuse any source naming an implementation would pass its own
        // fixture and fail here.
        var neighbour = Fixture("takes-the-implementation-it-was-given");

        Assert.False(Trips(neighbour));
        Assert.Contains("SystemFileRemoval", neighbour, StringComparison.Ordinal);
    }

    [Fact]
    public void No_fixture_is_compiled_into_the_suite_or_counted_as_a_plugin_source()
    {
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
        RealImplementations().Any(name => source.Contains(Construction(name), StringComparison.Ordinal));

    /// <summary>
    /// The token a construction of one is written as.
    /// </summary>
    /// <remarks>
    /// Assembled rather than written out, so this file holds none of the strings it
    /// looks for. A scanner excluded from its own scan is a hole exactly where
    /// somebody would put the thing it forbids.
    /// </remarks>
    private static string Construction(string implementation) => "new" + " " + implementation + "(";

    /// <summary>
    /// The implementation types the registrator names for the container.
    /// </summary>
    /// <remarks>
    /// A descriptor carrying a factory rather than a type is left out, because there
    /// is no name for a source to be refused for and the construction happens inside
    /// the lambda the registrator owns.
    /// </remarks>
    private static List<string> RealImplementations()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        return services
            .Where(descriptor => descriptor.ImplementationType is not null)
            .Select(descriptor => descriptor.ImplementationType!.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".cs.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "composition-root");

    private static string Read(string fileName) =>
        File.ReadAllText(PluginSourceFiles().Single(path => Path.GetFileName(path) == fileName));

    /// <summary>
    /// The plugin's own sources, out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason the
    /// neighbouring scanners give: sources are not copied beside the assembly, and a
    /// path walked upwards from one depends on the configuration and the framework it
    /// was built for.
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
