using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The record of what this plugin published, as the file it is kept in: what an
/// append writes, what a read gives back, and what a line a person broke does to
/// the rest.
/// </summary>
/// <remarks>
/// A real directory, because the subject is a file and its bytes: one line per
/// entry, appended, read back in order. The directory is made under the system
/// temporary one and removed when the class is done, which
/// <c>DeterminismTests</c> holds this suite to.
///
/// The hostile case <c>docs/untrusted-input.md</c> names this class for is the
/// line somebody edited: the file is plugin data a person can open, and a line
/// that will not parse is counted and skipped rather than repaired or taken as a
/// reason to read nothing.
/// </remarks>
public sealed class PublishedSubtitleRecordTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-record-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private static readonly DateTimeOffset _moment = new(2026, 9, 5, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_record_nothing_has_written_to_reads_as_empty()
    {
        var record = new PublishedSubtitleRecordFile(_directory);

        var reading = await record.ReadAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(reading.Entries);
        Assert.Equal(0, reading.UnreadableLines);
        Assert.False(Directory.Exists(_directory), "reading created the directory, which only an append should do");
    }

    [Fact]
    public async Task Entries_come_back_in_the_order_they_were_appended_with_every_field()
    {
        var record = new PublishedSubtitleRecordFile(_directory);
        var first = Entry("/media/Films/Arrival (2016)/Arrival (2016).en.Transcribed.srt", 1234);
        var second = Entry("/media/Films/Heat (1995)/Heat (1995).en.Transcribed.srt", 5678);

        await record.AppendAsync(first, CancellationToken.None).ConfigureAwait(true);
        await record.AppendAsync(second, CancellationToken.None).ConfigureAwait(true);

        var reading = await record.ReadAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(2, reading.Entries.Count);
        Assert.Equal(0, reading.UnreadableLines);

        Assert.Equal(first.ItemId, reading.Entries[0].ItemId);
        Assert.Equal(first.Path, reading.Entries[0].Path);
        Assert.Equal(first.SizeInBytes, reading.Entries[0].SizeInBytes);
        Assert.Equal(first.Sha256, reading.Entries[0].Sha256);
        Assert.Equal(first.WrittenAt, reading.Entries[0].WrittenAt);
        Assert.Equal(second.Path, reading.Entries[1].Path);
    }

    [Fact]
    public async Task The_first_append_creates_the_directory_and_writes_one_line_per_entry()
    {
        var record = new PublishedSubtitleRecordFile(_directory);

        await record.AppendAsync(Entry("/media/a.srt", 1), CancellationToken.None).ConfigureAwait(true);
        await record.AppendAsync(Entry("/media/b.srt", 2), CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(Path.Combine(_directory, PublishedSubtitleRecordFile.FileName), record.FilePath);
        Assert.True(File.Exists(record.FilePath));
        Assert.Equal(2, (await File.ReadAllLinesAsync(record.FilePath).ConfigureAwait(true)).Length);
    }

    [Fact]
    public async Task A_line_a_person_broke_is_counted_and_the_lines_around_it_are_read()
    {
        // The hostile case: the file opened in an editor and one line mangled. The
        // reader neither repairs it nor stops at it.
        var record = new PublishedSubtitleRecordFile(_directory);
        await record.AppendAsync(Entry("/media/a.srt", 1), CancellationToken.None).ConfigureAwait(true);
        await File.AppendAllTextAsync(record.FilePath, "{ this is not an entry\n").ConfigureAwait(true);
        await File.AppendAllTextAsync(record.FilePath, "\n").ConfigureAwait(true);
        await record.AppendAsync(Entry("/media/b.srt", 2), CancellationToken.None).ConfigureAwait(true);

        var reading = await record.ReadAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(new[] { "/media/a.srt", "/media/b.srt" }, reading.Entries.Select(entry => entry.Path));
        Assert.Equal(1, reading.UnreadableLines);
    }

    [Fact]
    public async Task A_line_missing_a_field_is_unreadable_rather_than_an_entry_with_a_default()
    {
        // A default here would be a path with no hash, which the listing would then
        // compare against nothing and could offer for removal.
        var record = new PublishedSubtitleRecordFile(_directory);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            record.FilePath,
            "{\"ItemId\":\"" + Guid.NewGuid() + "\",\"Path\":\"/media/a.srt\",\"SizeInBytes\":3,\"WrittenAt\":\"2026-09-05T21:00:00+00:00\"}\n").ConfigureAwait(true);

        var reading = await record.ReadAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(reading.Entries);
        Assert.Equal(1, reading.UnreadableLines);
    }

    [Fact]
    public void The_directory_and_the_entry_are_required()
    {
        Assert.Throws<ArgumentException>(() => new PublishedSubtitleRecordFile(" "));
        Assert.Throws<ArgumentException>(() => new PublishedSubtitle(Guid.NewGuid(), " ", 1, "ab", _moment));
        Assert.Throws<ArgumentException>(() => new PublishedSubtitle(Guid.NewGuid(), "/media/a.srt", 1, " ", _moment));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PublishedSubtitle(Guid.NewGuid(), "/media/a.srt", -1, "ab", _moment));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static PublishedSubtitle Entry(string path, long size) =>
        new(Guid.NewGuid(), path, size, new string('a', 64), _moment);
}
