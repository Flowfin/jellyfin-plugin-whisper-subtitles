using System;
using System.Collections.Generic;
using System.Net.Http;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Builds every backend this plugin has, with the settings each one is missing.
/// </summary>
/// <remarks>
/// Here rather than in the composition root because this folder is the one
/// allowed to name a concrete backend, which
/// <c>No_type_outside_the_backend_folder_names_a_concrete_backend</c> refuses a
/// breach of. The registrator calls this and never names a backend itself.
///
/// The list is every backend rather than the configured one, because selection
/// answers an operator who named a backend that does not exist by listing the
/// ones that do, and that answer needs a complete list.
/// </remarks>
public static class BackendCandidates
{
    /// <summary>
    /// Builds one candidate per backend from the seams and the settings each was
    /// given.
    /// </summary>
    /// <param name="runner">The seam every child process is started through.</param>
    /// <param name="httpHandler">The seam every remote request goes through.</param>
    /// <param name="local">The paths the local backend was configured with.</param>
    /// <param name="remote">The endpoint the remote backend was configured with.</param>
    /// <returns>Every backend this plugin has.</returns>
    /// <remarks>
    /// The missing settings are the property names of the options types rather
    /// than the labels a page would show, and they travel into a sentence an
    /// operator reads. That wording is a debt this owes the page in #36 rather
    /// than an oversight.
    /// </remarks>
    public static IReadOnlyList<BackendCandidate> From(
        IProcessRunner runner,
        HttpMessageHandler httpHandler,
        LocalBackendOptions local,
        RemoteBackendOptions remote)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(httpHandler);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        return new[]
        {
            new BackendCandidate(
                NotConfiguredBackend.BackendName,
                new NotConfiguredBackend(),
                Array.Empty<string>()),
            new BackendCandidate(
                LocalWhisperBackend.BackendName,
                new LocalWhisperBackend(runner, local),
                MissingFrom(local)),
            new BackendCandidate(
                RemoteWhisperBackend.BackendName,
                new RemoteWhisperBackend(httpHandler, remote),
                MissingFrom(remote)),
        };
    }

    private static List<string> MissingFrom(LocalBackendOptions local)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(local.ExecutablePath))
        {
            missing.Add(nameof(LocalBackendOptions.ExecutablePath));
        }

        if (string.IsNullOrWhiteSpace(local.ModelPath))
        {
            missing.Add(nameof(LocalBackendOptions.ModelPath));
        }

        return missing;
    }

    private static List<string> MissingFrom(RemoteBackendOptions remote)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(remote.BaseUrl))
        {
            missing.Add(nameof(RemoteBackendOptions.BaseUrl));
        }

        if (string.IsNullOrWhiteSpace(remote.Model))
        {
            missing.Add(nameof(RemoteBackendOptions.Model));
        }

        return missing;
    }
}
