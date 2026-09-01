using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The transcription child is asked to be scheduled below the work the machine
/// was bought for, and a platform that refuses costs the item nothing.
/// </summary>
/// <remarks>
/// This is the clause of #22 that needs neither the run nor the definition of
/// busy that issue reserves: what is asserted is the call the backend makes
/// through the injected seam, and that a refusal of it does not fail the item.
///
/// CONTRIBUTING.md refuses "a test that needs elevation to lower a process
/// priority", and names the seam and this failure case as what stands in. This
/// class is one of the two that line names as its replacement, and the line goes
/// on owing #22 the cgroup limit and the log line: the first has nothing in the
/// tree and the second has nowhere to go while this plugin does not log.
///
/// Nothing here starts a process, reads a clock or asks the operating system for
/// anything. Lowering a priority needs no elevation on any platform this runs on
/// and raising one is never asked for, but neither fact is what keeps this suite
/// headless: no real process exists in it to have a priority at all.
/// </remarks>
public class ChildProcessPriorityTests
{
    private const string Tool = "/opt/whisper/whisper-cli";
    private const string Model = "/var/lib/models/ggml-base.bin";
    private const string Audio = "/tmp/extracted audio.wav";
    private const int Threads = 3;

    private static readonly string[] _twoCues =
    {
        "[00:00:00.000 --> 00:00:02.500]   The first thing said.",
        "[00:00:02.500 --> 00:00:05.000]   The second.",
    };

    /// <summary>
    /// Every failure a platform can answer this with, as far as the caller is
    /// concerned.
    /// </summary>
    /// <remarks>
    /// Four types rather than one, because the real implementation throws whatever
    /// the platform throws and this plugin has no list of what that is. A caller
    /// catching one named type would pass a test written against that type and
    /// fail on the first machine that answers with another.
    /// </remarks>
    public static TheoryData<Exception> RefusalsAPlatformCanAnswerWith() => new()
    {
        new PlatformNotSupportedException("this platform does not schedule by priority class"),
        new UnauthorizedAccessException("the account this server runs as may not do that"),
        new InvalidOperationException("the process has already ended"),
        new System.ComponentModel.Win32Exception("access is denied"),
    };

    [Fact]
    public async Task The_transcription_child_is_asked_to_run_below_ordinary_work()
    {
        var process = ScriptedProcess.Printing(_twoCues);

        await Backend(process).TranscribeAsync(Request(), new RecordingProgress(), CancellationToken.None);

        Assert.True(process.LowerPriorityRequested);
    }

    [Fact]
    public async Task The_ask_happens_before_the_tool_has_printed_anything()
    {
        var process = ScriptedProcess.Printing(_twoCues);

        await Backend(process).TranscribeAsync(Request(), new RecordingProgress(), CancellationToken.None);

        // Zero rather than "not the last line". An ask made once the transcript is
        // in has left the run at the ordinary priority for the whole of it, which
        // is the failure this limit exists against rather than a smaller version
        // of it.
        Assert.Equal(0, process.LinesReadWhenPriorityWasAsked);
    }

    [Theory]
    [MemberData(nameof(RefusalsAPlatformCanAnswerWith))]
    public async Task A_platform_that_refuses_the_lower_priority_still_gets_its_transcript(Exception refusal)
    {
        var process = ScriptedProcess.Printing(_twoCues).RefusingToLowerPriority(refusal);
        var progress = new RecordingProgress();

        var result = await Backend(process).TranscribeAsync(Request(), progress, CancellationToken.None);

        // The whole item, not merely the absence of a throw. A run that survived
        // the refusal and lost its segments would satisfy a weaker assertion.
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal("The first thing said.", result.Segments[0].Text);
        Assert.Equal("en", result.Language);
        Assert.Equal(new[] { 1d }, progress.Reported);
        Assert.True(process.Disposed);
        Assert.False(process.KillRequested);
    }

    [Fact]
    public async Task A_cancellation_arriving_through_that_call_is_carried_rather_than_swallowed()
    {
        var process = ScriptedProcess.Printing(_twoCues)
            .RefusingToLowerPriority(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Backend(process).TranscribeAsync(Request(), new RecordingProgress(), CancellationToken.None));

        // The reason the one exception type is let through rather than swallowed
        // with the rest: what carries it is the catch that ends the child, and a
        // swallowed cancellation would leave a transcription running on the
        // operator's machine with nobody reading it.
        Assert.True(process.KillRequested);
        Assert.True(process.Disposed);
    }

    private static LocalBackendOptions Configured() =>
        new(Tool, Model, LocalBackendOptions.DefaultProbeTimeout, Threads);

    private static TranscriptionRequest Request() => new(Audio, "en");

    private static LocalWhisperBackend Backend(ScriptedProcess process) =>
        new(ScriptedProcessRunner.Starting(process), Files(), Configured());

    private static StubFileFacts Files() => StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
}
