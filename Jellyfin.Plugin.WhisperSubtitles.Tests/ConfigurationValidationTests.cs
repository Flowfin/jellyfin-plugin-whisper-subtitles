using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The configuration is XML a person can edit and it is read back on every server
/// start, so every value in it is hostile input with a friendly origin. These are
/// the files somebody actually produces: a typo in a language code, a backend name
/// from another plugin's documentation, an editor that saved half the file, and a
/// path that turned out to hold something else entirely.
/// </summary>
public class ConfigurationValidationTests
{
    private const string LocalName = LocalWhisperBackend.BackendName;

    private static readonly Guid _recordings = Guid.Parse("2b4a3f10-6c1e-4f2a-9a55-0f1b2c3d4e5f");
    private static readonly Guid _films = Guid.Parse("8d7c6b5a-4e3f-2a1b-9c8d-7e6f5a4b3c2d");

    [Fact]
    public void A_valid_file_is_read_with_nothing_to_complain_about()
    {
        var load = ConfigurationFile.Read(File(
            schemaVersion: "1",
            backend: LocalName,
            targetLanguage: "eng",
            rows: Row(_recordings, "deu")));

        Assert.Empty(load.Complaints);
        Assert.Equal(1, load.InForce.SchemaVersion);
        Assert.Equal(LocalName, load.InForce.Backend);
        Assert.Equal("eng", load.InForce.TargetLanguage);
        Assert.Equal("deu", load.InForce.TargetLanguagesByLibrary[_recordings]);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.SchemaVersion), "7", LocalName, "eng", "")]
    [InlineData(nameof(PluginConfiguration.Backend), "1", "whisperx", "eng", "")]
    [InlineData(nameof(PluginConfiguration.TargetLanguage), "1", LocalName, "klingon", "")]
    [InlineData(nameof(PluginConfiguration.LibraryTargets), "1", LocalName, "eng", "not a library")]
    public void Each_field_invalid_in_turn_is_complained_about_by_name(
        string field,
        string schemaVersion,
        string backend,
        string targetLanguage,
        string libraryId)
    {
        // One file per field, each one wrong in exactly one place, so a rule that
        // fires on the wrong field or a rule that fires on everything is visible.
        var load = ConfigurationFile.Read(File(
            schemaVersion,
            backend,
            targetLanguage,
            libraryId.Length == 0 ? Row(_recordings, "deu") : Row(libraryId, "deu")));

        var complaint = Assert.Single(load.Complaints);
        Assert.StartsWith(field, complaint.Field, StringComparison.Ordinal);
        Assert.NotEmpty(complaint.Problem);
        Assert.NotEmpty(complaint.InForce);
    }

    [Fact]
    public void A_field_that_fails_its_rule_falls_back_to_the_documented_default()
    {
        // The complaint is half the promise. The other half is that the run uses the
        // default rather than the value that was refused, and for the language that
        // means selecting nothing at all.
        var load = ConfigurationFile.Read(File(
            schemaVersion: "0",
            backend: LocalName,
            targetLanguage: "klingon",
            rows: Row("not a library", "deu")));

        Assert.Equal(ConfigurationValidation.CurrentSchemaVersion, load.InForce.SchemaVersion);
        Assert.Equal(ConfigurationValidation.NoTargetLanguage, load.InForce.TargetLanguage);
        Assert.True(LanguageTarget.IsAbsent(load.InForce.TargetLanguage));
        Assert.Empty(load.InForce.TargetLanguagesByLibrary);
        Assert.Equal(3, load.Complaints.Count);
    }

    [Fact]
    public void A_truncated_file_is_the_defaults_and_a_complaint_rather_than_a_throw()
    {
        // What an editor killed mid-save leaves behind, and what a full disk
        // produces. The server would replace it; this is asked directly because a
        // plugin that threw on it is one an operator cannot repair from the page.
        var whole = File("1", LocalName, "eng", Row(_recordings, "deu"));
        var load = ConfigurationFile.Read(whole[..(whole.Length / 2)]);

        var complaint = Assert.Single(load.Complaints);
        Assert.Equal("configuration", complaint.Field);
        AssertEverythingIsAtItsDefault(load);
    }

    [Fact]
    public void A_file_that_is_not_xml_at_all_is_the_defaults_and_a_complaint()
    {
        var load = ConfigurationFile.Read("{ \"Backend\": \"Local\" }");

        var complaint = Assert.Single(load.Complaints);
        Assert.Equal("configuration", complaint.Field);
        AssertEverythingIsAtItsDefault(load);
    }

    [Fact]
    public void An_empty_file_is_the_defaults_and_a_complaint()
    {
        var load = ConfigurationFile.Read("   ");

        Assert.Single(load.Complaints);
        AssertEverythingIsAtItsDefault(load);
    }

    [Fact]
    public async Task An_invalid_backend_setting_yields_the_do_nothing_backend()
    {
        // The exception to falling back to a default: a backend that fails
        // validation must not become a backend that runs. Read end to end, from the
        // bytes on disk to the object that would transcribe, because the two halves
        // pass separately and the join is where a working backend could appear.
        var load = ConfigurationFile.Read(File("1", "whisperx", "eng", Row(_recordings, "deu")));

        Assert.Contains(
            load.Complaints,
            c => string.Equals(c.Field, nameof(PluginConfiguration.Backend), StringComparison.Ordinal));

        var choice = await BackendSelector.SelectAsync(
            load.InForce.Backend,
            [new BackendCandidate(LocalName, new StubBackend(LocalName), Array.Empty<string>())],
            CancellationToken.None).ConfigureAwait(true);

        Assert.IsType<NotConfiguredBackend>(choice.Backend);
        Assert.Equal(BackendSelectionOutcome.UnknownName, choice.Outcome);

        // Named rather than merely refused. A refusal that does not repeat the name
        // leaves an operator comparing a page against their own memory of what they
        // typed.
        Assert.Contains("whisperx", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_setting_the_configuration_declares_has_a_rule_that_decides_it()
    {
        // The condition this issue is written around is "every field", and every
        // field is a moving set. A setting added with the feature that needed it,
        // in a branch about that feature, is the one that arrives unvalidated, and
        // it arrives holding whatever the file says for as long as nobody notices.
        var declared = typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var validated = ConfigurationValidation.ValidatedFields.ToHashSet(StringComparer.Ordinal);

        Assert.Empty(declared.Except(validated));

        // And the other direction, so a rule naming a setting that has since been
        // removed is a failure rather than dead text.
        Assert.Empty(validated.Except(declared));
    }

    [Fact]
    public void A_row_whose_library_will_not_parse_is_dropped_and_the_rest_are_kept()
    {
        var load = ConfigurationFile.Read(File(
            "1",
            LocalName,
            "eng",
            Row("not a library", "deu") + Row(_films, "fra")));

        Assert.Equal(new[] { _films }, load.InForce.TargetLanguagesByLibrary.Keys.ToArray());
        Assert.Single(load.Complaints);
    }

    [Fact]
    public void A_row_asking_for_a_language_the_server_cannot_label_leaves_that_library_on_the_default()
    {
        var load = ConfigurationFile.Read(File(
            "1",
            LocalName,
            "eng",
            Row(_recordings, "klingon")));

        Assert.Empty(load.InForce.TargetLanguagesByLibrary);
        Assert.Single(load.Complaints);
    }

    [Fact]
    public void A_second_row_for_one_library_applies_and_says_so()
    {
        // The last row wins, which is the reading the code already had. What is new
        // is that the file being ambiguous is something the operator hears about
        // instead of a silent choice between two lines they wrote.
        var load = ConfigurationFile.Read(File(
            "1",
            LocalName,
            "eng",
            Row(_recordings, "deu") + Row(_recordings, "fra")));

        Assert.Equal("fra", load.InForce.TargetLanguagesByLibrary[_recordings]);
        Assert.Single(load.Complaints);
    }

    [Fact]
    public void A_row_that_asks_for_nothing_is_a_library_following_the_default()
    {
        // The page stores a follower by removing its row, so one left behind by hand
        // means the same thing. Keeping it would override a default the operator did
        // choose with nothing, which is the setting silently failing to apply.
        var load = ConfigurationFile.Read(File(
            "1",
            LocalName,
            "eng",
            Row(_recordings, string.Empty)));

        Assert.Empty(load.InForce.TargetLanguagesByLibrary);
        Assert.Empty(load.Complaints);
    }

    [Fact]
    public void The_reserved_word_is_a_target_and_so_is_nothing_at_all()
    {
        var detecting = ConfigurationFile.Read(File("1", LocalName, LanguageTarget.Detect, string.Empty));
        Assert.Empty(detecting.Complaints);
        Assert.True(LanguageTarget.IsDetection(detecting.InForce.TargetLanguage));

        var unchosen = ConfigurationFile.Read(File("1", string.Empty, string.Empty, string.Empty));
        Assert.Empty(unchosen.Complaints);
        Assert.True(LanguageTarget.IsAbsent(unchosen.InForce.TargetLanguage));
    }

    [Fact]
    public void A_complaint_says_the_field_the_reason_and_what_the_run_will_use()
    {
        // One line, because that is what a log takes and what a page has room for,
        // and all three parts because a reader who is told only that a setting was
        // refused will assume theirs is still the one in force.
        var load = ConfigurationFile.Read(File("1", "whisperx", "eng", string.Empty));

        var line = Assert.Single(load.Complaints).ToString();

        Assert.Contains(nameof(PluginConfiguration.Backend), line, StringComparison.Ordinal);
        Assert.Contains("whisperx", line, StringComparison.Ordinal);
        Assert.Contains(LocalName, line, StringComparison.Ordinal);
        Assert.False(line.Contains('\n', StringComparison.Ordinal));
    }

    [Fact]
    public void There_being_no_configuration_at_all_is_the_defaults_and_a_complaint()
    {
        // The server hands back whatever its serializer produced, and nothing in
        // its contract promises that is an object.
        var load = ConfigurationValidation.Of(null);

        Assert.Single(load.Complaints);
        AssertEverythingIsAtItsDefault(load);
    }

    [Fact]
    public void A_setting_that_is_absent_rather_than_blank_is_the_same_state()
    {
        // An element deleted out of the file by hand comes back as nothing rather
        // than as an empty string, and an operator means the same thing by both.
        var load = ConfigurationValidation.Of(new PluginConfiguration
        {
            Backend = null!,
            TargetLanguage = null!,
            LibraryTargets = null!,
        });

        Assert.Empty(load.Complaints);
        AssertEverythingIsAtItsDefault(load);
    }

    [Fact]
    public void A_row_that_is_nothing_at_all_is_dropped_and_the_rest_are_kept()
    {
        var load = ConfigurationValidation.Of(new PluginConfiguration
        {
            LibraryTargets =
            [
                null!,
                new LibraryLanguageTarget { LibraryId = _films.ToString(), Target = "fra" }
            ],
        });

        Assert.Equal(new[] { _films }, load.InForce.TargetLanguagesByLibrary.Keys.ToArray());
        Assert.Single(load.Complaints);
    }

    [Fact]
    public void Three_letters_the_server_has_no_language_under_are_refused_as_well()
    {
        // Two refusals rather than one. "klingon" is not a language code at all;
        // this is the other kind, three letters shaped exactly like one, which is
        // what somebody produces by copying a code out of a backend's own list.
        var load = ConfigurationValidation.Of(new PluginConfiguration { TargetLanguage = "xxx" });

        Assert.Single(load.Complaints);
        Assert.Equal(ConfigurationValidation.NoTargetLanguage, load.InForce.TargetLanguage);
    }

    [Fact]
    public void The_names_a_backend_may_be_configured_as_are_the_backends_this_plugin_has()
    {
        // A validator with its own copy of the list would refuse a name that then
        // worked, or accept one that then did not.
        Assert.True(BackendNames.IsKnown(NotConfiguredBackend.BackendName));
        Assert.True(BackendNames.IsKnown(LocalName));
        Assert.True(BackendNames.IsKnown("remote"));
        Assert.False(BackendNames.IsKnown("whisperx"));
        Assert.False(BackendNames.IsKnown(null));
    }

    private static void AssertEverythingIsAtItsDefault(ConfigurationLoad load)
    {
        Assert.Equal(ConfigurationValidation.CurrentSchemaVersion, load.InForce.SchemaVersion);
        Assert.Equal(ConfigurationValidation.NoBackendChosen, load.InForce.Backend);
        Assert.Equal(ConfigurationValidation.NoTargetLanguage, load.InForce.TargetLanguage);
        Assert.Empty(load.InForce.TargetLanguagesByLibrary);
    }

    private static string Row(Guid library, string target) => Row(library.ToString(), target);

    private static string Row(string library, string target) =>
        $"<LibraryLanguageTarget><LibraryId>{library}</LibraryId><Target>{target}</Target></LibraryLanguageTarget>";

    private static string File(string schemaVersion, string backend, string targetLanguage, string rows) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + "<PluginConfiguration>"
        + $"<SchemaVersion>{schemaVersion}</SchemaVersion>"
        + $"<Backend>{backend}</Backend>"
        + $"<TargetLanguage>{targetLanguage}</TargetLanguage>"
        + $"<LibraryTargets>{rows}</LibraryTargets>"
        + "</PluginConfiguration>";
}
