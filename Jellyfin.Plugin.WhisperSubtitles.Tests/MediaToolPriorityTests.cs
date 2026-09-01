using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The media tool that extracts the audio is asked to be scheduled below the work
/// the machine was bought for, and a platform that refuses costs the item nothing.
/// </summary>
/// <remarks>
/// This is the other half of the clause of #22 that needs neither the run nor the
/// definition of busy that issue reserves. <c>ChildProcessPriorityTests</c> holds
/// the transcription child; the decode is the second thing this plugin starts that
/// takes every core it is given, and until this file nothing asked it to step
/// down. What is asserted is the call the extractor makes through the injected
/// seam, and that a refusal of it does not fail the item.
///
/// CONTRIBUTING.md refuses "a test that needs elevation to lower a process
/// priority", and names the seam and this failure case as what stands in. This
/// class is the other of the two that line names as its replacement, and the line
/// goes on owing #22 the cgroup limit and the log line: the first has nothing in
/// the tree and the second has nowhere to go while this plugin does not log.
///
/// Nothing here starts a program or asks the operating system for anything. The
/// file the double writes is a real file in a real directory and the tool that
/// produced it is not, which is the arrangement <c>AudioExtractorTests</c> already
/// works in.
/// </remarks>
public sealed class MediaToolPriorityTests : IDisposable
{
    private const string Encoder = "/usr/lib/jellyfin-ffmpeg/ffmpeg";
    private const string Media = "/media/films/A Film.mkv";

    private static readonly AudioStreamDescription _stream = new(1, "eng", 2, isDefault: true);

    private readonly string _workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Every failure a platform can answer this with, as far as the caller is
    /// concerned.
    /// </summary>
    /// <remarks>
    /// Four types rather than one, for the reason the transcription child's suite
    /// gives: the real implementation throws whatever the platform throws and this
    /// plugin has no list of what that is, so a catch written against one named
    /// type passes a test written against that type and fails on the first machine
    /// that answers with another.
    /// </remarks>
    public static TheoryData<Exception> RefusalsAPlatformCanAnswerWith() => new()
    {
        new PlatformNotSupportedException("this platform does not schedule by priority class"),
        new UnauthorizedAccessException("the account this server runs as may not do that"),
        new InvalidOperationException("the process has already ended"),
        new System.ComponentModel.Win32Exception("access is denied"),
    };

    [Fact]
    public async Task The_media_tool_is_asked_to_run_below_ordinary_work()
    {
        var runner = MediaToolRunner.Writing(4096);

        using var audio = await Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None);

        Assert.NotNull(runner.Started);
        Assert.True(runner.Started.LowerPriorityRequested);
    }

    [Fact]
    public async Task The_ask_happens_before_the_wait_for_the_tool_rather_than_after_it()
    {
        var runner = MediaToolRunner.Writing(4096);

        using var audio = await Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None);

        // False rather than "not null". An ask made once the wait is under way has
        // let the decode run at the ordinary priority for the whole of the item,
        // which is the failure this limit exists against rather than a smaller
        // version of it, and the double reports null when nothing asked at all.
        Assert.Equal(false, runner.Started!.WaitHadBegunWhenPriorityWasAsked);
    }

    [Theory]
    [MemberData(nameof(RefusalsAPlatformCanAnswerWith))]
    public async Task A_platform_that_refuses_the_lower_priority_still_gets_its_audio(Exception refusal)
    {
        var runner = MediaToolRunner.Writing(4096).RefusingToLowerPriority(refusal);

        using var audio = await Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None);

        // The whole extraction, not merely the absence of a throw. A run that
        // survived the refusal and handed back a file it had already deleted would
        // satisfy a weaker assertion.
        Assert.Equal(4096, audio.SizeInBytes);
        Assert.True(File.Exists(audio.Path));
        Assert.True(runner.Started!.Disposed);
        Assert.False(runner.Started.KillRequested);
    }

    [Fact]
    public async Task A_cancellation_arriving_through_that_call_ends_the_tool_rather_than_leaving_it_decoding()
    {
        var runner = MediaToolRunner.Writing(4096)
            .RefusingToLowerPriority(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None));

        // The registration above the ask fires on the token and on nothing else,
        // so an ask that unwound the extraction by itself would leave the tool
        // decoding an item nobody is waiting for any more.
        Assert.True(runner.Started!.KillRequested);
        Assert.True(runner.Started.Disposed);

        // And the half-written file goes with it, which is what every other exit
        // path from this extraction already promises.
        Assert.False(File.Exists(runner.OutputPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    private AudioExtractor Extractor(MediaToolRunner runner) =>
        new(runner, Encoder, _workingDirectory);
}
