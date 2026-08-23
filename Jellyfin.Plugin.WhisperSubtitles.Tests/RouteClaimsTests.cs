using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A plugin claims things from the server it is a guest on: a scheduled task key, the
/// locations it writes to, and the API paths the server answers on its behalf. Two
/// plugins claiming one of them are each correct alone and wrong together, and the
/// second claimant is the defect. This records the third of the three for this plugin
/// and refuses a source that adds to it without the record moving.
/// </summary>
/// <remarks>
/// THE RECORD IS THAT THIS PLUGIN ANSWERS NO ROUTE, AND AN EMPTY SET IS EXACTLY THE
/// ONE THAT GROWS IN SILENCE. The other two claim sets already appear somewhere a
/// change has to touch. The task key is a constant with a literal beside it in
/// <c>SubtitleGenerationTaskTests</c>, so changing it turns a test red. The written
/// locations are listed in <c>docs/limits.md</c> and read against the sources by
/// <c>WriteLocationsTests</c>, so a write somewhere new is refused until the page
/// gains a sentence. Adding the first controller to this plugin adds no line to
/// either, and a set nothing has ever named is a set nobody notices leaving zero.
///
/// WHAT IT COMPARES. Every source of the plugin, against a vocabulary of the shapes
/// that claim a path from the server, and against the list of sources this record
/// says may carry one. That list is empty today, so any hit at all is a claim nobody
/// wrote down, and the leg that refuses it says which file and which shape.
///
/// The number of sources compared is stated rather than implied, in the guard leg's
/// own message. A scan that found none would report a plugin claiming no route
/// whatever the plugin did, in green, which is the failure the neighbouring scanners
/// in this directory each open by naming.
///
/// The vocabulary is assembled from fragments so this file holds none of the literals
/// it looks for. Otherwise the scanner would be its own first violation, and the
/// alternative, excluding this file from the scan, is a hole exactly where somebody
/// would put the thing the rule is about.
///
/// WHAT THIS DOES NOT DO, AND IT IS THE LARGER HALF OF THE ISSUE BEHIND IT. It reads
/// sources. The scan that issue asks for derives its sets from a RUNNING SERVER, so
/// that what is compared is the server's own answer rather than an assumption about
/// it, and it compares this plugin's claims against the claims of every sibling
/// installed beside it. Nothing here boots a server and nothing here sees another
/// plugin, so a collision between two installed plugins is outside what this can
/// say. This is the recording half only: it makes a new claim by THIS plugin a
/// difference in the tree rather than a surprise in a later run.
///
/// It reads tokens, so a route registered through reflection, through a helper under
/// another name, or through a server extension point this vocabulary does not know is
/// invisible to it. It reads only lines that are not comments, so a claim inside a
/// block comment opened part way along a line of code is counted as code, which
/// refuses something that is not a claim rather than admitting one that is. Both
/// bounds are the ones the write-location scanner beside it states about itself, and
/// they are part of why the running-server half is not replaced by this one.
/// </remarks>
public class RouteClaimsTests
{
    private const string Bracket = "[";

    private const string Open = "(";

    /// <summary>
    /// The shapes that claim a path from the server. Attribute routing, the base class
    /// that turns a type into a controller, and the registrations a plugin can reach
    /// through the server's own endpoint builder.
    /// </summary>
    /// <remarks>
    /// Deliberately not here: anything that only SPEAKS HTTP. The remote backend builds
    /// requests, chooses a method and posts audio to an endpoint an operator
    /// configured, and none of that claims a path on this server. A vocabulary coarse
    /// enough to catch a client is one that has to exempt the file the remote backend
    /// lives in, and the near-miss fixture beside this class is that file's shape.
    /// </remarks>
    private static readonly string[] _claims =
    [
        "Api" + "Controller",
        "Controller" + "Base",
        Bracket + "Route" + Open,
        Bracket + "Http" + "Get",
        Bracket + "Http" + "Post",
        Bracket + "Http" + "Put",
        Bracket + "Http" + "Delete",
        Bracket + "Http" + "Patch",
        "Map" + "Get" + Open,
        "Map" + "Post" + Open,
        "Map" + "Group" + Open,
        "IEndpoint" + "RouteBuilder"
    ];

    /// <summary>
    /// The sources of this plugin that claim a route. Empty, and that is the record
    /// rather than an omission: nothing in this plugin answers a path, so the server
    /// answers nothing on its behalf and there is nothing here for a sibling to
    /// collide with. The day one is added, the leg below refuses the file until it is
    /// named here, which is the moment the claim becomes a line in a diff.
    /// </summary>
    private static readonly string[] _claimants = [];

    public static TheoryData<string> EveryPluginSourceFile =>
        new(PluginSourceFiles().Select(Path.GetFileName).ToArray()!);

    [Fact]
    public void The_scanner_can_see_the_plugin_sources_it_judges()
    {
        // The number compared is stated rather than implied. A scan that compared
        // nothing would report a plugin claiming no route, in green, whatever the
        // plugin claimed.
        var files = PluginSourceFiles();

        Assert.True(files.Count > 40, $"only {files.Count} plugin source files were compared beside {ThisFile()}");
        Assert.Contains("Plugin.cs", files.Select(Path.GetFileName));
        Assert.Contains("PluginServiceRegistrator.cs", files.Select(Path.GetFileName));
    }

    [Fact]
    public void The_scanner_would_see_a_shape_it_was_shown()
    {
        // The vocabulary is assembled from fragments, so a typo in the assembly would
        // leave a token matching nothing and passing for as long as nobody looked.
        foreach (var token in _claims)
        {
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Contains(token, "        " + token + "something", StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(EveryPluginSourceFile))]
    public void Everything_that_claims_a_route_is_a_claimant_the_record_names(string fileName)
    {
        if (_claimants.Contains(fileName, StringComparer.Ordinal))
        {
            return;
        }

        var source = WithoutComments(Read(fileName));

        foreach (var token in _claims)
        {
            Assert.False(
                source.Contains(token, StringComparison.Ordinal),
                $"{fileName} claims a route from the server and the recorded claim set does not name it: it carries {token}");
        }
    }

    [Fact]
    public void Every_source_the_record_names_still_claims_a_route()
    {
        // The other direction, which is vacuous while the record is empty and is here
        // for the run after the first controller lands: a claimant renamed or emptied
        // leaves a permission standing, and the next file to take that name inherits a
        // claim nobody granted it.
        foreach (var fileName in _claimants)
        {
            Assert.Contains(fileName, PluginSourceFiles().Select(Path.GetFileName));

            var source = WithoutComments(Read(fileName));

            Assert.True(
                _claims.Any(token => source.Contains(token, StringComparison.Ordinal)),
                $"{fileName} is recorded as claiming a route and claims none, so the record stands for nothing");
        }
    }

    [Fact]
    public void The_record_says_the_set_is_empty_rather_than_leaving_it_absent()
    {
        // What the issue behind this class asks a record for is that a later change
        // adding a claim shows up as a difference. That needs the zero to be written
        // where a change has to edit it, and this is the leg that fails if a claimant
        // is added here with no file behind it.
        Assert.Empty(_claimants);
    }

    [Fact]
    public void The_scanner_refuses_a_source_that_claims_a_route_of_its_own()
    {
        // Plausible rather than contrived: a small endpoint for the configuration page
        // to ask a backend whether it is ready, which is a surface this plugin has an
        // open reason to want. It is also a path a sibling could claim first.
        var fixture = WithoutComments(Fixture("claims-a-route-of-its-own"));

        Assert.True(_claims.Any(token => fixture.Contains(token, StringComparison.Ordinal)), "the fixture trips no token");
    }

    [Fact]
    public void The_neighbour_that_speaks_http_as_a_client_is_accepted()
    {
        // The near miss, and this plugin really has one: the remote backend posts the
        // extracted audio to an endpoint an operator configured. Speaking HTTP and
        // claiming a path on this server are opposite directions, and a rule as coarse
        // as the word http would refuse the file the whole remote backend lives in.
        var fixture = WithoutComments(Fixture("speaks-http-as-a-client"));

        Assert.False(
            _claims.Any(token => fixture.Contains(token, StringComparison.Ordinal)),
            "a client of somebody else's endpoint trips a claim token");
        Assert.Contains("PostAsync", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scanner_reads_the_code_and_not_the_prose_beside_it()
    {
        // A document comment naming a claim is not a claim, and it is a shape this
        // record invites: a file explaining that this plugin deliberately answers no
        // path is worth writing, and read without this it would be the first thing
        // refused, with the repair being to delete the explanation.
        var fixture = Fixture("names-a-route-in-a-comment");

        Assert.True(_claims.Any(token => fixture.Contains(token, StringComparison.Ordinal)), "the fixture names no claim at all");
        Assert.False(
            _claims.Any(token => WithoutComments(fixture).Contains(token, StringComparison.Ordinal)),
            "the comment survived being taken out");
    }

    [Fact]
    public void No_fixture_is_a_source_anything_else_compiles()
    {
        // The extension is the whole of what keeps these away from the compiler and
        // from the scanners that walk this tree for sources. A fixture that acquired a
        // plain one would be a claim this plugin does not make, in a file that reads
        // like one it does.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".cs.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// The source with its comment lines removed.
    /// </summary>
    /// <remarks>
    /// Whole lines rather than a parse, which is the choice the write-location scanner
    /// beside this one makes and states the cost of: a block comment opened part way
    /// along a line of code leaves its contents counted as code, and that refuses
    /// something which is not a claim rather than admitting one that is.
    /// </remarks>
    private static string WithoutComments(string source) =>
        string.Join(
            '\n',
            source
                .Split('\n')
                .Select(line => line.TrimEnd('\r').TrimStart())
                .Where(line =>
                    !line.StartsWith("//", StringComparison.Ordinal)
                    && !line.StartsWith("/*", StringComparison.Ordinal)
                    && !line.StartsWith('*')));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".cs.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "route-claims");

    private static string Read(string fileName) =>
        File.ReadAllText(PluginSourceFiles().Single(path => Path.GetFileName(path) == fileName));

    /// <summary>
    /// The plugin's own sources, out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason the neighbouring
    /// scanners give: sources are not copied beside the assembly, and a path walked
    /// upwards from one depends on the configuration and the framework it was built
    /// for. The build directories are left out because what is in them is generated.
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
