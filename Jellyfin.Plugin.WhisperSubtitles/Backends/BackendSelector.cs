using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Turns what the configuration says into the backend that will actually run.
/// </summary>
/// <remarks>
/// Exactly one backend is active and the configuration says which. Everything
/// here is about the case where it says something that cannot be honoured, and
/// the rule for that case is to fall back to the do-nothing backend and say what
/// was wrong.
///
/// It never substitutes a different working backend. An operator who configured a
/// model on their own machine and silently got a remote endpoint instead has had a
/// decision made for them about where their audio goes, and that is not a decision
/// this plugin is entitled to make on their behalf.
///
/// This lives beside the backends because it is the one place allowed to name the
/// do-nothing one.
/// </remarks>
public static class BackendSelector
{
    /// <summary>
    /// Chooses the backend to run.
    /// </summary>
    /// <param name="configuredName">The backend name the configuration holds, which may be absent, blank or wrong.</param>
    /// <param name="candidates">Every backend this plugin has, with the settings each is missing.</param>
    /// <param name="cancellationToken">Stops the readiness check.</param>
    /// <returns>The backend to use and why it is that one.</returns>
    public static async Task<BackendChoice> SelectAsync(
        string? configuredName,
        IReadOnlyList<BackendCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (string.IsNullOrWhiteSpace(configuredName))
        {
            return FallBack(
                BackendSelectionOutcome.NothingConfigured,
                NotConfiguredBackend.Explanation);
        }

        var wanted = configuredName.Trim();

        var candidate = candidates.FirstOrDefault(
            c => string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase));

        if (candidate is null)
        {
            return FallBack(
                BackendSelectionOutcome.UnknownName,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The configured backend \"{0}\" is not one this plugin has. Nothing is transcribed. The backends it has are: {1}.",
                    wanted,
                    candidates.Count == 0 ? "none" : string.Join(", ", candidates.Select(c => c.Name))));
        }

        if (candidate.MissingSettings.Count > 0)
        {
            return FallBack(
                BackendSelectionOutcome.MissingSetting,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The backend \"{0}\" is selected and cannot run without these settings, which are not filled in: {1}. Nothing is transcribed.",
                    candidate.Name,
                    string.Join(", ", candidate.MissingSettings)));
        }

        BackendReadiness readiness;

        try
        {
            readiness = await candidate.Backend.CheckReadinessAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the caller stopping, not the backend failing, so it
            // is not turned into a selection outcome.
            throw;
        }
#pragma warning disable CA1031 // A readiness check is a backend's own code and may throw anything; failing closed is the point.
        catch (Exception thrown)
#pragma warning restore CA1031
        {
            return FallBack(
                BackendSelectionOutcome.NotReady,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The backend \"{0}\" is selected and its readiness check failed: {1}. Nothing is transcribed.",
                    candidate.Name,
                    thrown.Message));
        }

        if (!readiness.IsReady)
        {
            return FallBack(
                BackendSelectionOutcome.NotReady,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The backend \"{0}\" is selected and is not ready: {1} Nothing is transcribed.",
                    candidate.Name,
                    readiness.Reason ?? "it gave no reason."));
        }

        return new BackendChoice(candidate.Backend, BackendSelectionOutcome.Selected, string.Empty);
    }

    private static BackendChoice FallBack(BackendSelectionOutcome outcome, string reason) =>
        new(new NotConfiguredBackend(), outcome, reason);
}
