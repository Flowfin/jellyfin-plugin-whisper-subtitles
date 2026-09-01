using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.OutOfProcess;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The limits page says every backend is out of process and files that as held
/// today. This reads the claim against the assembly this plugin ships rather than
/// taking the marker for a reading.
/// </summary>
/// <remarks>
/// That entry names an issue and a page and no suite, and it names no file either,
/// so both resolution legs <see cref="LimitsPageTests"/> runs over an entry iterate
/// over nothing for this one. What it is filed as is "Held today", the stronger of
/// the two states the page keeps apart, which a reader may rely on for what a
/// running server does. Nothing asked the tree.
///
/// The cost of the entry being wrong is why it is worth a check rather than a
/// review note. What it promises an operator is that a native fault inside an
/// inference library ends one transcription rather than the media server, and that
/// killing a run reclaims its memory because the memory belonged to another
/// process. Neither survives a backend that does its work in the server process,
/// and neither failure arrives as a red suite: it arrives as a media server that
/// died while somebody was watching something.
///
/// Two directions, and they are different questions rather than one asked twice.
///
/// A backend that reaches nothing outside the process is refused by the census. It
/// classifies each implementation of <see cref="ITranscriptionBackend"/> the
/// shipped assembly carries by what it has to be HANDED before it can work: a
/// process runner, which is the child process on the same machine, or a message
/// handler, which is the remote endpoint. A backend handed neither is asked whether
/// it transcribes at all, because the do-nothing backend is handed nothing either
/// and is not a breach.
///
/// A binding that loads an inference runtime into the server process is refused by
/// the source scan. That is the other way this entry stops being true and it needs
/// no new backend at all: a declaration inside an existing one is enough. The scan
/// reads the plugin's own sources rather than the built assembly, for the reason
/// <see cref="DeterminismTests"/> gives about the code it scans - what has to be
/// refused is the declaration, before anything runs anywhere.
///
/// WHAT THIS DOES NOT DO, and the first bound is the largest. The census asks what
/// a backend is handed and never what it does with it. A backend given a process
/// runner that ignores it and transcribes in this process passes, which is the
/// shape a review catches and no reading of a constructor could. The seam list is
/// the two the entry names; a third way of leaving the process, were one taken,
/// would be refused here until it was added, which is the direction worth having.
///
/// It says nothing about whether out of process is the RIGHT limit. That question
/// is open, in #8, and the entry says so itself. What this refuses is the tree and
/// the page disagreeing about which state the plugin is in today.
///
/// The scan reads the plugin project and nothing else. A test double binding native
/// code would pass, which is the right answer rather than a gap: what ships to an
/// operator is the subject, and a double is not in it.
///
/// The census carries no fixture, because its subject is a compiled assembly rather
/// than text. What proves it bites are the fabricated backends below, which live in
/// this project under the fixture namespace, are never in the population it reads,
/// and one run against the
/// real tree: a backend taking no seam was put into the plugin's own
/// <c>Backends</c> directory, the census went red naming it, and it was taken out
/// again. That run is in the pull request. The scan's proof is a fixture under
/// <c>Fixtures/out-of-process-backends/</c>, with the neighbour that breaks nothing
/// beside it.
/// </remarks>
public class OutOfProcessBackendTests
{
    /// <summary>
    /// The entry on the limits page this reads, by title.
    /// </summary>
    private const string Entry = "It does not run the transcription inside the server process";

    /// <summary>
    /// The ways an inference runtime is bound into the process that declares it,
    /// assembled from fragments so this file is not the first thing its own scan
    /// would find.
    /// </summary>
    /// <remarks>
    /// A vocabulary rather than a derivation, and it is a floor rather than a
    /// guarantee: it holds the declarations a binding actually needs, and a shape
    /// nobody has written yet walks through it. Widening it is a change here and
    /// narrowing it is a change to what this plugin is allowed to declare.
    /// </remarks>
    private static readonly string[] _nativeBindings =
    [
        "Dll" + "Import",
        "Library" + "Import",
        "Native" + "Library.",
        "Assembly" + ".LoadFrom",
        "Assembly" + ".LoadFile",
        "Assembly" + ".UnsafeLoadFrom",
    ];

    /// <summary>
    /// What a backend has to be handed before it can transcribe.
    /// </summary>
    private enum Reach
    {
        /// <summary>A child process on the same machine.</summary>
        ChildProcess,

        /// <summary>A remote endpoint.</summary>
        RemoteEndpoint,

        /// <summary>Nothing, and it transcribes nothing.</summary>
        TranscribesNothing,

        /// <summary>Nothing, and it transcribes anyway.</summary>
        InsideTheServerProcess,
    }

    public static TheoryData<string> EveryBackendThisPluginShips =>
        new(BackendsInTheShippedAssembly().Select(backend => backend.FullName!).ToArray());

    [Theory]
    [MemberData(nameof(EveryBackendThisPluginShips))]
    public async Task Every_backend_this_plugin_ships_leaves_the_process_or_transcribes_nothing(string name)
    {
        var backend = BackendsInTheShippedAssembly()
            .Single(type => type.FullName!.Equals(name, StringComparison.Ordinal));

        var reach = await ReachOfAsync(backend);

        Assert.True(
            reach != Reach.InsideTheServerProcess,
            $"{name} is handed neither a process runner nor a message handler and transcribes anyway, so its work happens in the server process. docs/limits.md files \"{Entry}\" as held today, and what that entry promises an operator is that a native fault ends one transcription rather than the media server.");
    }

    /// <summary>
    /// Guards the census rather than the backends, for the reason the readers in
    /// <see cref="LimitsPageTests"/> carry their own guard: a population that
    /// quietly emptied would make the leg above pass by comparing nothing, and a
    /// population that lost one of the two shapes would make it pass over a plugin
    /// that had stopped doing what the entry describes.
    /// </summary>
    [Fact]
    public async Task Both_shapes_the_entry_names_are_shipped_rather_than_only_one()
    {
        var reaches = new List<Reach>();

        foreach (var backend in BackendsInTheShippedAssembly())
        {
            reaches.Add(await ReachOfAsync(backend));
        }

        Assert.Contains(Reach.ChildProcess, reaches);
        Assert.Contains(Reach.RemoteEndpoint, reaches);
    }

    [Fact]
    public async Task The_census_refuses_a_backend_that_is_handed_nothing_and_transcribes()
    {
        Assert.Equal(Reach.InsideTheServerProcess, await ReachOfAsync(typeof(BackendThatWorksInThisProcess)));
    }

    /// <summary>
    /// The neighbour the leg above needs, or a census refusing every backend handed
    /// nothing would pass it while refusing the do-nothing backend this plugin
    /// ships.
    /// </summary>
    [Fact]
    public async Task The_census_accepts_a_backend_that_is_handed_nothing_and_transcribes_nothing()
    {
        Assert.Equal(Reach.TranscribesNothing, await ReachOfAsync(typeof(BackendThatTranscribesNothing)));
    }

    [Fact]
    public async Task The_census_reads_the_seam_a_backend_is_handed_rather_than_its_name()
    {
        Assert.Equal(Reach.ChildProcess, await ReachOfAsync(typeof(BackendHandedAProcessRunner)));
        Assert.Equal(Reach.RemoteEndpoint, await ReachOfAsync(typeof(BackendHandedAMessageHandler)));
    }

    [Fact]
    public void Nothing_in_this_plugin_binds_an_inference_runtime_into_the_server_process()
    {
        var declared = PluginSources()
            .SelectMany(path => Bindings(File.ReadAllText(path))
                .Select(binding => $"{Path.GetFileName(path)} declares {binding}"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            declared.Count == 0,
            $"docs/limits.md files \"{Entry}\" as held today, and these bind code into the process that declares them: {string.Join("; ", declared)}");
    }

    /// <summary>
    /// Guards the scan's reach rather than the sources, so a scan reading an empty
    /// set cannot pass the leg above.
    /// </summary>
    [Fact]
    public void The_scan_reads_the_plugin_project_rather_than_an_empty_set()
    {
        Assert.Contains(
            PluginSources().Select(Path.GetFileName),
            name => string.Equals(name, "LocalWhisperBackend.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scan_finds_a_binding_in_a_source_that_carries_one()
    {
        Assert.NotEmpty(Bindings(Fixture("binds-an-inference-runtime")));
    }

    /// <summary>
    /// The neighbour that has to stay accepted, or the leg above passes for a scan
    /// that refuses every source in the project.
    /// </summary>
    [Fact]
    public void The_scan_accepts_a_source_that_reaches_a_child_process_instead()
    {
        Assert.Empty(Bindings(Fixture("reaches-a-child-process")));
    }

    /// <summary>
    /// The page and this suite, so neither drifts away from the other in silence.
    /// </summary>
    /// <remarks>
    /// It is the half <see cref="LimitsPageTests"/> could not do for this entry.
    /// That suite asks whether every suite an entry NAMES is one this assembly runs,
    /// and this entry named none, so it passed while nothing read the entry. A name
    /// in the entry turns that leg into a comparison, and this one refuses the entry
    /// losing the name, or losing its state while the name is still there.
    /// </remarks>
    [Fact]
    public void The_entry_this_reads_is_filed_as_held_and_names_this_suite()
    {
        var entry = EntryOnThePage();

        Assert.Contains("Held today", entry, StringComparison.Ordinal);
        Assert.Contains(nameof(OutOfProcessBackendTests), entry, StringComparison.Ordinal);
    }

    /// <summary>
    /// Guards the reader above rather than the page, for the reason its neighbours
    /// in <see cref="LimitsPageTests"/> carry one: a reader that stopped finding the
    /// entry would make the leg above pass by reading an empty string.
    /// </summary>
    [Fact]
    public void The_entry_reader_returns_one_entry_and_stops_at_the_next_heading()
    {
        var entry = EntryOnThePage();

        Assert.NotEmpty(entry);
        Assert.DoesNotContain("\n## ", entry, StringComparison.Ordinal);
    }

    /// <summary>
    /// The entry, read out of the checkout and normalised so a clone that checked
    /// the page out with either line ending reads the same one.
    /// </summary>
    private static string EntryOnThePage()
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "limits.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        var heading = "## " + Entry + "\n";
        var start = page.IndexOf(heading, StringComparison.Ordinal);

        if (start < 0)
        {
            return string.Empty;
        }

        var body = page[(start + heading.Length)..];
        var next = body.IndexOf("\n## ", StringComparison.Ordinal);

        return next < 0 ? body : body[..next];
    }

    private static List<string> Bindings(string source) =>
        _nativeBindings.Where(binding => source.Contains(binding, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// The plugin's own sources, without the directories a build writes into.
    /// </summary>
    private static List<string> PluginSources() =>
        Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !Written(path, "obj") && !Written(path, "bin"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static bool Written(string path, string directory) =>
        path.Contains(
            string.Concat(Path.DirectorySeparatorChar, directory, Path.DirectorySeparatorChar),
            StringComparison.Ordinal);

    /// <summary>
    /// The backends the assembly an operator installs carries.
    /// </summary>
    /// <remarks>
    /// From the shipped assembly and never from this one. The stand-ins this suite
    /// carries for the same interface are deliberately outside the population: a
    /// test double is not a backend this plugin ships, and a census counting one
    /// would refuse the doubles that prove the census.
    /// </remarks>
    private static List<Type> BackendsInTheShippedAssembly() =>
        typeof(ITranscriptionBackend).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ITranscriptionBackend).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// What a backend has to be handed, read off its constructors, and what it does
    /// when it is handed nothing.
    /// </summary>
    private static async Task<Reach> ReachOfAsync(Type backend)
    {
        var handed = backend.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        if (handed.Exists(type => type == typeof(IProcessRunner)))
        {
            return Reach.ChildProcess;
        }

        if (handed.Exists(typeof(HttpMessageHandler).IsAssignableFrom))
        {
            return Reach.RemoteEndpoint;
        }

        return await TranscribesAnythingAsync(backend).ConfigureAwait(false)
            ? Reach.InsideTheServerProcess
            : Reach.TranscribesNothing;
    }

    /// <summary>
    /// Asks a backend that was handed nothing whether it transcribes anyway.
    /// </summary>
    /// <remarks>
    /// The do-nothing backend refuses rather than returning an empty result, which
    /// is its own rule and is what makes this question answerable at all: a backend
    /// answering with segments while holding no seam produced them here.
    /// </remarks>
    private static async Task<bool> TranscribesAnythingAsync(Type backend)
    {
        var instance = (ITranscriptionBackend)Activator.CreateInstance(backend)!;

        try
        {
            await instance.TranscribeAsync(
                    new TranscriptionRequest("audio.wav", null),
                    new Progress<double>(),
                    CancellationToken.None)
                .ConfigureAwait(false);

            return true;
        }
        catch (BackendNotConfiguredException)
        {
            return false;
        }
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(ThisFile())!,
            "Fixtures",
            "out-of-process-backends",
            name + ".cs.fixture"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
