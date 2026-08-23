using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend is the first thing an operator has to choose and the only setting
/// that decides whether anything runs at all. The page is a second copy of the set
/// of names it may hold, written in another language, and nothing but this compares
/// the two.
/// </summary>
/// <remarks>
/// The failure this is written against is quiet in both directions. A page offering
/// a name the plugin does not answer to saves a value selection refuses, and the
/// operator reads a page that accepted their choice while every run says nothing is
/// transcribed. A page missing a name the plugin does have hides a backend behind a
/// file an operator has to edit by hand.
///
/// Nothing here opens the page in a browser or loads it into a server. What is
/// compared is the markup and its script against the code, which is the replacement
/// this repository declares for a page test that would need a display, and the load
/// itself is owed by #63.
/// </remarks>
public class BackendChoicePageTests
{
    [Fact]
    public void The_page_offers_every_backend_this_plugin_answers_to_and_no_other()
    {
        // Both directions on purpose. One catches a backend added in code that an
        // operator can then only select by editing the file on disk; the other
        // catches a name on the page that validation refuses and selection has
        // never heard of.
        var offered = BackendsThePageOffers();

        Assert.Equal(
            BackendNames.Known.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            offered.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void The_page_reads_and_writes_the_backend_setting()
    {
        // A name the page only reads is a setting an operator can never change, and
        // a name it only writes is one that never comes back filled in.
        var page = ConfigurationPage();

        Assert.Contains(
            nameof(PluginConfiguration.Backend),
            Names(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)"));

        Assert.Contains(
            nameof(PluginConfiguration.Backend),
            Names(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)\s*="));
    }

    [Fact]
    public void The_unchosen_state_is_offered_by_the_value_the_code_reads_it_as()
    {
        // Blank and the do-nothing backend are two states with two sentences. Blank
        // is nothing chosen; "None" is a backend that was chosen and transcribes
        // nothing on purpose. A page that offered one value for both would collapse
        // them, and the operator would be told the wrong one of the two.
        var page = ConfigurationPage();

        Assert.Contains(
            "noBackendChosen: '" + ConfigurationValidation.NoBackendChosen + "'",
            page,
            StringComparison.Ordinal);

        Assert.DoesNotContain(ConfigurationValidation.NoBackendChosen, BackendsThePageOffers());
        Assert.False(BackendNames.IsKnown(ConfigurationValidation.NoBackendChosen));
    }

    [Fact]
    public void A_stored_name_this_plugin_does_not_have_is_kept_rather_than_replaced()
    {
        // Validation hands on a backend name it does not know, unchanged, so that
        // selection can answer with the name itself, and that sentence is what
        // repairs a typo. A page offering only the names it knows would show blank
        // for such a value, and the next save would write the blank over the name,
        // leaving an operator with a plugin that reports nothing configured rather
        // than one that reports what is wrong.
        //
        // WHAT THIS READS IS THE SCRIPT AS TEXT AND NOT ITS BEHAVIOUR. Nothing here
        // runs the page, so this says the branch is written rather than that it
        // does what the sentence above describes. The page under a server that
        // booted is owed by #63, which this repository's list of refused tests
        // already names.
        var page = ConfigurationPage();

        Assert.Contains("which is not a backend this plugin has", page, StringComparison.Ordinal);
        Assert.Contains("document.createElement('option')", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_matches_a_stored_name_as_loosely_as_the_code_does()
    {
        // The comparison in BackendNames ignores case, and so does the one in
        // selection. A page stricter than either would label a stored "local" as a
        // name this plugin does not have while every run treats it as the local
        // backend, which is a page contradicting the thing it configures. Read as
        // text, with the same bound as the leg above.
        var recased = BackendNames.Known
            .Select(name => name.ToUpperInvariant())
            .Where(name => !BackendNames.Known.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.NotEmpty(recased);
        Assert.All(recased, name => Assert.True(BackendNames.IsKnown(name)));

        Assert.Contains(
            "backend.name.toLowerCase() === wanted.toLowerCase()",
            ConfigurationPage(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_reader_behind_these_comparisons_finds_the_vocabulary_it_judges()
    {
        // Guards the three legs above rather than the page. A regular expression
        // that matched nothing would find no backend on the page and report a page
        // offering exactly the empty set, which is a comparison passing for the
        // wrong reason.
        Assert.NotEmpty(BackendsThePageOffers());
    }

    private static HashSet<string> BackendsThePageOffers() =>
        Names(ConfigurationPage(), @"\{\s*name:\s*'([^']*)'");

    private static HashSet<string> Names(string page, string pattern) =>
        Regex.Matches(page, pattern)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string ConfigurationPage()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());
        var page = Assert.Single(plugin.GetPages());

        using var stream = typeof(PluginUnderTest).Assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);

        return reader.ReadToEnd();
    }
}
