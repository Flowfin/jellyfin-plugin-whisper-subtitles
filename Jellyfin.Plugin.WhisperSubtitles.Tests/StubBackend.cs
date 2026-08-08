using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend every test transcribes against.
/// </summary>
/// <remarks>
/// No test downloads a model, launches an inference binary or reaches a
/// transcription service, and the interface exists so the expensive half can be
/// replaced by something deterministic. This is that replacement, and there is
/// one of it: a fake hand-rolled inside a test file is how two tests come to
/// disagree about what a backend does, and the disagreement is invisible because
/// each file is right about its own fake.
///
/// Everything this can be told to do is a property set at construction or after
/// it. Nothing here reads a path, an address, a clock or an environment: being
/// slow is a gate the test opens, not a duration the test waits out, so a run is
/// the same length whatever the machine is doing and a hung test fails as a hang
/// rather than as a flake.
/// </remarks>
internal sealed class StubBackend : ITranscriptionBackend
{
    private readonly List<string?> _languagesAsked = new();
    private readonly List<string> _audioPathsAsked = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StubBackend"/> class, ready,
    /// returning one segment and nothing else.
    /// </summary>
    /// <param name="name">The name selection and reporting will know it by.</param>
    public StubBackend(string name = "stub")
    {
        Name = name;
    }

    /// <summary>
    /// Gets the name this backend answers to.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the readiness check says yes.
    /// </summary>
    public bool IsReady { get; set; } = true;

    /// <summary>
    /// Gets or sets what the readiness check gives as the reason it said no.
    /// </summary>
    public string? NotReadyReason { get; set; }

    /// <summary>
    /// Gets or sets the exception the readiness check throws instead of answering.
    /// </summary>
    /// <remarks>
    /// A backend that throws out of its readiness check is a different case from
    /// one that answers no, and callers have to survive both.
    /// </remarks>
    public Exception? ReadinessThrows { get; set; }

    /// <summary>
    /// Gets or sets the segments a transcription returns.
    /// </summary>
    public IReadOnlyList<TimedSegment> Segments { get; set; } =
        new[] { new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "stub") };

    /// <summary>
    /// Gets or sets the language a transcription reports having found.
    /// </summary>
    public string DetectedLanguage { get; set; } = "en";

    /// <summary>
    /// Gets or sets the reason a transcription fails with, or null to succeed.
    /// </summary>
    public TranscriptionFailureReason? FailsWith { get; set; }

    /// <summary>
    /// Gets or sets the fractions a transcription reports, in order, before it ends.
    /// </summary>
    public IReadOnlyList<double> ProgressPattern { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets a value indicating whether a transcription stops when the token
    /// is cancelled.
    /// </summary>
    /// <remarks>
    /// False is the backend the cancellation rules exist for: one that keeps going
    /// after it was told to stop. It is a knob rather than a second class because
    /// a caller cannot tell the two apart by type, only by what happens.
    /// </remarks>
    public bool ObservesCancellation { get; set; } = true;

    /// <summary>
    /// Gets or sets the gate a transcription waits at before it finishes.
    /// </summary>
    /// <remarks>
    /// This is what slow means here. The test holds the transcription open for
    /// exactly as long as it needs to observe something else, then releases it,
    /// and no part of that depends on how fast the machine is.
    /// </remarks>
    public TaskCompletionSource? HeldUntilReleased { get; set; }

    /// <summary>
    /// Gets or sets the range the cost estimate answers with.
    /// </summary>
    public CostEstimate Estimate { get; set; } = new(TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>
    /// Gets or sets how long this backend's description promises it takes to stop.
    /// </summary>
    public TimeSpan CancellationBudget { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets a value indicating whether the description claims language detection.
    /// </summary>
    public bool CanDetectLanguage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the description claims a confidence
    /// with a detected language.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CanDetectLanguage"/> and from
    /// <see cref="DetectedConfidence"/>, so a test can set up a backend whose
    /// description and result disagree. That combination is a defect in a backend
    /// rather than a configuration, and it is the one a caller is most likely to
    /// read as certainty.
    /// </remarks>
    public bool CanReportLanguageConfidence { get; set; }

    /// <summary>
    /// Gets or sets the confidence a transcription reports for the language it
    /// found, or null to report none.
    /// </summary>
    public double? DetectedConfidence { get; set; }

    /// <summary>
    /// Gets how many times the readiness check was asked.
    /// </summary>
    public int ReadinessChecks { get; private set; }

    /// <summary>
    /// Gets how many transcriptions were asked for.
    /// </summary>
    public int TranscriptionsAsked { get; private set; }

    /// <summary>
    /// Gets the language each transcription was asked for, in order.
    /// </summary>
    public IReadOnlyList<string?> LanguagesAsked => _languagesAsked;

    /// <summary>
    /// Gets the audio path each transcription was asked for, in order.
    /// </summary>
    public IReadOnlyList<string> AudioPathsAsked => _audioPathsAsked;

    /// <inheritdoc />
    public BackendDescription Description => new(
        Name,
        new[] { "stub-model" },
        new[] { "en" },
        CanDetectLanguage,
        CanReportLanguageConfidence,
        CancellationBudget);

    /// <inheritdoc />
    public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ReadinessChecks++;

        if (ReadinessThrows is not null)
        {
            throw ReadinessThrows;
        }

        return Task.FromResult(new BackendReadiness(IsReady, IsReady ? null : NotReadyReason));
    }

    /// <inheritdoc />
    public CostEstimate EstimateCost(TimeSpan mediaDuration) => Estimate;

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        TranscriptionsAsked++;
        _languagesAsked.Add(request.Language);
        _audioPathsAsked.Add(request.AudioFilePath);

        foreach (var fraction in ProgressPattern)
        {
            progress.Report(fraction);
        }

        if (HeldUntilReleased is not null)
        {
            await HeldUntilReleased.Task.ConfigureAwait(false);
        }

        if (ObservesCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (FailsWith is TranscriptionFailureReason reason)
        {
            throw new TranscriptionFailedException(reason, $"The stub was told to fail with {reason}.");
        }

        return new TranscriptionResult(Segments, DetectedLanguage, DetectedConfidence);
    }
}
