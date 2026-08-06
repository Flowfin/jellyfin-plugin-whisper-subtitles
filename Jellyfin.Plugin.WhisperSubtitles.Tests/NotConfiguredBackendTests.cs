using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The default a fresh install runs with. What is asserted here is mostly that it
/// says so: a plugin that is switched off and silent about it is the failure this
/// backend exists to prevent, and silence is not something the type system
/// refuses on its own.
/// </summary>
public class NotConfiguredBackendTests
{
    [Fact]
    public async Task It_reports_itself_as_not_ready_and_says_why()
    {
        var readiness = await new NotConfiguredBackend().CheckReadinessAsync(CancellationToken.None);

        Assert.False(readiness.IsReady);
        Assert.Equal(NotConfiguredBackend.Explanation, readiness.Reason);
    }

    [Fact]
    public async Task Asking_it_to_transcribe_is_refused_as_not_configured_rather_than_as_a_failure()
    {
        // The type is the assertion. A caller has to tell "nobody has set this up",
        // which wants a sentence pointing at the configuration page, from "it was
        // set up and it broke", which wants an error; a message cannot be read for
        // that and ThrowsAsync matches the exact type rather than a base of it.
        var thrown = await Assert.ThrowsAsync<BackendNotConfiguredException>(
            () => new NotConfiguredBackend().TranscribeAsync(
                new TranscriptionRequest("audio.wav", "en"),
                new Progress<double>(),
                CancellationToken.None));

        Assert.Equal(NotConfiguredBackend.Explanation, thrown.Message);
    }

    [Fact]
    public async Task It_never_answers_a_transcription_with_an_empty_success()
    {
        // Stated separately from the exception type because this is the outcome
        // that would be silent: an empty result is a successful result to every
        // caller, so it would be written out, marked as generated and recorded as
        // done, for every item in the library.
        await Assert.ThrowsAnyAsync<Exception>(
            () => new NotConfiguredBackend().TranscribeAsync(
                new TranscriptionRequest("audio.wav", null),
                new Progress<double>(),
                CancellationToken.None));
    }

    [Fact]
    public void It_offers_no_model_and_no_language()
    {
        // An administrator surface reads this to fill a dropdown. A plausible list
        // from a backend that transcribes nothing is worse than an empty one.
        var description = new NotConfiguredBackend().Description;

        Assert.Empty(description.Models);
        Assert.Empty(description.Languages);
        Assert.False(description.CanDetectLanguage);
    }

    [Fact]
    public void It_costs_nothing_for_any_duration()
    {
        var estimate = new NotConfiguredBackend().EstimateCost(TimeSpan.FromHours(3));

        Assert.Equal(TimeSpan.Zero, estimate.Shortest);
        Assert.Equal(TimeSpan.Zero, estimate.Longest);
    }

    [Fact]
    public void The_explanation_names_what_would_change_it()
    {
        // A reason that only says "not configured" leaves an operator with nowhere
        // to go, and this sentence is the one that reaches the log, the readiness
        // answer and the exception.
        Assert.Contains("configuration page", NotConfiguredBackend.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_readiness_check_stops()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new NotConfiguredBackend().CheckReadinessAsync(cancelled.Token));
    }
}
