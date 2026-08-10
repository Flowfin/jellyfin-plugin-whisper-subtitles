using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the moment a server decides whether this plugin loaded.
/// No server is booted here and none of these says one was: what they judge is the
/// two calls the server makes against this assembly, made the same way against a
/// container this suite builds. A real server starting clean is #63's harness.
/// </summary>
public class PluginServiceRegistratorTests
{
    [Fact]
    public void The_server_can_construct_the_registrator()
    {
        // The server does Activator.CreateInstance on the type it found, with no
        // arguments. A constructor parameter here throws inside the server's own
        // try, which logs and disables this plugin.
        var found = typeof(PluginServiceRegistrator).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(found);

        // The type-taking overload on purpose. The server has a Type it found by
        // reflection and no compile-time knowledge of it, so the generic form would
        // assert against a call nothing makes.
#pragma warning disable CA2263
        Assert.NotNull(Activator.CreateInstance(typeof(PluginServiceRegistrator)));
#pragma warning restore CA2263
    }

    [Fact]
    public void The_server_can_build_the_scheduled_task_from_what_this_registers()
    {
        // ActivatorUtilities.CreateInstance is the call the server makes for every
        // exported scheduled task, and it resolves the constructor arguments out of
        // the container this registrator wrote into.
        using var provider = Registered();

        var task = ActivatorUtilities.CreateInstance<SubtitleGenerationTask>(provider);

        Assert.Equal(SubtitleGenerationTask.TaskKey, task.Key);
    }

    [Fact]
    public void Without_the_registration_the_same_construction_fails()
    {
        // The near-miss, and the state of the mainline before this change: one
        // missing registration, the same call, and the server's catch turns the
        // exception into a failed plugin.
        using var bare = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => ActivatorUtilities.CreateInstance<SubtitleGenerationTask>(bare));
    }

    [Fact]
    public void Registration_reads_nothing_from_the_application_host()
    {
        // Null is not a state the server produces. It is how this asserts the
        // argument is never dereferenced, which keeps registration independent of
        // the order plugins load in.
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        Assert.NotEmpty(services);
    }

    [Fact]
    public void Registration_builds_nothing()
    {
        // The container is not built yet when the server calls this. A descriptor
        // carrying an instance is one constructed during registration, which for the
        // handler is a socket pool opened on a plugin that may never transcribe.
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        Assert.All(services, descriptor => Assert.Null(descriptor.ImplementationInstance));
    }

    [Fact]
    public void Nothing_is_registered_under_a_name_this_plugin_does_not_own()
    {
        // The collection belongs to the server and is shared with every other
        // installed plugin. A registration under a framework type is one the last
        // writer wins, and either direction is one plugin changing another's
        // behaviour outside a declared interface.
        var services = new ServiceCollection();
        var mine = typeof(PluginServiceRegistrator).Assembly;

        new PluginServiceRegistrator().RegisterServices(services, null!);

        Assert.All(
            services,
            descriptor => Assert.True(
                Owned(descriptor.ServiceType, mine),
                $"{descriptor.ServiceType} is not a name this plugin owns."));
    }

    [Fact]
    public void Every_backend_this_plugin_has_is_a_candidate()
    {
        // Selection answers an operator who named a backend that does not exist by
        // listing the ones that do, so a backend missing from this list is one they
        // are told this plugin does not have.
        using var provider = Registered();

        var names = provider.GetRequiredService<IReadOnlyList<BackendCandidate>>()
            .Select(c => c.Name)
            .ToArray();

        Assert.Equal(
            new[]
            {
                NotConfiguredBackend.BackendName,
                LocalWhisperBackend.BackendName,
                RemoteWhisperBackend.BackendName,
            },
            names);
    }

    [Fact]
    public void A_backend_with_no_settings_says_which_ones_it_has_not_got()
    {
        // Out of the box neither working backend has been given anything, and what
        // an operator is told names the fields rather than saying it is unavailable.
        using var provider = Registered();
        var candidates = provider.GetRequiredService<IReadOnlyList<BackendCandidate>>();

        Assert.Equal(
            new[] { nameof(LocalBackendOptions.ExecutablePath), nameof(LocalBackendOptions.ModelPath) },
            Named(candidates, LocalWhisperBackend.BackendName).MissingSettings);

        Assert.Equal(
            new[] { nameof(RemoteBackendOptions.BaseUrl), nameof(RemoteBackendOptions.Model) },
            Named(candidates, RemoteWhisperBackend.BackendName).MissingSettings);

        Assert.Empty(Named(candidates, NotConfiguredBackend.BackendName).MissingSettings);
    }

    [Fact]
    public async Task A_fresh_install_runs_the_task_and_says_nothing_is_configured()
    {
        // The two halves joined: the task the server would have built, running on the
        // candidates this registrator supplied.
        using var provider = Registered();
        var task = ActivatorUtilities.CreateInstance<SubtitleGenerationTask>(provider);

        await task.ExecuteAsync(new NoProgress(), CancellationToken.None);

        Assert.Equal(NotConfiguredBackend.Explanation, task.LastReport);
    }

    [Fact]
    public void One_handler_serves_the_whole_plugin_and_the_container_can_dispose_it()
    {
        // A handler per request is socket exhaustion, and one nobody disposes is a
        // pool that outlives the plugin. The container disposes the singletons it
        // constructed, which is why a registered type owns the handler.
        using var provider = Registered();

        var first = provider.GetRequiredService<RemoteHttpHandler>();

        Assert.Same(first, provider.GetRequiredService<RemoteHttpHandler>());
        Assert.IsAssignableFrom<IDisposable>(first);
    }

    private static ServiceProvider Registered()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        return services.BuildServiceProvider();
    }

    private static BackendCandidate Named(IReadOnlyList<BackendCandidate> candidates, string name) =>
        candidates.Single(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private static bool Owned(Type serviceType, Assembly mine) =>
        serviceType.Assembly == mine
        || (serviceType.IsGenericType && serviceType.GetGenericArguments().Any(a => a.Assembly == mine));

    private sealed class NoProgress : IProgress<double>
    {
        public void Report(double value)
        {
        }
    }
}
