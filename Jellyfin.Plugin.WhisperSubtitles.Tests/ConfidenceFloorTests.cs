using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Detection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The failure this whole rule exists against is a confident wrong language: a
/// file labelled as one language holding another, written into the library and
/// believed by everything downstream afterwards. So the tests here are mostly
/// about the refusals, and about the two ways a refusal quietly turns back into
/// an acceptance.
///
/// The first is a missing score read as certainty. The second is a permission to
/// detect read as an answer. Both are one-character changes in
/// <see cref="LanguageAcceptance"/> and neither shows up in a test that only
/// checks a score above the floor is accepted.
/// </summary>
public class ConfidenceFloorTests
{
    private static readonly IProgress<double> _nowhere = new Progress<double>();

    [Fact]
    public void The_floor_has_a_default_and_the_default_is_the_stated_one()
    {
        Assert.Equal(DetectionOptions.DefaultConfidenceFloor, new DetectionOptions().ConfidenceFloor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void A_score_is_a_floor_an_operator_may_set(double floor)
    {
        Assert.Equal(floor, new DetectionOptions(floor).ConfidenceFloor);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_floor_that_is_not_a_score_is_refused_rather_than_clamped(double floor)
    {
        // Clamping would turn 1.5 into a floor of one, which refuses everything,
        // and -1 into a floor of zero, which accepts everything. Both read as a
        // setting that took effect.
        Assert.Throws<ArgumentOutOfRangeException>(() => new DetectionOptions(floor));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.95)]
    [InlineData(0.8)]
    public void A_detection_at_or_above_the_floor_is_accepted(double score)
    {
        var decision = OnResult(null, Weighing(), "de", score, new DetectionOptions(0.8));

        Assert.Equal(LanguageDecisionOutcome.DetectionAccepted, decision.Outcome);
        Assert.Equal("de", decision.WrittenLanguage);
        Assert.True(decision.MayWrite);
    }

    [Theory]
    [InlineData(0.79)]
    [InlineData(0.41)]
    [InlineData(0)]
    public void A_detection_below_the_floor_writes_nothing(double score)
    {
        var decision = OnResult(null, Weighing(), "pt", score, new DetectionOptions(0.8));

        Assert.Equal(LanguageDecisionOutcome.BelowTheConfidenceFloor, decision.Outcome);
        Assert.Null(decision.WrittenLanguage);
        Assert.False(decision.MayWrite);
    }

    [Fact]
    public void The_refusal_names_the_candidate_and_the_score()
    {
        var decision = OnResult(null, Weighing(), "pt", 0.41, new DetectionOptions(0.8));

        Assert.Equal("pt", decision.Candidate);
        Assert.Equal(0.41, decision.Score);

        // In the sentence too, not only in the fields. What an operator gets is
        // the sentence, and a reason that says a language was not confident
        // enough leaves them with nothing to act on.
        Assert.Contains("pt", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("0.41", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("0.80", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_floor_that_decides_is_the_one_that_was_set()
    {
        var backend = Weighing();

        Assert.Equal(
            LanguageDecisionOutcome.BelowTheConfidenceFloor,
            OnResult(null, backend, "es", 0.6, new DetectionOptions(0.9)).Outcome);

        Assert.Equal(
            LanguageDecisionOutcome.DetectionAccepted,
            OnResult(null, backend, "es", 0.6, new DetectionOptions(0.5)).Outcome);
    }

    [Fact]
    public void A_backend_that_reports_no_confidence_is_refused_before_any_audio_is_extracted()
    {
        var decision = LanguageAcceptance.BeforeTheRun(null, NotWeighing().Description);

        Assert.Equal(LanguageDecisionOutcome.DetectionCannotBeWeighed, decision.Outcome);
        Assert.False(decision.MayWrite);
        Assert.Contains("silent", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("Name one", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_backend_that_reports_no_confidence_is_refused_on_its_result_as_well()
    {
        // Driven through the interface rather than by handing the decision a
        // description built here, so the pair being judged is the one a backend
        // actually ships: the description it publishes and the result it returns.
        var backend = NotWeighing();
        var result = await backend
            .TranscribeAsync(new TranscriptionRequest("audio.wav", null), _nowhere, CancellationToken.None)
            .ConfigureAwait(true);

        var decision = LanguageAcceptance.OnTheResult(null, backend.Description, result, new DetectionOptions());

        Assert.Equal(LanguageDecisionOutcome.DetectionCannotBeWeighed, decision.Outcome);
        Assert.Null(decision.WrittenLanguage);
    }

    [Fact]
    public void A_backend_that_reports_no_confidence_is_used_with_a_named_language()
    {
        // The other half of the decision this issue took. Such a backend is not
        // unusable; it is usable with a language named for the library, and that
        // path has to stay open or the refusal above is just a broken backend.
        var decision = LanguageAcceptance.BeforeTheRun("de", NotWeighing().Description);

        Assert.Equal(LanguageDecisionOutcome.AsRequested, decision.Outcome);
        Assert.Equal("de", decision.WrittenLanguage);
    }

    [Fact]
    public void A_backend_that_offers_a_confidence_and_returns_none_is_refused()
    {
        // The near-miss. A single `?? 1.0` reading a missing score as certainty
        // passes every other test in this file, and it is the exact shape of the
        // failure the floor exists against.
        var decision = OnResult(null, Weighing(), "ja", null, new DetectionOptions());

        Assert.Equal(LanguageDecisionOutcome.DetectionCannotBeWeighed, decision.Outcome);
        Assert.Null(decision.WrittenLanguage);
        Assert.Equal("ja", decision.Candidate);
        Assert.Null(decision.Score);
    }

    [Fact]
    public void Permission_to_detect_is_not_a_language_to_write_under()
    {
        // The second near-miss. `MayWrite` reading true off a decision that only
        // says a backend may be asked would write a subtitle under nothing at
        // all, before the backend has said a word.
        var decision = LanguageAcceptance.BeforeTheRun(null, Weighing().Description);

        Assert.Equal(LanguageDecisionOutcome.DetectionMayProceed, decision.Outcome);
        Assert.Null(decision.WrittenLanguage);
        Assert.False(decision.MayWrite);
    }

    [Fact]
    public void A_named_language_is_not_weighed_against_the_floor()
    {
        // An endpoint that echoes the language it was handed is the common shape,
        // so a named language arriving back with a low score must not be read as
        // a detection. The operator already made this decision.
        var decision = OnResult("en", Weighing(), "en", 0.02, new DetectionOptions(0.8));

        Assert.Equal(LanguageDecisionOutcome.AsRequested, decision.Outcome);
        Assert.Equal("en", decision.WrittenLanguage);
        Assert.Null(decision.Score);
    }

    [Fact]
    public void A_language_named_with_padding_is_still_a_named_language()
    {
        var decision = LanguageAcceptance.BeforeTheRun("  de  ", Weighing().Description);

        Assert.Equal(LanguageDecisionOutcome.AsRequested, decision.Outcome);
        Assert.Equal("de", decision.WrittenLanguage);
    }

    [Fact]
    public void Every_reason_reads_the_same_whatever_the_server_formats_numbers_like()
    {
        // A server running under a culture that writes a decimal comma would put
        // 0,41 in the sentence, and an operator cannot paste that back into a
        // field expecting a score. Asserted by forcing the culture rather than by
        // reading the machine's, so the test says the same thing everywhere.
        var was = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var decision = OnResult(null, Weighing(), "pt", 0.41, new DetectionOptions(0.8));

            Assert.Contains("0.41", decision.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain("0,41", decision.Reason, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    [Fact]
    public void The_backends_in_this_tree_say_what_they_can_weigh()
    {
        // The live case this issue was written about, read off the backend rather
        // than described: the remote one detects and cannot say how sure it is,
        // which is the pair the rule above refuses. Reading a description sends
        // nothing, and the handler below refuses to be used to prove it.
        using var nothing = new NoEndpoint();

        var remote = new RemoteWhisperBackend(nothing, new RemoteBackendOptions(null, null, null)).Description;

        Assert.True(remote.CanDetectLanguage);
        Assert.False(remote.CanReportLanguageConfidence);

        Assert.Equal(
            LanguageDecisionOutcome.DetectionCannotBeWeighed,
            LanguageAcceptance.BeforeTheRun(null, remote).Outcome);

        Assert.False(new NotConfiguredBackend().Description.CanReportLanguageConfidence);
    }

    private static StubBackend Weighing() =>
        new("weighing") { CanDetectLanguage = true, CanReportLanguageConfidence = true };

    private static StubBackend NotWeighing() =>
        new("silent") { CanDetectLanguage = true, CanReportLanguageConfidence = false };

    private static LanguageDecision OnResult(
        string? requested,
        StubBackend backend,
        string language,
        double? score,
        DetectionOptions options) =>
        LanguageAcceptance.OnTheResult(
            requested,
            backend.Description,
            new TranscriptionResult(Array.Empty<TimedSegment>(), language, score),
            options);

    /// <summary>
    /// A handler that refuses to answer, so a test asserting on a description
    /// proves the description was read rather than fetched.
    /// </summary>
    private sealed class NoEndpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Reading what a backend offers sends nothing.");
    }
}
