using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The local backend runs a program the operator chose and reads what it prints.
/// Every way that can go wrong has to end as a named reason rather than as an
/// exception nobody expected, and cancellation has to end the program rather than
/// stop waiting for it.
/// </summary>
public class LocalWhisperBackendTests
{
    private const string Tool = "/opt/whisper/whisper-cli";
    private const string Model = "/var/lib/models/ggml-base.bin";
    private const string Audio = "/tmp/extracted audio.wav";

    private static readonly string[] _threeCues =
    {
        "[00:00:00.000 --> 00:00:02.500]   The first thing said.",
        "[00:00:02.500 --> 00:00:05.000]   The second.",
        "[00:00:05.000 --> 00:00:07.250]   And the third.",
    };

    [Fact]
    public async Task A_clean_run_gives_back_every_segment_the_tool_printed()
    {
        var process = ScriptedProcess.Printing(_threeCues);
        var progress = new RecordingProgress();

        var result = await Backend(process).TranscribeAsync(Request("en"), progress, CancellationToken.None);

        Assert.Equal(3, result.Segments.Count);
        Assert.Equal("The first thing said.", result.Segments[0].Text);
        Assert.Equal(TimeSpan.FromMilliseconds(7250), result.Segments[2].End);
        Assert.Equal("en", result.Language);

        Assert.Equal(new[] { 1d }, progress.Reported);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task The_tool_is_handed_an_argument_vector_and_never_a_command_line()
    {
        // The audio path has a space in it on purpose. Anything that built one
        // string out of these would either break here or, worse, work here and let
        // a different path decide what runs.
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing(_threeCues));

        await new LocalWhisperBackend(runner, Configured()).TranscribeAsync(
            Request("de"),
            new RecordingProgress(),
            CancellationToken.None);

        var invocation = Assert.IsType<ProcessInvocation>(runner.Invocation);

        Assert.Equal(Tool, invocation.ExecutablePath);
        Assert.Equal(new[] { "-m", Model, "-l", "de", "-f", Audio }, invocation.Arguments);
        Assert.Contains(Audio, invocation.Arguments);
    }

    [Fact]
    public async Task A_non_zero_exit_is_the_backend_failing_and_carries_what_the_tool_said()
    {
        var process = ScriptedProcess.Printing(
            Array.Empty<string>(),
            exitCode: 3,
            standardError: "error: failed to load model");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Backend(process).TranscribeAsync(Request("en"), new RecordingProgress(), CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendFailed, failed.Reason);
        Assert.Contains("3", failed.Message, StringComparison.Ordinal);
        Assert.Contains("failed to load model", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Output_that_stops_halfway_is_a_parse_failure_and_the_tool_is_ended()
    {
        // A tool killed by the machine mid-sentence leaves a line with no closing
        // bracket. Reading the segments before it as a finished transcription would
        // write a subtitle that stops early and looks complete.
        var lines = new List<string>(_threeCues) { "[00:00:07.250 --> 00:00:09" };
        var process = ScriptedProcess.Printing(lines);

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Backend(process).TranscribeAsync(Request("en"), new RecordingProgress(), CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.True(process.KillRequested, "the tool is still running and nothing is going to read it");
    }

    [Fact]
    public async Task A_timestamp_the_plugin_cannot_read_is_a_parse_failure_rather_than_a_cue()
    {
        var process = ScriptedProcess.Printing(new[] { "[00:00:0X.000 --> 00:00:02.000]   nonsense" });

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Backend(process).TranscribeAsync(Request("en"), new RecordingProgress(), CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.Contains("start time", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_ends_the_tool_rather_than_stopping_the_wait_for_it()
    {
        // The assertion that matters is KillRequested. A backend that observed the
        // token and returned would satisfy every other line here, and would leave a
        // transcription running on the operator's machine with nobody reading it.
        var process = ScriptedProcess.StillRunningAfter(_threeCues, exitCode: 130);
        using var stopping = new CancellationTokenSource();

        var transcribing = Backend(process).TranscribeAsync(Request("en"), new RecordingProgress(), stopping.Token);

        await process.ReachedTheEndOfItsOutput.ConfigureAwait(true);
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transcribing);

        Assert.True(process.KillRequested, "the token was observed but the tool was never asked to stop");
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task A_cancelled_run_reports_nothing_finished()
    {
        // The exit code a killed tool leaves is not zero, and reporting that as the
        // tool having failed would tell an operator who pressed stop that something
        // went wrong.
        var process = ScriptedProcess.StillRunningAfter(_threeCues, exitCode: 130);
        var progress = new RecordingProgress();
        using var stopping = new CancellationTokenSource();

        var transcribing = Backend(process).TranscribeAsync(Request("en"), progress, stopping.Token);

        await process.ReachedTheEndOfItsOutput.ConfigureAwait(true);
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transcribing);

        Assert.Empty(progress.Reported);
    }

    [Fact]
    public async Task A_tool_that_cannot_be_started_is_unreachable_rather_than_failed()
    {
        // Not the same state as a tool that ran and failed. One is a path an
        // operator can correct on the page; the other is worth retrying.
        var runner = ScriptedProcessRunner.Refusing(new FileNotFoundException("no such file"));

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => new LocalWhisperBackend(runner, Configured()).TranscribeAsync(
                Request("en"),
                new RecordingProgress(),
                CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendUnreachable, failed.Reason);
        Assert.IsType<FileNotFoundException>(failed.InnerException);
    }

    [Fact]
    public async Task A_backend_with_no_paths_refuses_before_it_starts_anything()
    {
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing(_threeCues));

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => new LocalWhisperBackend(runner, new LocalBackendOptions(null, null)).TranscribeAsync(
                Request("en"),
                new RecordingProgress(),
                CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendNotReady, failed.Reason);
        Assert.Null(runner.Invocation);
    }

    [Fact]
    public async Task A_request_that_names_no_language_is_refused_rather_than_guessed()
    {
        // The backend says it cannot detect a language. Running anyway and reporting
        // whatever came back would put a language on the subtitle that nobody
        // measured.
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing(_threeCues));
        var backend = new LocalWhisperBackend(runner, Configured());

        Assert.False(backend.Description.CanDetectLanguage);

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => backend.TranscribeAsync(Request(null), new RecordingProgress(), CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendNotReady, failed.Reason);
        Assert.Null(runner.Invocation);
    }

    [Fact]
    public async Task Readiness_answers_on_the_settings_and_says_it_looked_no_further()
    {
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing(_threeCues));

        var unset = await new LocalWhisperBackend(runner, new LocalBackendOptions(" ", Model))
            .CheckReadinessAsync(CancellationToken.None);

        Assert.False(unset.IsReady);
        Assert.Contains("configuration page", unset.Reason, StringComparison.Ordinal);

        var set = await new LocalWhisperBackend(runner, Configured()).CheckReadinessAsync(CancellationToken.None);

        Assert.True(set.IsReady);
        Assert.Null(set.Reason);
        Assert.Null(runner.Invocation);
    }

    [Fact]
    public void The_cost_hint_never_shrinks_as_the_media_grows()
    {
        // The one property a caller may rely on while the numbers themselves are a
        // placeholder that #38 replaces with a measurement.
        var backend = new LocalWhisperBackend(
            ScriptedProcessRunner.Starting(ScriptedProcess.Printing(_threeCues)),
            Configured());

        var shorter = backend.EstimateCost(TimeSpan.FromMinutes(20));
        var longer = backend.EstimateCost(TimeSpan.FromMinutes(90));

        Assert.True(longer.Shortest >= shorter.Shortest);
        Assert.True(longer.Longest >= shorter.Longest);
        Assert.True(longer.Longest >= longer.Shortest);
    }

    private static LocalBackendOptions Configured() => new(Tool, Model);

    private static TranscriptionRequest Request(string? language) => new(Audio, language);

    private static LocalWhisperBackend Backend(ScriptedProcess process) =>
        new(ScriptedProcessRunner.Starting(process), Configured());
}
