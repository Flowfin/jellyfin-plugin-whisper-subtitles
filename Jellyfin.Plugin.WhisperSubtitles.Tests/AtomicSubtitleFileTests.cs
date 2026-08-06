using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is what a watcher of the destination directory can see
/// while a subtitle is being written, and what is left behind when writing one
/// goes wrong.
///
/// The directory is a real directory and the files are real files. A double of a
/// file system would prove that this code calls the methods this code calls, and
/// the properties here are about what is on a disk at a given moment, which is
/// the thing a double cannot hold.
/// </summary>
public sealed class AtomicSubtitleFileTests : IDisposable
{
    private static readonly byte[] _subtitle = Encoding.UTF8.GetBytes(
        "1\r\n00:00:01,000 --> 00:00:02,000\r\nA line somebody said.\r\n\r\n");

    private readonly string _destination = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    private string Target => Path.Combine(_destination, "A Film.srt");

    [Fact]
    public async Task The_bytes_arrive_under_the_name_a_reader_opens()
    {
        Directory.CreateDirectory(_destination);

        await AtomicSubtitleFile.WriteAsync(Target, _subtitle, CancellationToken.None);

        Assert.True(File.Exists(Target));
        Assert.Equal(_subtitle, await File.ReadAllBytesAsync(Target));
        Assert.Equal(new[] { Path.GetFileName(Target) }, NamesInDestination());
    }

    [Fact]
    public async Task Nothing_carries_the_final_name_until_every_byte_is_written()
    {
        Directory.CreateDirectory(_destination);
        string[]? namesDuringTheWrite = null;

        await AtomicSubtitleFile.WriteAsync(
            Target,
            async (stream, token) =>
            {
                await stream.WriteAsync(_subtitle.AsMemory(0, 10), token).ConfigureAwait(false);
                namesDuringTheWrite = NamesInDestination();
            },
            CancellationToken.None);

        Assert.NotNull(namesDuringTheWrite);
        Assert.DoesNotContain(Path.GetFileName(Target), namesDuringTheWrite);
        var inFlight = Assert.Single(namesDuringTheWrite);
        Assert.EndsWith(AtomicSubtitleFile.TemporaryExtension, inFlight, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_file_being_written_is_in_the_destination_directory_and_not_in_a_system_temporary_one()
    {
        Directory.CreateDirectory(_destination);
        string? directoryOfTheFileBeingWritten = null;

        await AtomicSubtitleFile.WriteAsync(
            Target,
            (stream, token) =>
            {
                directoryOfTheFileBeingWritten = Path.GetDirectoryName(((FileStream)stream).Name);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // The rename is only one operation where both names are in one directory.
        // Written anywhere else it becomes a copy, which has the window in the
        // middle that writing to another name exists to remove, and it crosses a
        // device boundary on the servers most likely to have one.
        Assert.Equal(_destination, directoryOfTheFileBeingWritten);
    }

    [Fact]
    public async Task A_write_that_fails_partway_leaves_no_subtitle_and_no_leftover()
    {
        Directory.CreateDirectory(_destination);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AtomicSubtitleFile.WriteAsync(
            Target,
            async (stream, token) =>
            {
                await stream.WriteAsync(_subtitle.AsMemory(0, 20), token).ConfigureAwait(false);
                throw new InvalidOperationException("the backend stopped talking partway through the item");
            },
            CancellationToken.None));

        Assert.False(File.Exists(Target), "a subtitle appeared for a write that failed");
        Assert.Empty(NamesInDestination());
    }

    [Fact]
    public async Task A_cancelled_write_leaves_no_subtitle_and_no_leftover()
    {
        Directory.CreateDirectory(_destination);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AtomicSubtitleFile.WriteAsync(
            Target,
            async (stream, token) =>
            {
                await stream.WriteAsync(_subtitle.AsMemory(0, 20), token).ConfigureAwait(false);
                await cancellation.CancelAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            },
            cancellation.Token));

        Assert.False(File.Exists(Target), "a subtitle appeared for a write that was stopped");
        Assert.Empty(NamesInDestination());
    }

    [Fact]
    public async Task A_run_cancelled_after_the_last_byte_still_publishes_nothing()
    {
        Directory.CreateDirectory(_destination);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AtomicSubtitleFile.WriteAsync(
            Target,
            async (stream, token) =>
            {
                await stream.WriteAsync(_subtitle, token).ConfigureAwait(false);
                await cancellation.CancelAsync().ConfigureAwait(false);
            },
            cancellation.Token));

        Assert.False(File.Exists(Target), "a run somebody stopped published its subtitle anyway");
        Assert.Empty(NamesInDestination());
    }

    [Fact]
    public async Task A_subtitle_that_is_already_there_is_not_touched()
    {
        Directory.CreateDirectory(_destination);
        var handWritten = Encoding.UTF8.GetBytes("1\r\n00:00:01,000 --> 00:00:02,000\r\nSomebody typed this.\r\n\r\n");
        await File.WriteAllBytesAsync(Target, handWritten);

        await Assert.ThrowsAsync<IOException>(() =>
            AtomicSubtitleFile.WriteAsync(Target, _subtitle, CancellationToken.None));

        Assert.Equal(handWritten, await File.ReadAllBytesAsync(Target));
        Assert.Equal(new[] { Path.GetFileName(Target) }, NamesInDestination());
    }

    [Fact]
    public void Two_attempts_on_one_item_cannot_pick_the_same_name()
    {
        var first = AtomicSubtitleFile.TemporaryNameFor(Target);
        var second = AtomicSubtitleFile.TemporaryNameFor(Target);

        Assert.NotEqual(first, second);
        Assert.StartsWith(".A Film.srt.", first, StringComparison.Ordinal);
        Assert.EndsWith(AtomicSubtitleFile.TemporaryExtension, first, StringComparison.Ordinal);
        Assert.Equal(first, Path.GetFileName(first));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_destination))
        {
            Directory.Delete(_destination, recursive: true);
        }
    }

    private string[] NamesInDestination() =>
        Directory.GetFiles(_destination).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray()!;
}
