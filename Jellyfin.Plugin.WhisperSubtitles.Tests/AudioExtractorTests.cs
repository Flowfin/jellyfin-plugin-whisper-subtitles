using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the file. An hour of this format is a hundred and
/// fifteen megabytes, so a leak of one file per failed item fills a disk in an
/// evening, and the exit paths that leak are the ones nobody exercises by hand:
/// a tool that failed, and a run somebody stopped.
///
/// No test here starts a program. The runner is the seam every launch goes
/// through, and the doubles write the bytes a media tool would write, so the
/// file being watched is a real file in a real directory and the tool that
/// produced it is not.
/// </summary>
public sealed class AudioExtractorTests : IDisposable
{
    private const string Encoder = "/usr/lib/jellyfin-ffmpeg/ffmpeg";
    private const string Media = "/media/films/A Film.mkv";

    private static readonly AudioStreamDescription _stream = new(1, "eng", 2, isDefault: true);

    private readonly string _workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_clean_run_gives_back_the_file_it_wrote()
    {
        var runner = MediaToolRunner.Writing(4096);

        using var audio = await Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None);

        Assert.True(File.Exists(audio.Path));
        Assert.Equal(4096, audio.SizeInBytes);
        Assert.Equal(_workingDirectory, Path.GetDirectoryName(audio.Path));
    }

    [Fact]
    public async Task The_file_is_gone_once_the_caller_is_done_with_it()
    {
        var runner = MediaToolRunner.Writing(4096);
        string path;

        using (var audio = await Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None))
        {
            path = audio.Path;
            Assert.True(File.Exists(path));
        }

        Assert.False(File.Exists(path), "the extracted audio outlived the caller that asked for it");
    }

    [Fact]
    public async Task Disposing_twice_is_allowed_because_a_caller_unwinding_cannot_know_which_it_is_doing()
    {
        var audio = await Extractor(MediaToolRunner.Writing(4096)).ExtractAsync(Media, _stream, CancellationToken.None);

        audio.Dispose();
        audio.Dispose();

        Assert.False(File.Exists(audio.Path));
    }

    [Fact]
    public async Task A_non_zero_exit_leaves_no_file_behind()
    {
        // The tool decoded part of the stream and gave up. What it wrote is on the
        // disk at the moment it fails, and nothing after this point will be looking
        // for it.
        var runner = MediaToolRunner.WritingThenFailing(4096, exitCode: 1);

        var failure = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.AudioUnreadable, failure.Reason);
        Assert.False(File.Exists(runner.OutputPath), "a failed extraction left its half written file behind");
        Assert.Empty(Directory.GetFiles(_workingDirectory));
    }

    [Fact]
    public async Task A_non_zero_exit_carries_what_the_tool_said()
    {
        var runner = MediaToolRunner.WritingThenFailing(4096, exitCode: 218);

        var failure = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None));

        Assert.Contains("218", failure.Message, StringComparison.Ordinal);
        Assert.Contains(runner.Started!.StandardError, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_run_ends_the_tool_and_leaves_no_file_behind()
    {
        // Two separate properties and both are needed. Ending the tool is what
        // stops an eight hour decode nobody is waiting for; removing the file is
        // what stops the disk filling one stopped run at a time.
        var runner = MediaToolRunner.WritingAndStillRunning(4096);
        using var stopping = new CancellationTokenSource();

        var extraction = Extractor(runner).ExtractAsync(Media, _stream, stopping.Token);

        await WaitUntil(() => runner.OutputPath is not null && File.Exists(runner.OutputPath));

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extraction);

        Assert.True(runner.Started!.KillRequested, "cancellation stopped waiting for the tool without ending it");
        Assert.False(File.Exists(runner.OutputPath), "a cancelled extraction left its half written file behind");
        Assert.Empty(Directory.GetFiles(_workingDirectory));
    }

    [Fact]
    public async Task A_run_cancelled_before_it_starts_launches_nothing()
    {
        var runner = MediaToolRunner.Writing(4096);
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Extractor(runner).ExtractAsync(Media, _stream, stopping.Token));

        Assert.Null(runner.Invocation);
    }

    [Fact]
    public async Task A_media_tool_that_cannot_be_started_is_unreachable_rather_than_unreadable_audio()
    {
        // The two want different actions from an operator. One is a server whose
        // media tool is not where it said, and the other is a file.
        var runner = MediaToolRunner.Refusing(new IOException("no such file"));

        var failure = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendUnreachable, failure.Reason);
        Assert.Contains(Encoder, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_that_reports_success_and_writes_nothing_is_a_failure()
    {
        var failure = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Extractor(MediaToolRunner.WritingNothing()).ExtractAsync(Media, _stream, CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.AudioUnreadable, failure.Reason);
    }

    [Fact]
    public async Task A_file_that_is_a_header_and_no_audio_is_a_failure()
    {
        var runner = MediaToolRunner.Writing(PcmAudio.HeaderBytes);

        var failure = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Extractor(runner).ExtractAsync(Media, _stream, CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.AudioUnreadable, failure.Reason);
        Assert.False(File.Exists(runner.OutputPath));
    }

    [Fact]
    public async Task A_file_that_reached_the_ceiling_is_refused_rather_than_transcribed_as_a_whole_item()
    {
        // The tool was told to stop at the ceiling, so a file that reached it is an
        // item that ran out of room. Transcribing it would produce a subtitle that
        // stops partway through with nothing saying why.
        var runner = MediaToolRunner.Writing(2048);

        var failure = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Extractor(runner, ceilingBytes: 2048).ExtractAsync(Media, _stream, CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.AudioUnreadable, failure.Reason);
        Assert.Contains("2048", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(runner.OutputPath));
    }

    [Fact]
    public async Task The_ceiling_reaches_the_tool_as_well_as_the_check()
    {
        var runner = MediaToolRunner.Writing(1024);

        using var audio = await Extractor(runner, ceilingBytes: 4096)
            .ExtractAsync(Media, _stream, CancellationToken.None);

        Assert.Contains("4096", runner.Invocation!.Arguments);
    }

    [Fact]
    public async Task Two_extractions_of_one_item_never_write_the_same_file()
    {
        // A name per attempt rather than per item. Two runs overlapping, or a run
        // meeting the leftovers of one that died, must not be able to read each
        // other's half written file.
        var extractor = Extractor(MediaToolRunner.Writing(1024));

        using var first = await extractor.ExtractAsync(Media, _stream, CancellationToken.None);
        using var second = await extractor.ExtractAsync(Media, _stream, CancellationToken.None);

        Assert.NotEqual(first.Path, second.Path);
    }

    [Fact]
    public async Task The_working_directory_is_created_rather_than_assumed()
    {
        Assert.False(Directory.Exists(_workingDirectory));

        using var audio = await Extractor(MediaToolRunner.Writing(1024))
            .ExtractAsync(Media, _stream, CancellationToken.None);

        Assert.True(Directory.Exists(_workingDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        // The double writes its bytes inside Start, so this is a handful of yields
        // rather than a wait on anything real. It is bounded so that a change
        // which stopped writing the file fails an assertion instead of hanging the
        // suite, and a hung test says nothing to whoever finds it.
        for (var attempt = 0; attempt < 1000 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        Assert.True(condition(), "the media tool never wrote the file this test is about");
    }

    private AudioExtractor Extractor(MediaToolRunner runner, long ceilingBytes = PcmAudio.DefaultCeilingBytes) =>
        new(runner, Encoder, _workingDirectory, ceilingBytes);
}
