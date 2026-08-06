using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Writes a finished subtitle where the item's library asks for it.
/// </summary>
/// <remarks>
/// The two halves it is made of are elsewhere on purpose.
/// <see cref="SubtitleDestination"/> decides the folder and refuses a name that
/// would leave it, and <see cref="AtomicSubtitleFile"/> makes the file visible
/// only once every byte is written. What is here is the part that touches a disk
/// and can therefore fail for reasons that are about the machine rather than
/// about the transcription: a read-only media directory, a metadata folder that
/// cannot be created, a destination that is not a directory at all.
///
/// A read-only media directory is a normal configuration and not a mistake, which
/// is why it ends as a typed failure with a sentence rather than as an exception
/// nobody expected. It says the destination refused the write, and nothing about
/// the item, because nothing about the item is wrong.
/// </remarks>
public sealed class SubtitleOutput
{
    private readonly Func<string, byte[], CancellationToken, Task> _write;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleOutput"/> class.
    /// </summary>
    public SubtitleOutput()
        : this(AtomicSubtitleFile.WriteAsync)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleOutput"/> class.
    /// </summary>
    /// <param name="write">The write itself, which the default constructor supplies.</param>
    /// <remarks>
    /// The seam exists for one case a real directory cannot produce portably: a
    /// destination that exists, is a directory, and refuses the write. A read-only
    /// directory is a permission on the file system, and setting one differs enough
    /// between platforms that a test doing it would be testing the platform. So the
    /// refusal is injected, and the tests that use a real directory cover
    /// everything else.
    /// </remarks>
    public SubtitleOutput(Func<string, byte[], CancellationToken, Task> write)
    {
        _write = write ?? throw new ArgumentNullException(nameof(write));
    }

    /// <summary>
    /// Writes one subtitle for one item.
    /// </summary>
    /// <param name="item">Where the item's files are, and what its library allows.</param>
    /// <param name="fileName">The name of the subtitle file, and nothing else.</param>
    /// <param name="content">The finished bytes of the subtitle.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>Where the file went.</returns>
    public async Task<WrittenSubtitle> WriteAsync(
        ItemLocation item,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(content);

        var folder = SubtitleDestination.Choose(item, out var kind);
        var path = SubtitleDestination.Resolve(folder, fileName);

        // The media folder is there by definition and a metadata folder for an item
        // nothing has written yet is not, so this creates rather than assumes. It
        // creates the chosen folder and nothing above it, because Resolve has
        // already refused anything that is not inside it.
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception refused) when (refused is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new SubtitleNotWrittenException(
                SubtitleWriteFailure.DestinationUnwritable,
                Describe(folder, kind),
                refused);
        }

        try
        {
            await _write(path, content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception refused) when (refused is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new SubtitleNotWrittenException(
                SubtitleWriteFailure.DestinationUnwritable,
                Describe(folder, kind),
                refused);
        }

        return new WrittenSubtitle(path, kind);
    }

    /// <remarks>
    /// The sentence names the folder and which of the two it is, because the repair
    /// differs: a media directory the server may not write into is either made
    /// writable or the library's option is turned off, and a metadata folder that
    /// refuses is a problem with the server's own data directory.
    /// </remarks>
    private static string Describe(string folder, SubtitleDestinationKind kind) =>
        string.Format(
            CultureInfo.InvariantCulture,
            kind == SubtitleDestinationKind.BesideTheMedia
                ? "The media folder {0} refused the subtitle. It is read only for the account the server runs as, or full. The transcription is fine; only the write failed. Turning off the library's option to save subtitles with media writes into the item's metadata folder instead."
                : "The item's metadata folder {0} refused the subtitle. It is read only for the account the server runs as, or full. The transcription is fine; only the write failed.",
            folder);
}
