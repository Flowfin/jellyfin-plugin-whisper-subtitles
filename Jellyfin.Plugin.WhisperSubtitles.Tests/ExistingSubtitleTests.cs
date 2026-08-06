using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The worst thing this plugin could do is lose a subtitle somebody corrected by
/// hand, so what is asserted here is the bytes of a file the run did not write,
/// after a run that tried to write over it.
///
/// A real directory and real files. A double of a file system would show that this
/// code calls the methods this code calls, and the property is about what is on a
/// disk once the run is over.
/// </summary>
public sealed class ExistingSubtitleTests : IDisposable
{
    private static readonly byte[] _handCorrected = Encoding.UTF8.GetBytes(
        "1\r\n00:00:01,000 --> 00:00:04,000\r\nSomebody typed this, and fixed it twice.\r\n\r\n");

    private static readonly byte[] _transcribed = Encoding.UTF8.GetBytes(
        "1\r\n00:00:01,000 --> 00:00:02,000\r\nA machine wrote this.\r\n\r\n");

    private readonly string _library = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_run_over_a_directory_holding_one_hand_corrected_subtitle_leaves_it_alone()
    {
        var items = new[] { "First Film", "Second Film", "Third Film" };

        Directory.CreateDirectory(_library);
        await File.WriteAllBytesAsync(Target("Second Film"), _handCorrected);

        var published = new ConcurrentQueue<SubtitlePublication>();

        var outcome = await BoundedRun.RunAsync(
            items,
            workers: 2,
            work: async (item, token) =>
                published.Enqueue(
                    await SubtitlePublisher.PublishAsync(Target(item), _transcribed, token).ConfigureAwait(false)),
            CancellationToken.None);

        // The file the operator made, byte for byte.
        Assert.Equal(_handCorrected, await File.ReadAllBytesAsync(Target("Second Film")));

        // Exactly one file per item and nothing else: no numbered variant beside
        // the one that was in the way, and no leftover from a write that was
        // refused.
        Assert.Equal(
            new[] { "First Film.srt", "Second Film.srt", "Third Film.srt" },
            NamesInLibrary());

        // The item is reported as skipped, and its reason is the collision rather
        // than a general failure.
        var skipped = Assert.Single(published, p => !p.WasWritten);
        Assert.Equal(SubtitlePublicationOutcome.SkippedTargetExists, skipped.Outcome);
        Assert.Equal(Target("Second Film"), skipped.Path);

        // And the run carried on. A collision is a normal thing to find in a
        // directory of the operator's own files, so it is not a failure and it does
        // not stop the items after it.
        Assert.Empty(outcome.Failures);
        Assert.Equal(items.Length, outcome.Completed);
        Assert.Equal(2, published.Count(p => p.WasWritten));
        Assert.Equal(_transcribed, await File.ReadAllBytesAsync(Target("First Film")));
        Assert.Equal(_transcribed, await File.ReadAllBytesAsync(Target("Third Film")));
    }

    [Fact]
    public async Task The_file_in_the_way_is_never_opened_for_writing()
    {
        // Held open for reading with no sharing for writing, which is the state a
        // player streaming it would have it in. A publish that truncated it, or
        // opened it to find out whether it could, fails here on the file rather
        // than on an assertion about it.
        Directory.CreateDirectory(_library);
        await File.WriteAllBytesAsync(Target("A Film"), _handCorrected);

        using var readerHoldingIt = new FileStream(
            Target("A Film"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var publication = await SubtitlePublisher.PublishAsync(
            Target("A Film"),
            _transcribed,
            CancellationToken.None);

        Assert.Equal(SubtitlePublicationOutcome.SkippedTargetExists, publication.Outcome);

        readerHoldingIt.Position = 0;
        var stillThere = new byte[_handCorrected.Length];
        await readerHoldingIt.ReadExactlyAsync(stillThere);

        Assert.Equal(_handCorrected, stillThere);
        Assert.Equal(_handCorrected.Length, readerHoldingIt.Length);
    }

    [Fact]
    public async Task An_empty_file_in_the_way_is_still_in_the_way()
    {
        // Nought bytes is the shape somebody reaches for as a marker, and it is
        // also what a crashed tool leaves. Either way it is not this plugin's file
        // to replace, and a check written as a length rather than as an existence
        // would replace it.
        Directory.CreateDirectory(_library);
        await File.WriteAllBytesAsync(Target("A Film"), []);

        var publication = await SubtitlePublisher.PublishAsync(
            Target("A Film"),
            _transcribed,
            CancellationToken.None);

        Assert.Equal(SubtitlePublicationOutcome.SkippedTargetExists, publication.Outcome);
        Assert.Empty(await File.ReadAllBytesAsync(Target("A Film")));
        Assert.Equal(new[] { "A Film.srt" }, NamesInLibrary());
    }

    [Fact]
    public async Task A_name_that_is_taken_while_the_write_is_running_is_a_skip_and_not_a_failure()
    {
        // The check the write makes before it starts cannot hold this case, and it
        // is the case selection can never rule out: the item was clear when the
        // transcription began and something arrived during the minutes it took.
        Directory.CreateDirectory(_library);

        var publication = await SubtitlePublisher.PublishAsync(
            Target("A Film"),
            async (stream, token) =>
            {
                await stream.WriteAsync(_transcribed, token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Target("A Film"), _handCorrected, token).ConfigureAwait(false);
            },
            CancellationToken.None);

        Assert.Equal(SubtitlePublicationOutcome.SkippedTargetExists, publication.Outcome);
        Assert.Equal(_handCorrected, await File.ReadAllBytesAsync(Target("A Film")));
        Assert.Equal(new[] { "A Film.srt" }, NamesInLibrary());
    }

    [Fact]
    public async Task A_file_system_failure_that_is_not_a_collision_stays_a_failure()
    {
        // The near miss this pair exists for. A skip is recognised by a file being
        // at the destination, and a publish that reported every file system error
        // as a skip would turn a directory nobody can write into a run that
        // reports every item quietly skipped and no subtitle anywhere.
        var missingDirectory = Path.Combine(_library, "not created", "A Film.srt");

        await Assert.ThrowsAnyAsync<IOException>(() =>
            SubtitlePublisher.PublishAsync(missingDirectory, _transcribed, CancellationToken.None));
    }

    [Fact]
    public async Task A_clear_destination_is_written_and_reported_as_written()
    {
        // Guards the assertions above rather than the refusal. A publish that
        // wrote nothing at all would report every item skipped and satisfy every
        // other leg here.
        Directory.CreateDirectory(_library);

        var publication = await SubtitlePublisher.PublishAsync(
            Target("A Film"),
            _transcribed,
            CancellationToken.None);

        Assert.Equal(SubtitlePublicationOutcome.Written, publication.Outcome);
        Assert.True(publication.WasWritten);
        Assert.Equal(Target("A Film"), publication.Path);
        Assert.Equal(_transcribed, await File.ReadAllBytesAsync(Target("A Film")));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_library))
        {
            Directory.Delete(_library, recursive: true);
        }
    }

    private string Target(string item) => Path.Combine(_library, item + ".srt");

    private string[] NamesInLibrary() =>
        Directory.GetFiles(_library)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
}
