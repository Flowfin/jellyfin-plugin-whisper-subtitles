using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Answers the configuration page's question, which is whether the chosen
/// backend can be used right now with these settings, saved or not.
/// </summary>
/// <remarks>
/// The settings arrive in the shape the page saves, and they go through the same
/// rule the file goes through at load. So a value the load would refuse is
/// refused here for the same words, and nothing looks at a path or a host the
/// load would not have named. That is the one thing this adds. Everything after
/// it is the selection a run makes and the probe each backend already answers.
///
/// Selection is reused rather than a backend being asked directly, because
/// selection is where "you named a backend this plugin has not got" and "you
/// left a setting out" are already sentences, and a second reading of the same
/// configuration here would be the same rules in two places with nothing keeping
/// them equal. The one thing selection says that the page did not ask is that
/// nothing is transcribed, and it stays in: it is true of the run the operator is
/// about to schedule with these settings.
///
/// The backends are built here, from the settings that were asked about, and are
/// not taken from the container. The container holds the backends built from what
/// the composition root read, which today is nothing, and a question about values
/// as they stand on a page cannot be answered by a backend built from other
/// values. The construction is <see cref="BackendCandidates.From"/>'s, in the one
/// folder allowed to name a concrete backend, and the seams the backends look
/// through are the caller's, so a test drives this with no disk and no socket.
/// </remarks>
public static class ReadinessQuestion
{
    /// <summary>
    /// Answers whether the backend the settings choose can be used right now.
    /// </summary>
    /// <param name="asked">The settings as they stand on the page, or null when the page sent nothing readable.</param>
    /// <param name="runner">The seam every child process is started through.</param>
    /// <param name="files">The seam the local backend looks at a path through.</param>
    /// <param name="httpHandler">The seam the remote backend reaches an endpoint through.</param>
    /// <param name="cancellationToken">Stops the question, which is the operator leaving the page.</param>
    /// <returns>The backend the settings choose, whether it is ready, and what stands in the way when it is not.</returns>
    /// <remarks>
    /// Cancellation is not turned into an answer. An operator who left the page has
    /// learned nothing about their backend, and the probes already tell that apart
    /// from a deadline of their own.
    /// </remarks>
    public static async Task<ReadinessReport> AskAsync(
        PluginConfiguration? asked,
        IProcessRunner runner,
        IFileFacts files,
        HttpMessageHandler httpHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(httpHandler);

        var settings = ConfigurationValidation.Of(asked).InForce;

        var candidates = BackendCandidates.From(
            runner,
            files,
            httpHandler,
            new LocalBackendOptions(
                NoneWhenBlank(settings.LocalToolPath),
                NoneWhenBlank(settings.LocalModelPath)),
            new RemoteBackendOptions(
                NoneWhenBlank(settings.RemoteBaseUrl),
                NoneWhenBlank(settings.RemoteApiKey),
                NoneWhenBlank(settings.RemoteModel)));

        var choice = await BackendSelector
            .SelectAsync(settings.Backend, candidates, cancellationToken)
            .ConfigureAwait(false);

        var ready = choice.Outcome == BackendSelectionOutcome.Selected;

        return new ReadinessReport(
            NameOf(settings.Backend, candidates),
            ready,
            ready ? null : choice.Reason,
            ready ? choice.Readiness?.Tool?.Describe() : null);
    }

    /// <summary>
    /// The settings in force spell "none named" as an empty string, and the
    /// backends' own options spell it as null; this is the translation, and it is
    /// the only thing done to a value on its way from one to the other.
    /// </summary>
    private static string? NoneWhenBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// The name the answer carries: the plugin's own spelling where the settings
    /// named a backend it has, what was typed where they did not, and the
    /// do-nothing backend's name where nothing was chosen.
    /// </summary>
    private static string NameOf(string configured, IReadOnlyList<BackendCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return NotConfiguredBackend.BackendName;
        }

        var wanted = configured.Trim();

        return candidates
            .FirstOrDefault(candidate => string.Equals(candidate.Name, wanted, StringComparison.OrdinalIgnoreCase))
            ?.Name ?? wanted;
    }
}
