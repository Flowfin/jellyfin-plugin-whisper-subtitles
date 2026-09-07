using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Library;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WhisperSubtitles;

/// <summary>
/// Where this plugin constructs the real thing behind a seam for the server to
/// hand out.
/// </summary>
/// <remarks>
/// The server finds a plugin's scheduled tasks by reflection and builds each one
/// out of its own container, so a constructor argument nothing registered is not
/// a task missing a dependency: it is a plugin the server marks as failed. That
/// is read off the server's own source on both supported lines, and the commands
/// are in the pull request that landed this class.
///
/// Only types this plugin owns are registered, which is why the HTTP handler
/// arrives as <see cref="RemoteHttpHandler"/> rather than under a framework name.
///
/// This is the only place a real implementation behind a seam is built, and
/// <c>CompositionRootTests</c> refuses the next one built anywhere else. It reads
/// the implementation types out of a collection this class registered rather than
/// out of a list, so a seam registered tomorrow is covered without anybody
/// remembering a line. What it cannot see is stated in its own remarks.
///
/// The sweep in <see cref="Audio.TemporaryAudioSweep"/> was the one exception
/// until #71: it held its removal as a static and its one-argument overload
/// closed over it, so a caller took the real one without asking any container for
/// it. That overload is gone and the removal is registered below.
///
/// Nothing is resolved here and nothing throws. The container is not built yet at
/// this point, and a registrator that throws is caught by the server, logged and
/// turned into a disabled plugin. Both are asserted rather than intended.
/// </remarks>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="applicationHost"/> is not read. Nothing this plugin does
    /// needs the host at registration time, and reaching into it is the shape that
    /// makes a registrator depend on the order plugins happen to be loaded in.
    /// </remarks>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        serviceCollection.AddSingleton<IProcessRunner, SystemProcessRunner>();
        serviceCollection.AddSingleton<IFileFacts, SystemFileFacts>();

        // The adapter between this plugin and the server's library. Its one
        // constructor argument is the server's own library manager, which the
        // server registers before any plugin is asked to register anything, so
        // this line names a type and resolves nothing.
        serviceCollection.AddSingleton<ILibrarySource, ServerLibrarySource>();
        serviceCollection.AddSingleton<IFileRemoval, SystemFileRemoval>();
        serviceCollection.AddSingleton<IFileDigest, SystemFileDigest>();

        // The record of what this plugin published lives in the data directory
        // the server hands this plugin, and the server is asked for it here rather
        // than the plugin's static instance being read, which is the shape #71
        // set out to stop. Asked at resolve time and never at registration, so
        // this registrator still reads nothing and throws nothing; a server that
        // has not loaded this plugin by the time a route needs the record answers
        // that route with the sentence below rather than a file in the wrong
        // place.
        serviceCollection.AddSingleton<IPublishedSubtitleRecord>(provider =>
            new PublishedSubtitleRecordFile(DataDirectoryOf(provider.GetRequiredService<IPluginManager>().GetPlugin(Plugin.PluginId))));
        serviceCollection.AddSingleton<RemoteHttpHandler>();

        // The two backends that do work need settings, and these two lines are
        // where they arrive. Nothing here reads the configuration, so a
        // LocalToolPath and a LocalModelPath an operator has typed are validated
        // on load and then dropped at the first line, and a RemoteBaseUrl, a
        // RemoteApiKey and a RemoteModel are validated on load and dropped at the
        // second. Every candidate built from these reports what it is missing
        // rather than pretending to be ready, so what this costs is not a wrong
        // answer: it is five fields on the page that change nothing, and what
        // would carry them here is the composition root reading the
        // configuration, which is #71. The remark said the schema held none of
        // this and went on saying it after the fields landed, which is why
        // BackendSettingsClaimTests now refuses this file falling silent about a
        // setting the schema declares for either backend.
        serviceCollection.AddSingleton(_ => new LocalBackendOptions(null, null));
        serviceCollection.AddSingleton(_ => new RemoteBackendOptions(null, null, null));

        serviceCollection.AddSingleton<IReadOnlyList<BackendCandidate>>(provider => BackendCandidates.From(
            provider.GetRequiredService<IProcessRunner>(),
            provider.GetRequiredService<IFileFacts>(),
            provider.GetRequiredService<RemoteHttpHandler>().Handler,
            provider.GetRequiredService<LocalBackendOptions>(),
            provider.GetRequiredService<RemoteBackendOptions>()));
    }

    /// <summary>
    /// The data directory the server reports for this plugin.
    /// </summary>
    /// <param name="listed">What the server's plugin manager lists under this plugin's id, or null when it lists nothing.</param>
    /// <returns>The directory.</returns>
    /// <exception cref="InvalidOperationException">The server lists no loaded instance of this plugin.</exception>
    public static string DataDirectoryOf(LocalPlugin? listed)
    {
        var directory = listed?.Instance?.DataFolderPath;

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "The server lists no loaded instance of this plugin, so the directory it keeps its record in is unknown and the record is not opened.");
        }

        return directory;
    }
}
