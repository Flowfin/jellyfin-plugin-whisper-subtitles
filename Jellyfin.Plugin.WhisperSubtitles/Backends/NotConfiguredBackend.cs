using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// The backend a fresh install has: it transcribes nothing, and it says why.
/// </summary>
/// <remarks>
/// The server must never depend on a model being present, and that is only true
/// if what ships by default needs none. This is that default. It is also where
/// selection lands when a configured backend turns out to be unusable, so the
/// plugin has one way of being switched off rather than two.
///
/// The half that matters is the sentence. An operator whose library is not being
/// transcribed has to be able to find out that nothing is configured and what
/// would change that, and a backend that quietly returned an empty transcription
/// would be the worst version of this: every item processed, every item marked
/// done, nothing produced, nothing said.
/// </remarks>
public sealed class NotConfiguredBackend : ITranscriptionBackend
{
    /// <summary>
    /// What an operator is told, wherever this backend is the reason nothing
    /// happened.
    /// </summary>
    /// <remarks>
    /// One sentence in one place, so the log line, the readiness answer and the
    /// exception cannot drift into saying three different things about one state.
    /// </remarks>
    public const string Explanation =
        "No transcription backend is configured, so nothing is transcribed. Choose a backend on the plugin's configuration page.";

    /// <summary>
    /// The name this backend reports.
    /// </summary>
    public const string BackendName = "None";

    /// <inheritdoc />
    /// <remarks>
    /// It offers no model and no language, which is the honest answer and not an
    /// oversight: an administrator surface listing this backend's languages must
    /// show an empty list rather than a plausible one.
    /// </remarks>
    public BackendDescription Description { get; } = new(
        BackendName,
        Array.Empty<string>(),
        Array.Empty<string>(),
        canDetectLanguage: false,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.Zero);

    /// <inheritdoc />
    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new BackendReadiness(false, Explanation));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Zero rather than a refusal. A dry run asking what a library would cost with
    /// nothing configured is asking a reasonable question, and the answer is that
    /// it would cost nothing because nothing would run.
    /// </remarks>
    public CostEstimate EstimateCost(TimeSpan mediaDuration) => new(TimeSpan.Zero, TimeSpan.Zero);

    /// <inheritdoc />
    /// <remarks>
    /// Refuses rather than returning an empty result. An empty transcription is a
    /// successful transcription as far as every caller is concerned, and it would
    /// be written, marked and recorded as one.
    /// </remarks>
    public Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        throw new BackendNotConfiguredException(Explanation);
    }
}
