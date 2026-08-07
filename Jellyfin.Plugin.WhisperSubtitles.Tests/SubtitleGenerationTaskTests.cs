using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using MediaBrowser.Model.Tasks;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is what an operator gets by installing this plugin and
/// doing nothing else. The property worth guarding is negative: no work starts, and
/// running the task by hand changes nothing and says why.
///
/// A dashboard is not booted here. Whether the task appears in the list on a real
/// server is #63's harness, and what these can hold is everything the server reads
/// to put it there.
/// </summary>
public class SubtitleGenerationTaskTests
{
    [Fact]
    public void Nothing_this_task_ships_would_start_work_unattended()
    {
        // The one property an operator cannot check for themselves before installing.
        // A single trigger here is a plugin that begins transcribing a library on
        // somebody's server without being asked.
        var triggers = Task().GetDefaultTriggers();

        Assert.Empty(triggers);
    }

    [Fact]
    public void The_task_carries_the_key_the_server_stores_its_schedule_under()
    {
        // Asserted as a literal rather than compared to the constant, so that
        // changing the key is a change to this test as well. The server keys a
        // task's triggers and history by this string, so a rename loses whatever
        // schedule an operator had set.
        Assert.Equal("WhisperSubtitlesGenerate", Task().Key);
        Assert.Equal("WhisperSubtitlesGenerate", SubtitleGenerationTask.TaskKey);
    }

    [Fact]
    public void An_operator_can_read_what_the_task_is_and_where_it_lives()
    {
        var task = Task();

        Assert.Equal("Library", task.Category);
        Assert.False(string.IsNullOrWhiteSpace(task.Name));
        Assert.False(string.IsNullOrWhiteSpace(task.Description));

        // The description says the heavy part runs outside the server and that
        // nothing runs unasked, because those are the two things somebody reading the
        // task list wants to know before pressing the button.
        Assert.Contains("outside the server", task.Description, StringComparison.Ordinal);
        Assert.Contains("nothing runs until you", task.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_task_is_visible_and_may_be_run()
    {
        var task = Task();

        Assert.False(task.IsHidden);
        Assert.True(task.IsEnabled);
        Assert.True(task.IsLogged);
    }

    [Fact]
    public async Task Running_it_with_nothing_configured_finishes_and_says_nothing_is_configured()
    {
        var task = Task();
        var progress = new RecordingProgress();

        await task.ExecuteAsync(progress, CancellationToken.None);

        Assert.Equal(NotConfiguredBackend.Explanation, task.LastReport);
        Assert.Equal(new[] { 100d }, progress.Reported);
    }

    [Fact]
    public async Task Running_it_with_nothing_configured_transcribes_nothing()
    {
        // The report is not the guarantee. A backend that was asked to transcribe and
        // refused would produce the same sentence, so what this asserts is that no
        // backend was asked at all.
        var watching = new CountingBackend();
        var task = new SubtitleGenerationTask(new[] { new BackendCandidate("Watching", watching, Array.Empty<string>()) });

        await task.ExecuteAsync(new RecordingProgress(), CancellationToken.None);

        Assert.Equal(0, watching.TranscriptionsAsked);
    }

    [Fact]
    public async Task An_operator_stopping_the_task_before_it_starts_is_cancellation()
    {
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Task().ExecuteAsync(new RecordingProgress(), stopped.Token));
    }

    [Fact]
    public void The_task_is_both_of_the_interfaces_a_server_reads()
    {
        // Two interfaces and not one. Without IConfigurableScheduledTask the server
        // has no way to ask whether the task is hidden, enabled or logged, and the
        // answers above would be this plugin's opinion rather than something the
        // dashboard reads.
        var task = Task();

        Assert.IsAssignableFrom<IScheduledTask>(task);
        Assert.IsAssignableFrom<IConfigurableScheduledTask>(task);
    }

    private static SubtitleGenerationTask Task() =>
        new(Array.Empty<BackendCandidate>());

    /// <summary>
    /// A backend that counts what it was asked to do and does none of it.
    /// </summary>
    private sealed class CountingBackend : ITranscriptionBackend
    {
        public int TranscriptionsAsked { get; private set; }

        public BackendDescription Description { get; } = new(
            "Watching",
            Array.Empty<string>(),
            Array.Empty<string>(),
            canDetectLanguage: false,
            cancellationBudget: TimeSpan.Zero);

        public Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(new BackendReadiness(true, null));

        public CostEstimate EstimateCost(TimeSpan mediaDuration) => new(mediaDuration, mediaDuration);

        public Task<TranscriptionResult> TranscribeAsync(
            TranscriptionRequest request,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            TranscriptionsAsked++;

            return System.Threading.Tasks.Task.FromResult(
                new TranscriptionResult(Array.Empty<TimedSegment>(), "en"));
        }
    }
}
