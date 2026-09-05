using System;
using System.IO;
using MediaBrowser.Common.Plugins;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Where the composition root puts the record of what this plugin published:
/// the data directory the server reports for this plugin, read off the server's
/// own listing rather than off the static instance.
/// </summary>
/// <remarks>
/// The server's listing is a <see cref="LocalPlugin"/> carrying the loaded
/// instance, and the instance's data directory is what the server's base class
/// computed from the paths it was handed. That is arranged here with the same
/// doubles the identity tests use, so the answer is the one a server gives and
/// not a string typed into a test.
/// </remarks>
public sealed class RecordDirectoryTests
{
    [Fact]
    public void The_record_goes_where_the_server_says_this_plugin_keeps_its_data()
    {
        var paths = new UnwrittenApplicationPaths();
        var plugin = new PluginUnderTest(paths, new ThrowingXmlSerializer());
        var listed = new LocalPlugin(
            Path.Combine(paths.PluginsPath, "Jellyfin.Plugin.WhisperSubtitles"),
            isSupported: true,
            new PluginManifest { Id = PluginUnderTest.PluginId, Name = plugin.Name })
        {
            Instance = plugin,
        };

        var directory = PluginServiceRegistrator.DataDirectoryOf(listed);

        Assert.Equal(plugin.DataFolderPath, directory);
        Assert.StartsWith(paths.PluginsPath, directory, StringComparison.Ordinal);
    }

    [Fact]
    public void A_server_that_lists_no_loaded_instance_is_refused_rather_than_answered_with_a_guess()
    {
        // The two states a server can be in before this plugin is loaded: no
        // listing at all, and a listing whose instance is not built yet. Either
        // way the record is not opened somewhere else.
        Assert.Throws<InvalidOperationException>(() => PluginServiceRegistrator.DataDirectoryOf(null));

        var unbuilt = new LocalPlugin("/plugins/whisper", isSupported: true, new PluginManifest { Id = PluginUnderTest.PluginId });

        Assert.Throws<InvalidOperationException>(() => PluginServiceRegistrator.DataDirectoryOf(unbuilt));
    }

    [Fact]
    public void The_identity_the_root_asks_the_server_about_is_the_one_the_plugin_loads_under()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.Equal(plugin.Id, PluginUnderTest.PluginId);
    }
}
