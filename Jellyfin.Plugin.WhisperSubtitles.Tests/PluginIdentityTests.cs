using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public void The_assembly_is_stamped_with_the_version_the_manifest_declares()
    {
        // build.yaml is the only file the version is written in and the build reads
        // the assembly's from it, so these two can disagree only where something has
        // come between them: a Version property put back into a project file, or a
        // manifest line the reader in Directory.Build.props no longer matches. What
        // that produces is an archive whose catalogue entry and whose assembly claim
        // different versions, and a server tells the operator the first of the two.
        //
        // The publish workflow makes this comparison as well, and it makes it on a
        // pushed tag, which is after the number has been chosen and announced. This
        // one runs on the pull request that moved it.
        var declared = Version.Parse(ManifestField("version"));
        var stamped = typeof(PluginUnderTest).Assembly.GetName().Version;

        Assert.NotNull(stamped);
        Assert.Equal(FourParts(declared), FourParts(stamped!));
    }

    /// <summary>
    /// Pads a version to four parts, so 1.4.0 and 1.4.0.0 compare as the same number
    /// written two ways rather than as a disagreement. The publish workflow pads both
    /// sides for the same reason.
    /// </summary>
    private static Version FourParts(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    [Fact]
    public void Plugin_reports_the_name_the_manifest_declares()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.False(string.IsNullOrWhiteSpace(plugin.Name));
        Assert.Equal(ManifestField("name"), plugin.Name);
    }

    [Fact]
    public void Neither_half_of_the_identity_is_still_the_template_value()
    {
        // The template ships one guid to every plugin generated from it. Two such
        // plugins on one server are one plugin as far as configuration storage is
        // concerned, and the comparison above is satisfied by both halves being
        // wrong in the same way, so it cannot catch this on its own.
        const string TemplateGuid = "eb5d7894-8eef-4b36-aa6f-5d124e828ce1";

        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.NotEqual(Guid.Parse(TemplateGuid), plugin.Id);
        Assert.NotEqual(TemplateGuid, ManifestField("guid").ToLowerInvariant());
        Assert.NotEqual("Template", ManifestField("name"));
    }

    [Fact]
    public void The_configuration_page_addresses_the_plugin_by_the_guid_the_plugin_reports()
    {
        // The page is the third copy of the identity and the only one no server
        // reads at install time. A guid changed in the assembly and the manifest
        // and left behind here gives an administrator a page that loads, saves,
        // and writes the configuration of a plugin that is not this one.
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());
        var page = Assert.Single(plugin.GetPages());

        using var stream = typeof(PluginUnderTest).Assembly.GetManifestResourceStream(page.EmbeddedResourcePath);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var html = reader.ReadToEnd();

        Assert.Contains(
            "pluginUniqueId: '" + plugin.Id.ToString("D", CultureInfo.InvariantCulture) + "'",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_manifest_declares_a_framework_this_repository_builds()
    {
        // A manifest naming a framework nothing here produces advertises an
        // artefact that does not exist, and jprm will package whatever is in the
        // output directory under that name regardless.
        var built = BuildProperty("SupportedServerLines").Split(';', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(ManifestField("framework"), built);
    }

    [Fact]
    public void The_manifest_target_abi_is_the_server_line_of_the_framework_it_declares()
    {
        // targetAbi is what a server compares itself against before it will load
        // the plugin, and the framework is what the assembly was compiled for. A
        // pair that disagrees either refuses to install on the server it was built
        // for, or installs on one whose runtime cannot load it.
        //
        // One manifest describes one line and this tree builds two, so each run
        // can only judge its own: the run whose framework the manifest names
        // requires the abi to be that run's line, and every other run requires the
        // abi not to be its own. Together the runs cover the pair; separately
        // neither does, and this is what the build matrix is for.
        var declaredFramework = ManifestField("framework");
        var declaredAbi = ManifestField("targetAbi");
        var thisFramework = BuildProperty("TargetFramework");
        var thisLine = BuildProperty("JellyfinServerLine");

        if (declaredFramework == thisFramework)
        {
            Assert.StartsWith(thisLine + ".", declaredAbi, StringComparison.Ordinal);
        }
        else
        {
            Assert.False(
                declaredAbi.StartsWith(thisLine + ".", StringComparison.Ordinal),
                $"The manifest declares framework {declaredFramework} with targetAbi {declaredAbi}, which is the {thisLine} line, and {thisLine} is built as {thisFramework}.");
        }
    }

    /// <summary>
    /// Reads one property the build itself used, carried into this assembly by the
    /// test project rather than written out a second time by hand.
    /// </summary>
    private static string BuildProperty(string key)
    {
        var value = typeof(PluginIdentityTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal))
            ?.Value;

        Assert.False(string.IsNullOrWhiteSpace(value), $"The test project carries no build property named {key}.");

        return value!;
    }

    /// <summary>
    /// Guards the reader above for the same reason the manifest reader is guarded:
    /// a comparison against a property that silently arrived empty compares
    /// nothing and passes.
    /// </summary>
    [Fact]
    public void The_build_property_reader_finds_a_property_that_is_present_and_refuses_one_that_is_not()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildProperty("TargetFramework")));

        Assert.ThrowsAny<Exception>(() => BuildProperty("a-property-the-build-does-not-set"));
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
