using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
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
        serviceCollection.AddSingleton<IFileRemoval, SystemFileRemoval>();
        serviceCollection.AddSingleton<RemoteHttpHandler>();

        // The two backends that do work need settings, and these two lines are
        // where they arrive. Nothing here reads the configuration, so a
        // LocalToolPath and a LocalModelPath an operator has typed are validated
        // on load and then dropped at this line, and no setting holds a remote
        // endpoint or a key at all. Every candidate built from these reports what
        // it is missing rather than pretending to be ready, so what this costs is
        // not a wrong answer: it is two path fields on the page that change
        // nothing. The remark said the schema held none of this and went on
        // saying it after the fields landed, which is why BackendSettingsClaimTests
        // now refuses this file falling silent about a path the schema declares.
        serviceCollection.AddSingleton(_ => new LocalBackendOptions(null, null));
        serviceCollection.AddSingleton(_ => new RemoteBackendOptions(null, null, null));

        serviceCollection.AddSingleton<IReadOnlyList<BackendCandidate>>(provider => BackendCandidates.From(
            provider.GetRequiredService<IProcessRunner>(),
            provider.GetRequiredService<IFileFacts>(),
            provider.GetRequiredService<RemoteHttpHandler>().Handler,
            provider.GetRequiredService<LocalBackendOptions>(),
            provider.GetRequiredService<RemoteBackendOptions>()));
    }
}
