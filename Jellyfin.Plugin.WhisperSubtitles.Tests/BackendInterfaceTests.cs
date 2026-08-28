using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The heavy runtime sits behind one narrow interface, and the value of that is
/// lost the moment something outside the backend folder knows which backend it is
/// holding.
/// </summary>
public class BackendInterfaceTests
{
    private const string BackendNamespace = "Jellyfin.Plugin.WhisperSubtitles.Backends";

    private static Assembly PluginAssembly => typeof(ITranscriptionBackend).Assembly;

    [Fact]
    public void No_type_outside_the_backend_folder_names_a_concrete_backend()
    {
        var violations = BackendIsolation.Violations(PluginAssembly, BackendNamespace);

        Assert.Empty(violations);
    }

    [Fact]
    public void The_isolation_check_finds_a_type_that_names_a_concrete_backend()
    {
        // Without this the assertion above is a green run over an empty set: the
        // plugin ships no concrete backend yet, so nothing could have been found
        // whether the check worked or not. The fixtures put one violation and one
        // near-miss in the test assembly and require the check to tell them apart.
        var violations = BackendIsolation.Violations(
            typeof(BackendInterfaceTests).Assembly,
            "Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.Backends");

        Assert.Contains(violations, v => v.Contains("HoldsAConcreteBackend", System.StringComparison.Ordinal));

        Assert.DoesNotContain(violations, v => v.Contains("HoldsTheInterface", System.StringComparison.Ordinal));
    }

    [Fact]
    public void The_interface_and_its_result_types_live_in_the_backend_folder()
    {
        var expected = new[]
        {
            typeof(ITranscriptionBackend),
            typeof(TranscriptionRequest),
            typeof(TranscriptionResult),
            typeof(TimedSegment),
            typeof(CostEstimate),
            typeof(BackendReadiness),
            typeof(BackendDescription)
        };

        Assert.All(expected, t => Assert.Equal(BackendNamespace, t.Namespace));
    }

    [Fact]
    public void The_interface_returns_segments_rather_than_a_formatted_file()
    {
        // Formatting, naming and marking belong to this plugin and must not differ
        // between backends, which is only true while no backend can return a file.
        var transcribe = typeof(ITranscriptionBackend).GetMethod(nameof(ITranscriptionBackend.TranscribeAsync));

        Assert.NotNull(transcribe);

        Assert.Equal(typeof(System.Threading.Tasks.Task<TranscriptionResult>), transcribe!.ReturnType);

        Assert.Equal(
            typeof(System.Collections.Generic.IReadOnlyList<TimedSegment>),
            typeof(TranscriptionResult).GetProperty(nameof(TranscriptionResult.Segments))!.PropertyType);
    }

    [Fact]
    public void The_request_cannot_ask_for_a_language_other_than_the_spoken_one()
    {
        // A transcription needs one language and a translation needs two, so this is
        // the limit written as something the interface cannot say rather than as
        // something documented as unsupported. The remark on the field states it;
        // this is what refuses the second field arriving under it.
        Assert.Equal(
            new[] { nameof(TranscriptionRequest.Language) },
            TranslationSurface.LanguagesNamedBy(typeof(TranscriptionRequest)));
    }

    [Fact]
    public void Nothing_beside_the_request_carries_a_language_into_a_transcription()
    {
        // The other way a second language could arrive, and the one the leg above
        // cannot see: a parameter on the call, with the request type unmoved.
        var transcribe = typeof(ITranscriptionBackend).GetMethod(nameof(ITranscriptionBackend.TranscribeAsync));

        Assert.NotNull(transcribe);

        Assert.Empty(TranslationSurface.LanguagesNamedBy(transcribe!));
    }

    [Fact]
    public void The_translation_reader_finds_a_second_language_and_leaves_the_first_alone()
    {
        // Without this the two assertions above are green runs over a set nothing
        // proved the reader can fill. The fixtures put the mistake and its one-change
        // neighbour in the test assembly, in both the shapes the legs above judge.
        Assert.Equal(
            new[] { "SpokenLanguage", "TargetLanguage" },
            TranslationSurface.LanguagesNamedBy(typeof(Fixtures.Translation.TranslatingRequest)));

        Assert.Equal(
            new[] { "Language" },
            TranslationSurface.LanguagesNamedBy(typeof(Fixtures.Translation.TranscribingRequest)));

        Assert.Equal(
            new[] { "targetLanguage" },
            TranslationSurface.LanguagesNamedBy(CallTaking(
                typeof(Fixtures.Translation.TranscribingRequest),
                typeof(string))));

        Assert.Empty(TranslationSurface.LanguagesNamedBy(CallTaking(
            typeof(Fixtures.Translation.TranscribingRequest))));
    }

    [Fact]
    public void Every_public_member_of_the_backend_folder_carries_a_documentation_comment()
    {
        // The build writes the XML file only for members that have one, so a member
        // missing from it is a member nobody documented.
        var xml = System.IO.Path.Combine(
            System.AppContext.BaseDirectory,
            "Jellyfin.Plugin.WhisperSubtitles.xml");

        Assert.True(System.IO.File.Exists(xml), $"no documentation file next to the test assembly, looked in {System.AppContext.BaseDirectory}");

        var documented = System.IO.File.ReadAllText(xml);

        var publicTypes = PluginAssembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == BackendNamespace)
            .ToList();

        Assert.NotEmpty(publicTypes);

        Assert.All(publicTypes, t => Assert.Contains($"\"T:{t.FullName}\"", documented, System.StringComparison.Ordinal));
    }

    private static System.Reflection.MethodInfo CallTaking(params System.Type[] parameters) =>
        typeof(Fixtures.Translation.TranslatingCall).GetMethod(
            nameof(Fixtures.Translation.TranslatingCall.TranscribeAsync),
            parameters)!;
}
