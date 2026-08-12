using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WhisperSubtitles;

/// <summary>
/// The one place this plugin constructs the real thing behind a seam.
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
        serviceCollection.AddSingleton<RemoteHttpHandler>();

        // The two backends that do work need settings, and no setting on the
        // configuration holds one yet: the schema carries the backend name, the
        // target language and the per-library targets, and the page an operator
        // would type a tool path or an endpoint into is #36. Until those fields
        // exist these two lines are where they arrive, and every candidate built
        // from them reports what it is missing rather than pretending to be ready.
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
