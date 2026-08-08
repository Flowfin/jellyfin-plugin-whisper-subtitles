using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Which language to produce is the operator's decision and it is not one decision
/// for a whole server. What is asserted here is that the answer a library gives is
/// the answer selection reads, through every copy of the setting between the
/// operator's field and the filter: the page, the two serializers the value
/// travels through, and the fallback to the default.
/// </summary>
public class TargetLanguageTests
{
    private static readonly Guid _films = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _recordings = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _shelfWithNoOpinion = new("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset _epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_library_that_names_a_target_wins_over_the_default()
    {
        var options = Options(
            serverWide: "eng",
            perLibrary: new Dictionary<Guid, string> { [_recordings] = "deu" });

        Assert.Equal("deu", options.TargetFor(_recordings));
        Assert.Equal("eng", options.TargetFor(_films));
        Assert.Equal("eng", options.TargetFor(_shelfWithNoOpinion));
    }

    [Fact]
    public void A_library_row_that_was_cleared_falls_back_to_the_default()
    {
        // Not to nothing. A library with no target is dropped out of every run, and
        // an operator who blanked a field was choosing to follow the default rather
        // than choosing to stop transcribing that library with no message anywhere.
        var options = Options(
            serverWide: "eng",
            perLibrary: new Dictionary<Guid, string> { [_recordings] = "   " });

        Assert.Equal("eng", options.TargetFor(_recordings));
    }

    [Fact]
    public void Two_libraries_wanting_two_languages_select_different_items_in_one_run()
    {
        // The whole reason the setting is per library. Each item is judged against
        // its own library's target in a single pass, so an operator does not run the
        // task once per language with the other libraries switched off by hand.
        var germanFilmWithGerman = Item("Das Boot", _recordings, subtitles: ["deu"]);
        var germanFilmWithEnglish = Item("Der Untergang", _recordings, subtitles: ["eng"]);
        var englishFilmWithEnglish = Item("Arrival", _films, subtitles: ["eng"]);
        var englishFilmWithGerman = Item("Blade Runner", _films, subtitles: ["deu"]);

        var result = ItemSelection.Select(
            [germanFilmWithGerman, germanFilmWithEnglish, englishFilmWithEnglish, englishFilmWithGerman],
            Options(
                serverWide: "eng",
                perLibrary: new Dictionary<Guid, string> { [_recordings] = "deu" }));

        Assert.Equal(
            new[] { "Blade Runner", "Der Untergang" },
            result.Candidates.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void An_item_whose_library_targets_a_language_it_already_has_is_not_selected()
    {
        var item = Item("Das Boot", _recordings, subtitles: ["deu"]);

        var result = ItemSelection.Select(
            [item],
            Options(
                serverWide: "eng",
                perLibrary: new Dictionary<Guid, string> { [_recordings] = "deu" }));

        Assert.Empty(result.Candidates);

        // And it is the library's target that did it rather than the default. The
        // same item under the default alone is a candidate, so a filter that ignored
        // the per-library value would pass the leg above only by luck.
        Assert.Single(ItemSelection.Select([item], Options(serverWide: "eng")).Candidates);
    }

    [Fact]
    public void A_library_left_on_a_default_that_was_never_chosen_selects_nothing()
    {
        // Out of the box. The configuration ships empty and empty is not detection,
        // so a fresh install with a backend configured still transcribes nothing
        // until somebody says into what.
        var result = ItemSelection.Select(
            [Item("Arrival", _films), Item("Das Boot", _recordings)],
            Options(serverWide: string.Empty));

        Assert.Empty(result.Candidates);
        Assert.Equal(TimeSpan.Zero, result.TotalDuration);
    }

    [Fact]
    public void Detection_takes_an_item_with_no_subtitle_and_leaves_one_that_has_any()
    {
        // Under detection the language that will come out is not knowable here, so
        // the filter cannot ask whether the item has that one. What it can ask is
        // whether the item has any, and an item that already shows a track in a
        // client is not the one to spend a transcription on finding out.
        var result = ItemSelection.Select(
            [Item("Untitled Recording", _recordings), Item("Talk With Captions", _recordings, subtitles: ["eng"])],
            Options(
                serverWide: string.Empty,
                perLibrary: new Dictionary<Guid, string> { [_recordings] = LanguageTarget.Detect }));

        var only = Assert.Single(result.Candidates);
        Assert.Equal("Untitled Recording", only.Name);
    }

    [Fact]
    public void The_word_that_asks_for_detection_cannot_be_a_language()
    {
        // What keeps a reserved word out of the value space it shares with language
        // codes. ISO 639 codes are two or three letters, so a six letter word cannot
        // be one, and this is asserted rather than believed because the day somebody
        // shortens it to two letters is the day a library asking for detection gets
        // a subtitle named in a language that does not exist.
        Assert.True(LanguageTarget.Detect.Length > 3);
        Assert.True(LanguageTarget.IsDetection("Detect"));
        Assert.True(LanguageTarget.IsDetection(" detect "));
        Assert.False(LanguageTarget.IsDetection("de"));
        Assert.False(LanguageTarget.IsDetection(null));

        // Blank is not detection, and that is the fail-closed half.
        Assert.False(LanguageTarget.IsDetection(string.Empty));
        Assert.True(LanguageTarget.IsAbsent(string.Empty));
        Assert.True(LanguageTarget.IsAbsent("  "));
        Assert.False(LanguageTarget.IsAbsent(LanguageTarget.Detect));
    }

    [Fact]
    public void The_setting_survives_the_serializer_the_server_stores_it_with()
    {
        var configured = new PluginConfiguration
        {
            TargetLanguage = "eng",
            LibraryTargets =
            [
                new LibraryLanguageTarget { LibraryId = _recordings.ToString(), Target = "deu" },
                new LibraryLanguageTarget { LibraryId = _films.ToString(), Target = LanguageTarget.Detect }
            ]
        };

        var restored = RoundTripThroughXml(configured);

        Assert.Equal("eng", restored.TargetLanguage);
        Assert.Equal("deu", restored.TargetLanguagesByLibrary()[_recordings]);
        Assert.Equal(LanguageTarget.Detect, restored.TargetLanguagesByLibrary()[_films]);
    }

    [Fact]
    public void The_setting_survives_the_serializer_the_configuration_page_posts_it_through()
    {
        // The other half of the round trip, and the one that fails differently. The
        // page reads and writes the configuration over the server's JSON API, and
        // System.Text.Json will not fill a property it cannot set, so a shape that
        // is fine on disk can come back from a save with the rows gone and nothing
        // in any log.
        var configured = new PluginConfiguration
        {
            TargetLanguage = LanguageTarget.Detect,
            LibraryTargets = [new LibraryLanguageTarget { LibraryId = _recordings.ToString(), Target = "deu" }]
        };

        var posted = JsonSerializer.Serialize(configured);
        var restored = JsonSerializer.Deserialize<PluginConfiguration>(posted);

        Assert.NotNull(restored);
        Assert.Equal(LanguageTarget.Detect, restored!.TargetLanguage);
        var row = Assert.Single(restored.LibraryTargets);
        Assert.Equal(_recordings.ToString(), row.LibraryId);
        Assert.Equal("deu", row.Target);
    }

    [Fact]
    public void A_row_naming_no_library_this_server_has_is_dropped_and_not_thrown_on()
    {
        // Read on every run out of a file an operator can edit. One unparseable line
        // taking the whole configuration down turns a typo into a plugin that fails
        // to load with nothing to look at, and the library it named is left on the
        // default, which is where it was before the line was written.
        var configured = new PluginConfiguration
        {
            TargetLanguage = "eng",
            LibraryTargets =
            [
                new LibraryLanguageTarget { LibraryId = "not a guid", Target = "deu" },
                new LibraryLanguageTarget { LibraryId = _recordings.ToString(), Target = "deu" }
            ]
        };

        var targets = configured.TargetLanguagesByLibrary();

        Assert.Equal(new[] { _recordings }, targets.Keys.ToArray());
    }

    [Fact]
    public void The_page_reads_and_writes_both_halves_of_the_setting()
    {
        // A name the page only reads is a setting an operator can never change, and
        // a name it only writes is one that never comes back filled in. Both
        // directions are compared against the declared properties, so the first real
        // setting cannot land on one side alone.
        var page = ConfigurationPage();

        var declared = typeof(PluginConfiguration)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var read = Names(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)");
        var written = Names(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)\s*=");

        Assert.Empty(read.Except(declared));
        Assert.Contains(nameof(PluginConfiguration.TargetLanguage), read);
        Assert.Contains(nameof(PluginConfiguration.LibraryTargets), read);
        Assert.Contains(nameof(PluginConfiguration.TargetLanguage), written);
        Assert.Contains(nameof(PluginConfiguration.LibraryTargets), written);
    }

    [Fact]
    public void The_page_offers_the_reserved_word_and_the_unchosen_state_by_the_names_the_code_uses()
    {
        // The page is a second copy of this vocabulary written in another language,
        // and nothing but this compares them. A page offering "auto" would save a
        // value every reader here treats as a language code.
        var page = ConfigurationPage();

        Assert.Contains("detect: '" + LanguageTarget.Detect + "'", page, StringComparison.Ordinal);
        Assert.Contains("followsTheDefault: ''", page, StringComparison.Ordinal);
    }

    private static HashSet<string> Names(string page, string pattern) =>
        Regex.Matches(page, pattern)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static PluginConfiguration RoundTripThroughXml(PluginConfiguration configuration)
    {
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var written = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        serializer.Serialize(written, configuration);

        using var read = new StringReader(written.ToString());

        // The subject is the call the server makes, in IXmlSerializer's own
        // implementation. Swapping in the safer overload the analyser asks for would
        // leave this green while saying nothing about the path the configuration
        // travels, and the input is a string this method wrote two lines earlier.
#pragma warning disable CA5369
        return (PluginConfiguration)serializer.Deserialize(read)!;
#pragma warning restore CA5369
    }

    private static string ConfigurationPage()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());
        var page = Assert.Single(plugin.GetPages());

        using var stream = typeof(PluginUnderTest).Assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);

        return reader.ReadToEnd();
    }

    private static SelectionOptions Options(
        string? serverWide,
        IReadOnlyDictionary<Guid, string>? perLibrary = null) =>
        new(
            [_films, _recordings, _shelfWithNoOpinion],
            ["Movie", "Episode"],
            serverWide,
            maximumItemDuration: null,
            addedSince: null,
            quarantinedItems: null,
            targetLanguagesByLibrary: perLibrary);

    private static ItemDescription Item(
        string name,
        Guid library,
        IReadOnlyList<string>? subtitles = null) =>
        new(
            DeterministicId(name),
            name,
            library,
            "Movie",
            TimeSpan.FromMinutes(97),
            hasAudioStream: true,
            subtitles ?? [],
            _epoch);

    /// <summary>
    /// An identifier derived from the name, so a fixture is reproducible and two
    /// runs of the suite compare the same items.
    /// </summary>
    private static Guid DeterministicId(string name)
    {
        var bytes = new byte[16];
        var source = System.Text.Encoding.UTF8.GetBytes(name);

        for (var i = 0; i < source.Length; i++)
        {
            bytes[i % 16] ^= source[i];
        }

        return new Guid(bytes);
    }
}
