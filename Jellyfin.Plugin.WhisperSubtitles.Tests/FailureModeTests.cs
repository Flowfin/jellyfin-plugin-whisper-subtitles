using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The ways transcription goes wrong are not one failure. What is asserted here is
/// that each of them stays distinguishable all the way to the operator: its own
/// value, its own sentence, its own retry decision, and no file where there is
/// nothing to write.
/// </summary>
public sealed class FailureModeTests : IDisposable
{
    private static readonly TimeSpan _itemLength = TimeSpan.FromMinutes(97);

    private readonly string _destination = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The modes that mean the audio held nothing to transcribe, which are the two
    /// the issue names as having to produce no file.
    /// </summary>
    public static TheoryData<TranscriptionFailureReason> NothingInTheAudio =>
        new(TranscriptionFailureReason.AudioIsSilent, TranscriptionFailureReason.AudioHasNoSpeech);

    public static TheoryData<TranscriptionFailureReason> EveryMode =>
        new(Enum.GetValues<TranscriptionFailureReason>());

    [Fact]
    public void Every_mode_has_its_own_sentence()
    {
        // Two modes sharing a sentence is the same defect as two modes sharing a
        // value: the vocabulary looks precise in the code and reaches the operator
        // as one message they cannot act on.
        var modes = Enum.GetValues<TranscriptionFailureReason>();
        var sentences = modes.Select(FailureReasonMessages.For).ToList();

        Assert.Equal(modes.Length, sentences.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(sentences, string.IsNullOrWhiteSpace);
    }

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void Every_mode_says_what_happened_and_not_what_to_do(TranscriptionFailureReason reason)
    {
        var sentence = FailureReasonMessages.For(reason);

        // Advice goes stale against the settings it advises about, and a run's
        // report is where somebody decides what to put in front of an operator.
        Assert.DoesNotContain("try ", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("check ", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("should", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.False(sentence.EndsWith('.'), sentence + " ends with a full stop, and these are fragments a report composes");
    }

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void Every_mode_has_a_retry_decision_and_a_quarantine_decision(TranscriptionFailureReason reason)
    {
        // Calling both is the assertion. Neither switch has a fallback arm, so a
        // value with no decision cannot compile, and a value cast in from outside
        // the vocabulary throws here rather than being silently taken as false.
        RetryPolicy.IsRetryable(reason);
        RetryPolicy.CountsTowardsQuarantine(reason);
    }

    [Fact]
    public void A_mode_outside_the_vocabulary_is_refused_rather_than_defaulted()
    {
        // What the missing fallback arm buys at run time as well as at build time.
        // A default of false would give an unknown mode the answer "never retry" and
        // quarantine the item off a decision nobody made.
        var notAMode = (TranscriptionFailureReason)9999;

        Assert.ThrowsAny<Exception>(() => FailureReasonMessages.For(notAMode));
        Assert.ThrowsAny<Exception>(() => RetryPolicy.IsRetryable(notAMode));
    }

    [Theory]
    [MemberData(nameof(NothingInTheAudio))]
    public async Task Nothing_is_written_where_the_audio_held_nothing(TranscriptionFailureReason reason)
    {
        Directory.CreateDirectory(_destination);

        var outcome = TranscriptionOutcome.WritesNothing(reason);

        await PublishIfThereIsAnything(outcome);

        // An empty subtitle track is worse than no track: it looks exactly like the
        // work was done, so the item is skipped by anybody looking for what is left
        // to do, and a viewer selecting it reads nothing.
        Assert.Empty(Directory.GetFiles(_destination));
        Assert.False(outcome.ProducesAFile);

        // And there is nothing to hand a writer even for a caller that did not ask.
        Assert.Throws<InvalidOperationException>(() => outcome.Result());
    }

    [Fact]
    public async Task Something_is_written_where_the_audio_held_speech()
    {
        // Guards the leg above. A publish path that wrote nothing at all would leave
        // the directory empty for every mode and pass it for the wrong reason.
        Directory.CreateDirectory(_destination);

        await PublishIfThereIsAnything(TranscriptionAppraisal.Appraise(Fits(), _itemLength));

        Assert.Single(Directory.GetFiles(_destination));
    }

    [Fact]
    public void An_outcome_that_writes_cannot_be_built_out_of_no_segments()
    {
        // The shape rather than a rule at each call site. A caller cannot construct
        // the writing outcome from an empty result, so an empty file has no path.
        Assert.Throws<ArgumentException>(() =>
            TranscriptionOutcome.Writes(new TranscriptionResult([], "eng")));
    }

    [Fact]
    public void A_backend_that_produced_no_segments_is_its_own_mode()
    {
        var outcome = TranscriptionAppraisal.Appraise(new TranscriptionResult([], "eng"), _itemLength);

        Assert.Equal(TranscriptionFailureReason.NoSegments, outcome.Reason);
        Assert.False(outcome.ProducesAFile);
    }

    [Fact]
    public void A_timing_grid_over_nothing_is_no_segments_rather_than_a_file()
    {
        // What a tool that emitted cues over silence produces. Written out it is a
        // track a viewer selects and reads nothing in.
        var blank = new TranscriptionResult(
            [
                new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(5), "   "),
                new TimedSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), string.Empty)
            ],
            "eng");

        Assert.Equal(TranscriptionFailureReason.NoSegments, TranscriptionAppraisal.Appraise(blank, _itemLength).Reason);
    }

    [Fact]
    public void Segments_that_run_past_the_end_of_the_item_are_refused()
    {
        // The fixture is a transcription of a different, longer file: it starts
        // plausibly and its last segment ends over an hour after this item does.
        var wrongFile = new TranscriptionResult(
            [
                new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(4), "This begins the way the right file would."),
                new TimedSegment(TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(41), "And it keeps going."),
                new TimedSegment(TimeSpan.FromMinutes(150), TimeSpan.FromMinutes(151), "Long after this item has ended.")
            ],
            "eng");

        var outcome = TranscriptionAppraisal.Appraise(wrongFile, _itemLength);

        Assert.Equal(TranscriptionFailureReason.TimingsDoNotFitTheItem, outcome.Reason);
        Assert.False(outcome.ProducesAFile);
    }

    [Fact]
    public void A_stray_segment_out_of_order_is_caught_as_well_as_a_late_last_one()
    {
        // The overrun is judged by the furthest end rather than by the last element,
        // so a transcription whose one bad segment is not at the end does not walk
        // through.
        var stray = new TranscriptionResult(
            [
                new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(4), "Ordinary."),
                new TimedSegment(TimeSpan.FromMinutes(400), TimeSpan.FromMinutes(401), "From somewhere else entirely."),
                new TimedSegment(TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(91), "Ordinary again.")
            ],
            "eng");

        Assert.Equal(
            TranscriptionFailureReason.TimingsDoNotFitTheItem,
            TranscriptionAppraisal.Appraise(stray, _itemLength).Reason);
    }

    [Fact]
    public void A_transcription_ending_a_moment_after_the_item_is_still_written()
    {
        // The tolerance, and why it is not nothing. A library's duration for an item
        // and the length a decoder produces from it differ by a frame or two
        // routinely, and a check with no slack refuses good work on short items.
        var justOver = new TranscriptionResult(
            [new TimedSegment(_itemLength - TimeSpan.FromSeconds(3), _itemLength + TimeSpan.FromSeconds(1), "The last thing said.")],
            "eng");

        Assert.True(TranscriptionAppraisal.Appraise(justOver, _itemLength).ProducesAFile);

        // And the tolerance is a tolerance rather than a hole: a second past it is
        // refused.
        var overTheTolerance = new TranscriptionResult(
            [new TimedSegment(_itemLength, _itemLength + TranscriptionAppraisal.DefaultTolerance + TimeSpan.FromSeconds(1), "Too late.")],
            "eng");

        Assert.Equal(
            TranscriptionFailureReason.TimingsDoNotFitTheItem,
            TranscriptionAppraisal.Appraise(overTheTolerance, _itemLength).Reason);
    }

    [Fact]
    public void The_modes_that_are_facts_about_the_file_are_not_retried()
    {
        // Nothing about tomorrow makes a silent item speak, a concert recording hold
        // narration, or a backend pointed at the wrong file point at the right one.
        Assert.False(RetryPolicy.IsRetryable(TranscriptionFailureReason.AudioIsSilent));
        Assert.False(RetryPolicy.IsRetryable(TranscriptionFailureReason.AudioHasNoSpeech));
        Assert.False(RetryPolicy.IsRetryable(TranscriptionFailureReason.AudioHasSeveralLanguages));
        Assert.False(RetryPolicy.IsRetryable(TranscriptionFailureReason.TimingsDoNotFitTheItem));
        Assert.False(RetryPolicy.IsRetryable(TranscriptionFailureReason.NoSegments));
        Assert.False(RetryPolicy.IsRetryable(TranscriptionFailureReason.DetectionBelowTheFloor));

        // While the modes that are facts about the run still are, which is what
        // keeps the leg above from passing on a policy that refuses everything.
        Assert.True(RetryPolicy.IsRetryable(TranscriptionFailureReason.BackendUnreachable));
        Assert.True(RetryPolicy.IsRetryable(TranscriptionFailureReason.Cancelled));
    }

    [Fact]
    public void A_new_mode_quarantines_on_its_first_failure_and_says_which_one()
    {
        // What a mode that is not worth retrying does to the item's record, through
        // the policy rather than around it.
        var item = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 8, 6, 21, 0, 0, TimeSpan.Zero);

        var record = RetryPolicy.Record(previous: null, item, TranscriptionFailureReason.AudioIsSilent, at);

        Assert.True(record.IsQuarantined);
        Assert.Equal(1, record.Failures);
        Assert.Equal(TranscriptionFailureReason.AudioIsSilent, record.LastReason);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_destination))
        {
            Directory.Delete(_destination, recursive: true);
        }
    }

    private static TranscriptionResult Fits() =>
        new(
            [new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(4), "Something somebody said.")],
            "eng");

    /// <summary>
    /// The whole of what a caller has to do to honour the rule, which is why the
    /// rule is in the shape of the outcome rather than in a comment.
    /// </summary>
    private async Task PublishIfThereIsAnything(TranscriptionOutcome outcome)
    {
        if (!outcome.ProducesAFile)
        {
            return;
        }

        var bytes = new SubRipWriter().Write(outcome.Result().Segments);

        await AtomicSubtitleFile.WriteAsync(
            Path.Combine(_destination, "An Item.srt"),
            bytes,
            CancellationToken.None).ConfigureAwait(false);
    }
}
