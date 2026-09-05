using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A publish that records itself: when the record is written relative to the
/// file taking its name, what the entry carries, and what a record that refuses
/// leaves behind.
/// </summary>
/// <remarks>
/// The property under test is the one #43 decided on 2026-09-05: no file is
/// written that the record does not name. It is held by ordering rather than by
/// cleanup, so the assertion that matters looks at the disk at the moment the
/// record is handed the entry, through a record double that runs a probe then,
/// and finds the file still under its working name.
///
/// A real directory for the same reason <c>AtomicSubtitleFileTests</c> uses one,
/// removed when the class is done.
/// </remarks>
public sealed class RecordedPublishTests : IDisposable
{
    private static readonly byte[] _content = Encoding.UTF8.GetBytes("1\n00:00:01,000 --> 00:00:02,000\nA line.\n");

    private static readonly DateTimeOffset _moment = new(2026, 9, 5, 21, 0, 0, TimeSpan.Zero);

    private readonly Guid _item = Guid.NewGuid();

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-publish-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public RecordedPublishTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task The_entry_is_appended_before_the_file_takes_its_name()
    {
        var destination = Destination();
        var seenAtAppend = (finalNameTaken: true, workingFiles: 0);
        var record = StubPublishedSubtitleRecord.Watching(_ =>
            seenAtAppend = (
                File.Exists(destination),
                Directory.GetFiles(_directory, "*" + AtomicSubtitleFile.TemporaryExtension).Length));

        var publication = await SubtitlePublisher
            .PublishAsync(destination, _content, Recording(record), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.True(publication.WasWritten);
        Assert.False(seenAtAppend.finalNameTaken, "the file carried its final name before the record was handed the entry");
        Assert.Equal(1, seenAtAppend.workingFiles);
        Assert.True(File.Exists(destination));
        Assert.Single(record.Entries);
    }

    [Fact]
    public async Task The_entry_carries_the_bytes_as_written()
    {
        var destination = Destination();
        var record = StubPublishedSubtitleRecord.Empty();

        await SubtitlePublisher.PublishAsync(destination, _content, Recording(record), CancellationToken.None).ConfigureAwait(true);

        var entry = Assert.Single(record.Entries);
        var onDisk = await File.ReadAllBytesAsync(destination).ConfigureAwait(true);

        Assert.Equal(_item, entry.ItemId);
        Assert.Equal(destination, entry.Path);
        Assert.Equal(_moment, entry.WrittenAt);
        Assert.Equal(onDisk.Length, entry.SizeInBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(onDisk)).ToLowerInvariant(), entry.Sha256);
        Assert.Equal(DigestingStream.Sha256Hex(_content), entry.Sha256);
    }

    [Fact]
    public async Task Content_written_in_pieces_is_hashed_as_one()
    {
        // The streaming overload, which is how a marked file is written: header
        // and cues arrive as separate writes and the digest has to be of the
        // whole.
        var destination = Destination();
        var record = StubPublishedSubtitleRecord.Empty();

        await SubtitlePublisher.PublishAsync(
            destination,
            async (stream, token) =>
            {
                await stream.WriteAsync(_content.AsMemory(0, 10), token).ConfigureAwait(false);
                await stream.WriteAsync(_content.AsMemory(10), token).ConfigureAwait(false);
            },
            Recording(record),
            CancellationToken.None).ConfigureAwait(true);

        var entry = Assert.Single(record.Entries);

        Assert.Equal(_content.Length, entry.SizeInBytes);
        Assert.Equal(DigestingStream.Sha256Hex(_content), entry.Sha256);
    }

    [Fact]
    public async Task A_record_that_refuses_the_entry_leaves_no_file_under_the_final_name()
    {
        // The direction the ordering exists for. A full disk under the record is a
        // publish that never happened, and the working file goes with it.
        var destination = Destination();
        var record = StubPublishedSubtitleRecord.Refusing(new IOException("no space left for the record"));

        await Assert.ThrowsAsync<IOException>(
            () => SubtitlePublisher.PublishAsync(destination, _content, Recording(record), CancellationToken.None)).ConfigureAwait(true);

        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(_directory, "*" + AtomicSubtitleFile.TemporaryExtension));
        Assert.Empty(record.Entries);
    }

    [Fact]
    public async Task A_skip_because_something_is_already_there_records_nothing()
    {
        var destination = Destination();
        await File.WriteAllTextAsync(destination, "somebody's own subtitle").ConfigureAwait(true);
        var record = StubPublishedSubtitleRecord.Empty();

        var publication = await SubtitlePublisher
            .PublishAsync(destination, _content, Recording(record), CancellationToken.None)
            .ConfigureAwait(true);

        Assert.False(publication.WasWritten);
        Assert.Empty(record.Entries);
        Assert.Equal("somebody's own subtitle", await File.ReadAllTextAsync(destination).ConfigureAwait(true));
    }

    [Fact]
    public async Task A_publish_without_a_recording_writes_the_file_and_records_nothing()
    {
        // The overloads the rest of the suite drives the write with. What they hold
        // is unchanged; what a run does is publish with a recording, and the run is
        // #183.
        var destination = Destination();

        var publication = await SubtitlePublisher.PublishAsync(destination, _content, CancellationToken.None).ConfigureAwait(true);

        Assert.True(publication.WasWritten);
        Assert.Equal(_content, await File.ReadAllBytesAsync(destination).ConfigureAwait(true));
    }

    [Fact]
    public void The_recording_needs_a_record()
    {
        Assert.Throws<ArgumentNullException>(() => new SubtitleRecording(null!, _item, _moment));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SubtitleRecording Recording(IPublishedSubtitleRecord record) => new(record, _item, _moment);

    private string Destination() =>
        Path.Combine(_directory, "Arrival (2016).en.Transcribed.srt");
}
