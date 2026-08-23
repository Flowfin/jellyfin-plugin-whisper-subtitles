using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The configuration is a shape the server writes to disk and reads back on every
/// start, and the configuration page is a second copy of that shape written in
/// another language. Neither the server nor the compiler compares them.
/// </summary>
public class ConfigurationShellTests
{
    /// <summary>
    /// Every declared setting the page is not expected to show, and why each one is
    /// here instead of on the page.
    /// </summary>
    /// <remarks>
    /// A comparison demanding a field for every declared property would be red the
    /// day it landed, so the set that is exempt has to be stated rather than left to
    /// whoever writes the next property. Stated here, one name at a time with its
    /// reason, so that adding a setting with no field is a decision written down
    /// rather than an omission nothing reports.
    /// </remarks>
    private static readonly Dictionary<string, string> _notOnThePage = new(StringComparer.Ordinal)
    {
        [nameof(PluginConfiguration.SchemaVersion)] =
            "It is the version of the file's own shape rather than a setting: the plugin writes it, a migration reads it, and an operator who edited it would be changing which rules the rest of the file is read under.",
    };

    [Fact]
    public void The_configuration_survives_the_serializer_the_server_stores_it_with()
    {
        // Jellyfin persists a plugin configuration with XmlSerializer. A property
        // whose type that serializer refuses compiles, ships, and then throws
        // inside the server on the first load, where the plugin is simply reported
        // as failed and the operator has no field to look at. Dictionaries and
        // interface-typed properties are the two shapes somebody reaches for first
        // when per-library settings arrive.
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var written = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        serializer.Serialize(written, new PluginConfiguration());

        using var read = new StringReader(written.ToString());

        // CA5369 asks for the XmlReader overload with DTD processing off. The
        // subject of this test is the call the server makes, in IXmlSerializer's
        // own implementation, and swapping in a safer overload here would leave the
        // test green while saying nothing about the path the configuration
        // actually travels. The input is a string this test wrote a line earlier.
#pragma warning disable CA5369
        var restored = serializer.Deserialize(read);
#pragma warning restore CA5369

        Assert.IsType<PluginConfiguration>(restored);
    }

    [Fact]
    public void The_configuration_page_reads_no_setting_the_configuration_does_not_carry()
    {
        // The page addresses settings by name through the API, so a name it gets
        // wrong is undefined at runtime rather than a compile error: the field
        // renders empty, saves an empty value over a good one, and reports
        // nothing. Today the set is empty on both sides, which is what this issue
        // asked for; the comparison is here so the first real setting cannot land
        // on one side alone.
        var used = Names(ConfigurationPage(), @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)");

        Assert.Empty(used.Except(Declared()));
    }

    [Fact]
    public void Every_setting_the_page_is_meant_to_carry_has_a_field_on_it()
    {
        // The other direction, and the one a comparison of names cannot get for
        // free. The leg above catches a page addressing a setting that is not
        // there; this one catches a setting that is there and reaches no page, so
        // an operator can only change it by editing the file the server writes.
        // It reads the two halves separately, because a name the page only reads
        // is a setting nobody can change and a name it only writes is one that
        // never comes back filled in.
        var page = ConfigurationPage();

        var expected = Declared().Except(_notOnThePage.Keys, StringComparer.Ordinal);

        Assert.Empty(expected.Except(Names(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)")));
        Assert.Empty(expected.Except(Names(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)\s*=")));
    }

    [Fact]
    public void Nothing_is_excused_from_the_page_that_the_configuration_no_longer_declares()
    {
        // The list above fails closed in the other direction too. A property that
        // is renamed or removed leaves its excuse behind, and an excuse matching no
        // property is one that silently covers nothing while reading as a decision
        // somebody took.
        Assert.Empty(_notOnThePage.Keys.Except(Declared(), StringComparer.Ordinal));
    }

    [Fact]
    public void The_page_reader_returns_the_page_and_not_an_empty_string()
    {
        // Guards the comparison above rather than the page: a reader that quietly
        // returned nothing would find no setting used and pass for the wrong
        // reason.
        Assert.Contains("WhisperSubtitlesConfigForm", ConfigurationPage(), StringComparison.Ordinal);
    }

    private static HashSet<string> Declared() =>
        typeof(PluginConfiguration)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

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
