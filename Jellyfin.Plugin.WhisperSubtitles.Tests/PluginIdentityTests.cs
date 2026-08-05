using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The manifest and the assembly each carry the plugin's identity, and a server
/// installs the pair. If they disagree the operator gets a plugin the catalogue
/// cannot match to what is running.
/// </summary>
public class PluginIdentityTests
{
    [Fact]
    public void Plugin_reports_the_guid_the_manifest_declares()
    {
        var manifestGuid = ManifestField("guid");

        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.Equal(Guid.Parse(manifestGuid), plugin.Id);
    }

    [Fact]
    public void The_manifest_declares_a_guid_that_is_a_guid()
    {
        // Without this, a manifest whose guid line was emptied or mangled would
        // turn the comparison above into a comparison of two failures to parse.
        Assert.True(Guid.TryParse(ManifestField("guid"), out _));
    }

    /// <summary>
    /// Reads one top-level scalar out of build.yaml.
    /// </summary>
    /// <remarks>
    /// This is a line reader and not a YAML parser, which is the whole of what it
    /// can do: it finds a key at column zero whose value is a plain scalar on the
    /// same line, and it would not survive an anchor, a block scalar or a nested
    /// mapping. The manifest is a flat file that JPRM writes and reads in that
    /// shape, so the bound costs nothing here and buys no dependency.
    /// </remarks>
    private static string ManifestField(string key)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "build.yaml");
        Assert.True(File.Exists(path), $"build.yaml was not copied next to the test assembly, looked in {AppContext.BaseDirectory}");

        var prefix = key + ":";
        var line = File.ReadAllLines(path).FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));

        Assert.NotNull(line);

        return line!.Substring(prefix.Length).Trim().Trim('"').Trim();
    }

    /// <summary>
    /// Guards the reader above rather than the manifest, so a change that quietly
    /// stops finding a field cannot make the identity comparison pass by reading
    /// the same wrong thing twice.
    /// </summary>
    [Fact]
    public void The_manifest_reader_finds_a_field_that_is_present_and_refuses_one_that_is_not()
    {
        Assert.False(string.IsNullOrWhiteSpace(ManifestField("version")));

        Assert.ThrowsAny<Exception>(() => ManifestField("a-key-the-manifest-does-not-have"));
    }

    [Fact]
    public void Plugin_reports_a_name()
    {
        // The manifest's own name field is deliberately not compared here. The two
        // disagree today by the plan's own division of work, and #4 holds the
        // check that refuses the disagreement.
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.False(string.IsNullOrWhiteSpace(plugin.Name));
    }

    [Fact]
    public void The_configuration_page_is_embedded_under_the_namespace_the_plugin_reports()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        var page = Assert.Single(plugin.GetPages());

        var expected = string.Format(
            CultureInfo.InvariantCulture,
            "{0}.Configuration.configPage.html",
            typeof(PluginUnderTest).Namespace);

        Assert.Equal(expected, page.EmbeddedResourcePath);

        // A renamed namespace that left the resource path behind loads a
        // configuration page that is not there, which the server reports as an
        // empty page rather than as an error.
        using var stream = typeof(PluginUnderTest).Assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
        Assert.NotNull(stream);
    }
}
