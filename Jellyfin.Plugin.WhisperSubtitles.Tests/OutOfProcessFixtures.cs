using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

// The fixture set for the out-of-process census: the breach, the do-nothing
// neighbour that must stay accepted, and one backend per seam the limits page
// names. What the census reads is what a backend has to be HANDED, so the four
// only mean anything beside each other and are kept in one file for the reason
// BackendFixtures.cs gives about its own three.
//
// They sit under the fixture namespace because that is where the suite's
// one-backend rule allows a hand-rolled implementation to live: they exist to be
// classified rather than to be transcribed against, which is the same exemption
// StubBackendTests names.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.OutOfProcess;

/// <summary>
/// A backend handed nothing that transcribes anyway, which is the breach.
/// </summary>
internal sealed class BackendThatWorksInThisProcess : ITranscriptionBackend
{
    public BackendDescription Description { get; } = new(
        "in the server process",
        Array.Empty<string>(),
        Array.Empty<string>(),
        canDetectLanguage: false,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.Zero);

    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BackendReadiness(true, null));

    public CostEstimate EstimateCost(TimeSpan mediaDuration) => new(TimeSpan.Zero, TimeSpan.Zero);

    public Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, IProgress<double> progress, CancellationToken cancellationToken) =>
        Task.FromResult(new TranscriptionResult(Array.Empty<TimedSegment>(), "en"));
}

/// <summary>
/// A backend handed nothing that transcribes nothing, which is not.
/// </summary>
/// <remarks>
/// The shape the plugin's own do-nothing backend has. Without it beside the one
/// above, a census refusing everything handed no seam would look right.
/// </remarks>
internal sealed class BackendThatTranscribesNothing : ITranscriptionBackend
{
    public BackendDescription Description { get; } = new(
        "nothing",
        Array.Empty<string>(),
        Array.Empty<string>(),
        canDetectLanguage: false,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.Zero);

    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BackendReadiness(false, "nothing is configured"));

    public CostEstimate EstimateCost(TimeSpan mediaDuration) => new(TimeSpan.Zero, TimeSpan.Zero);

    public Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, IProgress<double> progress, CancellationToken cancellationToken) =>
        throw new BackendNotConfiguredException("nothing is configured");
}

/// <summary>
/// A backend handed the seam that reaches a child process on the same machine.
/// </summary>
internal sealed class BackendHandedAProcessRunner : ITranscriptionBackend
{
    public BackendHandedAProcessRunner(IProcessRunner runner)
    {
        Runner = runner;
    }

    public IProcessRunner Runner { get; }

    public BackendDescription Description { get; } = new(
        "a child process",
        Array.Empty<string>(),
        Array.Empty<string>(),
        canDetectLanguage: false,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.Zero);

    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BackendReadiness(true, null));

    public CostEstimate EstimateCost(TimeSpan mediaDuration) => new(TimeSpan.Zero, TimeSpan.Zero);

    public Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, IProgress<double> progress, CancellationToken cancellationToken) =>
        Task.FromResult(new TranscriptionResult(Array.Empty<TimedSegment>(), "en"));
}

/// <summary>
/// A backend handed the seam that reaches a remote endpoint.
/// </summary>
/// <remarks>
/// It transcribes rather than refusing, on purpose. Both seam-holding fixtures do,
/// so a census that classified by what a backend ANSWERS rather than by what it
/// holds would put them where the breach is and be caught.
/// </remarks>
internal sealed class BackendHandedAMessageHandler : ITranscriptionBackend
{
    public BackendHandedAMessageHandler(HttpMessageHandler handler)
    {
        Handler = handler;
    }

    public HttpMessageHandler Handler { get; }

    public BackendDescription Description { get; } = new(
        "a remote endpoint",
        Array.Empty<string>(),
        Array.Empty<string>(),
        canDetectLanguage: false,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.Zero);

    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BackendReadiness(true, null));

    public CostEstimate EstimateCost(TimeSpan mediaDuration) => new(TimeSpan.Zero, TimeSpan.Zero);

    public Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, IProgress<double> progress, CancellationToken cancellationToken) =>
        Task.FromResult(new TranscriptionResult(Array.Empty<TimedSegment>(), "en"));
}
