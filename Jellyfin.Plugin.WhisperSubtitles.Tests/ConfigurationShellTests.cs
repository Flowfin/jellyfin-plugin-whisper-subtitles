using System;
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
        var restored = serializer.Deserialize(read);

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
        var declared = typeof(PluginConfiguration)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var used = Regex.Matches(ConfigurationPage(), @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(used.Except(declared));
    }

    [Fact]
    public void The_page_reader_returns_the_page_and_not_an_empty_string()
    {
        // Guards the comparison above rather than the page: a reader that quietly
        // returned nothing would find no setting used and pass for the wrong
        // reason.
        Assert.Contains("WhisperSubtitlesConfigForm", ConfigurationPage(), StringComparison.Ordinal);
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
}
