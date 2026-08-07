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
/// What these are about is which folder a file lands in and, more importantly,
/// which folders stay empty. An operator who has told their server not to write
/// into their media tree has said something this plugin honours, and the way to
/// show it is honoured is to look at a real tree afterwards.
///
/// The directories are real and the files are real. A double of a file system
/// would prove this code calls the methods this code calls, and what is asserted
/// here is what is on a disk when the write has finished.
/// </summary>
public sealed class SubtitleDestinationTests : IDisposable
{
    private static readonly byte[] _subtitle = Encoding.UTF8.GetBytes(
        "1\r\n00:00:01,000 --> 00:00:02,000\r\nA line somebody said.\r\n\r\n");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    public SubtitleDestinationTests()
    {
        Directory.CreateDirectory(MediaFolder);
        Directory.CreateDirectory(Elsewhere);
        File.WriteAllBytes(MediaFile, Encoding.ASCII.GetBytes("not really a film"));
    }

    private string MediaFolder => Path.Combine(_root, "library", "A Film (2019)");

    private string MediaFile => Path.Combine(MediaFolder, "A Film (2019).mkv");

    private string MetadataFolder => Path.Combine(_root, "data", "metadata", "library", "abc123");

    /// <summary>
    /// A folder that is neither destination, so a test can say nothing arrived
    /// anywhere else rather than only that something arrived where it should.
    /// </summary>
    private string Elsewhere => Path.Combine(_root, "elsewhere");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_library_that_saves_subtitles_with_media_gets_the_file_next_to_the_media()
    {
        var written = await new SubtitleOutput().WriteAsync(
            Location(saveWithMedia: true),
            "A Film (2019).en.srt",
            _subtitle,
            CancellationToken.None);

        Assert.Equal(SubtitleDestinationKind.BesideTheMedia, written.Kind);
        Assert.Equal(Path.Combine(MediaFolder, "A Film (2019).en.srt"), written.Path);
        Assert.Equal(_subtitle, await File.ReadAllBytesAsync(written.Path));
        Assert.Equal(new[] { Path.Combine("library", "A Film (2019)", "A Film (2019).en.srt") }, WrittenFiles());
    }

    [Fact]
    public async Task A_library_that_does_not_gets_the_file_in_the_items_metadata_folder()
    {
        // The metadata folder does not exist for an item nothing has written for yet,
        // which is the ordinary case rather than an edge one.
        Assert.False(Directory.Exists(MetadataFolder));

        var written = await new SubtitleOutput().WriteAsync(
            Location(saveWithMedia: false),
            "A Film (2019).en.srt",
            _subtitle,
            CancellationToken.None);

        Assert.Equal(SubtitleDestinationKind.InTheMetadataFolder, written.Kind);
        Assert.Equal(Path.Combine(MetadataFolder, "A Film (2019).en.srt"), written.Path);
        Assert.Equal(_subtitle, await File.ReadAllBytesAsync(written.Path));

        // Nothing in the media tree, which is the half of this the operator asked
        // for. Their media folder still holds one file, the one that was there.
        Assert.Equal(new[] { "A Film (2019).mkv" }, NamesIn(MediaFolder));
    }

    [Fact]
    public async Task A_destination_that_refuses_the_write_is_a_typed_failure_and_leaves_nothing_behind()
    {
        // The refusal is injected, because a directory that exists and refuses a
        // write is a file system permission and setting one differs enough between
        // platforms that the test would be about the platform. What is asserted is
        // that the refusal arrives as a reason and not as an exception from the
        // depths, and that nothing partial is left where the file would have gone.
        var refusing = new SubtitleOutput((_, _, _) =>
            throw new UnauthorizedAccessException("Access to the path is denied."));

        var failed = await Assert.ThrowsAsync<SubtitleNotWrittenException>(
            () => refusing.WriteAsync(
                Location(saveWithMedia: true),
                "A Film (2019).en.srt",
                _subtitle,
                CancellationToken.None));

        Assert.Equal(SubtitleWriteFailure.DestinationUnwritable, failed.Failure);
        Assert.Contains(MediaFolder, failed.Message, StringComparison.Ordinal);
        Assert.Contains("read only", failed.Message, StringComparison.Ordinal);
        Assert.Empty(WrittenFiles());
    }

    [Fact]
    public async Task A_media_folder_the_operating_system_refuses_is_the_same_failure()
    {
        // The one unwritable destination a test can produce portably: the chosen
        // folder's own path is a file. Every platform refuses to make a directory
        // under a file, and it reaches the same reason as a permission would.
        var asFile = Path.Combine(_root, "library", "Not A Folder.mkv");
        await File.WriteAllBytesAsync(asFile, Encoding.ASCII.GetBytes("a file where a folder would be"));

        var item = new ItemLocation(
            Path.Combine(asFile, "An Episode.mkv"),
            MetadataFolder,
            saveSubtitlesWithMedia: true);

        var failed = await Assert.ThrowsAsync<SubtitleNotWrittenException>(
            () => new SubtitleOutput().WriteAsync(item, "An Episode.en.srt", _subtitle, CancellationToken.None));

        Assert.Equal(SubtitleWriteFailure.DestinationUnwritable, failed.Failure);
        Assert.Empty(WrittenFiles());
    }

    [Theory]
    [InlineData("../escaped.srt")]
    [InlineData("../../escaped.srt")]
    [InlineData("elsewhere/escaped.srt")]
    [InlineData("sub/A Film (2019).en.srt")]
    [InlineData(".")]
    public async Task A_name_that_would_leave_the_destination_is_refused_and_writes_nothing(string fileName)
    {
        // The name comes from a media file name and a language code, and the code
        // came from a backend, so this is the last place before a path is opened.
        foreach (var saveWithMedia in new[] { true, false })
        {
            var failed = await Assert.ThrowsAsync<SubtitleNotWrittenException>(
                () => new SubtitleOutput().WriteAsync(
                    Location(saveWithMedia),
                    fileName,
                    _subtitle,
                    CancellationToken.None));

            Assert.Equal(SubtitleWriteFailure.NameWouldLeaveTheDestination, failed.Failure);
        }

        Assert.Empty(WrittenFiles());
        Assert.Empty(NamesIn(Elsewhere));
    }

    [Fact]
    public async Task A_rooted_name_is_refused_rather_than_followed()
    {
        // Path.Combine returns a rooted second argument as it is, so a name that is
        // a full path would otherwise be written exactly where it says.
        var rooted = Path.Combine(Elsewhere, "escaped.srt");

        var failed = await Assert.ThrowsAsync<SubtitleNotWrittenException>(
            () => new SubtitleOutput().WriteAsync(
                Location(saveWithMedia: true),
                rooted,
                _subtitle,
                CancellationToken.None));

        Assert.Equal(SubtitleWriteFailure.NameWouldLeaveTheDestination, failed.Failure);
        Assert.False(File.Exists(rooted));
        Assert.Empty(WrittenFiles());
    }

    [Fact]
    public async Task Nothing_is_ever_written_outside_the_two_folders()
    {
        // Every case above, run against one tree, so what is asserted at the end is
        // over the whole of it rather than over the folder each case was looking at.
        var names = new[]
        {
            "A Film (2019).en.srt",
            "../escaped.srt",
            "elsewhere/escaped.srt",
            Path.Combine(Elsewhere, "escaped.srt"),
        };

        foreach (var saveWithMedia in new[] { true, false })
        {
            foreach (var name in names)
            {
                try
                {
                    await new SubtitleOutput().WriteAsync(
                        Location(saveWithMedia),
                        name,
                        _subtitle,
                        CancellationToken.None);
                }
                catch (SubtitleNotWrittenException)
                {
                    // Refused, which is the point. What matters is the tree at the end.
                }
            }
        }

        Assert.Equal(
            new[]
            {
                Path.Combine("data", "metadata", "library", "abc123", "A Film (2019).en.srt"),
                Path.Combine("library", "A Film (2019)", "A Film (2019).en.srt"),
                Path.Combine("library", "A Film (2019)", "A Film (2019).mkv"),
            },
            AllFiles());

        Assert.Empty(NamesIn(Elsewhere));
    }

    [Fact]
    public void The_folder_is_chosen_without_a_disk_being_touched()
    {
        // The decision is a property of the library's setting and the item's paths,
        // so it can be argued with before anything exists. Neither folder is created
        // by asking.
        var absent = new ItemLocation(
            Path.Combine(_root, "gone", "A Film.mkv"),
            Path.Combine(_root, "gone-metadata"),
            saveSubtitlesWithMedia: true);

        Assert.Equal(Path.Combine(_root, "gone"), SubtitleDestination.Choose(absent, out var beside));
        Assert.Equal(SubtitleDestinationKind.BesideTheMedia, beside);
        Assert.False(Directory.Exists(Path.Combine(_root, "gone")));

        var closed = new ItemLocation(
            Path.Combine(_root, "gone", "A Film.mkv"),
            Path.Combine(_root, "gone-metadata"),
            saveSubtitlesWithMedia: false);

        Assert.Equal(Path.Combine(_root, "gone-metadata"), SubtitleDestination.Choose(closed, out var metadata));
        Assert.Equal(SubtitleDestinationKind.InTheMetadataFolder, metadata);
        Assert.False(Directory.Exists(Path.Combine(_root, "gone-metadata")));
    }

    [Fact]
    public void A_media_path_with_no_folder_is_refused_rather_than_redirected()
    {
        // Falling back to the metadata folder here would put the file somewhere the
        // operator did not ask for, and they would have no way of knowing.
        var nowhere = new ItemLocation("A Film.mkv", MetadataFolder, saveSubtitlesWithMedia: true);

        var failed = Assert.Throws<SubtitleNotWrittenException>(() => SubtitleDestination.Choose(nowhere, out _));

        Assert.Equal(SubtitleWriteFailure.DestinationUnwritable, failed.Failure);
    }

    private ItemLocation Location(bool saveWithMedia) =>
        new(MediaFile, MetadataFolder, saveWithMedia);

    /// <summary>
    /// Every file under the root except the media file that was there to begin with.
    /// </summary>
    private string[] WrittenFiles() =>
        AllFiles()
            .Where(name => !name.EndsWith(".mkv", StringComparison.Ordinal))
            .ToArray();

    private string[] AllFiles() =>
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_root, path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static string[] NamesIn(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder).Select(path => Path.GetFileName(path)).OrderBy(name => name, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
}
