using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jellyfin.Plugin.WhisperSubtitles;
using Jellyfin.Plugin.WhisperSubtitles.Library;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The adapter between this plugin and the server's library, judged from the two
/// sides a suite with no server can reach: what the seam lets a run be driven
/// with, and whether the server could build the real thing behind it.
/// </summary>
/// <remarks>
/// WHAT NO TEST HERE EXECUTES is the translation itself. Turning a
/// <c>BaseItem</c> into an <see cref="ItemDescription"/> is the substance of
/// <see cref="ServerLibrarySource"/>, and the item answers its media streams and
/// its metadata path through static services on <c>BaseItem</c> that a test would
/// have to set for the whole process. Every test in this project runs offline and
/// in parallel with its neighbours, so a suite that set them would be one test
/// deciding what another one sees. That refusal is written into
/// <c>CONTRIBUTING.md</c> beside the others, with the issue that owes the coverage,
/// and this paragraph is not a substitute for reading it.
///
/// What is left is worth having and is the half that goes wrong silently. A seam
/// nothing can be substituted behind is a seam in name only, and a real
/// implementation the container cannot build is a plugin the server marks as
/// failed at load with no test having said so.
/// </remarks>
public class LibrarySourceTests
{
    private static readonly Guid _library = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Selection_runs_over_the_descriptions_the_double_gave_it()
    {
        // The point of the seam, stated as a run: a library nobody has, driving the
        // decision that determines what a run costs.
        var wanted = Item("A Film", TimeSpan.FromMinutes(90), subtitles: []);
        var already = Item("A Film With Subtitles", TimeSpan.FromMinutes(90), subtitles: ["en"]);
        var source = new StubLibrarySource([wanted, already]);

        var chosen = ItemSelection.Select(source.ItemsUnderConsideration(), Options());

        Assert.Equal(1, source.TimesTheItemsWereRead);
        Assert.Equal([wanted.Id], chosen.Candidates.Select(item => item.Id));
        Assert.Equal(TimeSpan.FromMinutes(90), chosen.TotalDuration);
    }

    [Fact]
    public void The_double_answers_where_an_item_is_and_says_so_when_it_has_no_answer()
    {
        // The second question the seam is for. A run that selected an item and then
        // found it gone is an ordinary thing on a library somebody is editing, and
        // the seam has to have a way of saying it rather than throwing.
        var here = Item("A Film", TimeSpan.FromMinutes(90), subtitles: []);
        var gone = Item("Another Film", TimeSpan.FromMinutes(90), subtitles: []);
        var location = new ItemLocation("/media/a-film.mkv", "/data/metadata/a-film", saveSubtitlesWithMedia: true);

        var source = new StubLibrarySource(
            [here, gone],
            new Dictionary<Guid, ItemLocation> { [here.Id] = location });

        Assert.Same(location, source.LocationOf(here.Id));
        Assert.Null(source.LocationOf(gone.Id));
        Assert.Equal([here.Id, gone.Id], source.Located);
    }

    [Fact]
    public void The_server_can_build_the_adapter_from_what_the_registrator_registers()
    {
        // ActivatorUtilities is the call the server's container makes. The library
        // manager is the server's own registration rather than this plugin's, so it
        // is added here the way the server would have it: present before anything
        // resolves the seam.
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);
        services.AddSingleton(NoLibraryManager());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<ServerLibrarySource>(provider.GetRequiredService<ILibrarySource>());
    }

    [Fact]
    public void Without_the_server_s_library_manager_the_same_construction_fails()
    {
        // The near miss, and the state a server would be in if this plugin declared
        // a dependency the server does not hand out. One line removed from the test
        // above, and the container refuses rather than handing back something half
        // built.
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ILibrarySource>());
    }

    [Fact]
    public void The_registration_names_a_type_and_builds_nothing()
    {
        // The composition root's own rule, asserted for this registration in
        // particular. A descriptor carrying an instance is one constructed during
        // registration, and registration happens before the container exists.
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        var descriptor = services.Single(entry => entry.ServiceType == typeof(ILibrarySource));

        Assert.Equal(typeof(ServerLibrarySource), descriptor.ImplementationType);
        Assert.Null(descriptor.ImplementationInstance);
        Assert.Null(descriptor.ImplementationFactory);
    }

    [Fact]
    public void Nothing_else_in_the_plugin_names_a_server_library_type()
    {
        // The claim the seam makes about the rest of the plugin, and the one that
        // decays quietly: a second file reaching for the library manager works, and
        // takes the run's input out of the values every other part is written
        // against.
        var offenders = PluginSourceFiles()
            .Where(path => !Path.GetFileName(path).Equals("ServerLibrarySource.cs", StringComparison.Ordinal))
            .Select(path => (Name: Path.GetFileName(path), Text: File.ReadAllText(path)))
            .Select(file => (file.Name, Named: ServerLibraryTypes().Where(type => Mentions(file.Text, type)).ToList()))
            .Where(file => file.Named.Count > 0)
            .Select(file => $"{file.Name} names {string.Join(", ", file.Named)}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"the adapter is meant to be the only plugin source naming a server library type: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void The_scan_reads_the_adapter_it_excludes()
    {
        // Without this the leg above passes for a scanner that reads nothing, and it
        // would pass hardest on the day the adapter was renamed.
        var adapter = PluginSourceFiles()
            .Single(path => Path.GetFileName(path).Equals("ServerLibrarySource.cs", StringComparison.Ordinal));

        var text = File.ReadAllText(adapter);

        Assert.All(
            ServerLibraryTypes(),
            type => Assert.True(Mentions(text, type), $"the adapter no longer names {type}, so the scan excludes it for nothing"));
    }

    /// <summary>
    /// The server types the run's input is meant to be free of.
    /// </summary>
    /// <remarks>
    /// Assembled from the names rather than written out as tokens, so this file is
    /// not itself a plugin source that would trip its own scan if it ever moved.
    /// </remarks>
    /// <returns>Each type name.</returns>
    private static string[] ServerLibraryTypes() =>
        [
            nameof(ILibraryManager),
            "BaseItem",
            "InternalItemsQuery",
        ];

    private static bool Mentions(string source, string type) =>
        source.Contains(type, StringComparison.Ordinal);

    /// <summary>
    /// A library manager that answers nothing.
    /// </summary>
    /// <remarks>
    /// Generated rather than written. The interface carries over a hundred members
    /// and they are not the same set on the two supported lines, so a hand-written
    /// stub would be a file that stops compiling when the server line moves, for a
    /// test that never calls one of them. What is under test here is that the
    /// container can satisfy the adapter's constructor.
    /// </remarks>
    /// <returns>The stand-in.</returns>
    private static ILibraryManager NoLibraryManager() =>
        DispatchProxy.Create<ILibraryManager, AnswersNothing>();

    private static List<string> PluginSourceFiles() =>
        Directory
            .EnumerateFiles(PluginDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine(PluginDirectory(), "bin"), StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.Combine(PluginDirectory(), "obj"), StringComparison.Ordinal))
            .ToList();

    private static string PluginDirectory() =>
        Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static ItemDescription Item(string name, TimeSpan duration, IReadOnlyList<string> subtitles) =>
        new(
            Guid.NewGuid(),
            name,
            _library,
            "Movie",
            duration,
            hasAudioStream: true,
            subtitles,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

    private static SelectionOptions Options() =>
        new(
            [_library],
            ["Movie"],
            "en",
            maximumItemDuration: null,
            addedSince: null);

    /// <summary>
    /// The proxy behind the generated library manager.
    /// </summary>
    /// <remarks>
    /// Unsealed and parameterless because <see cref="DispatchProxy"/> derives from
    /// it at run time.
    /// It throws rather than returning a default, so a test that started calling
    /// the server through it fails loudly instead of asserting against a silence
    /// this class invented.
    /// </remarks>
    // CA1852, seal a type nothing derives from. DispatchProxy derives from this
    // one at run time and refuses a sealed base with "The base type ... cannot be
    // sealed", which is what sealing it produced here before this line existed.
#pragma warning disable CA1852
    internal class AnswersNothing : DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException(
                $"nothing in this suite calls the server's library, and {targetMethod?.Name} was called");
    }
#pragma warning restore CA1852
}
