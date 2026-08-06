using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Publishes a subtitle where nothing of the operator's is in the way, and says
/// so where something is.
/// </summary>
/// <remarks>
/// This plugin writes into directories that hold files somebody made by hand.
/// Losing a hand corrected subtitle to a machine transcription is the worst thing
/// it could do, so an existing file at the destination is never overwritten,
/// never truncated, never removed and never renamed out of the way. The item is
/// reported as skipped and the run carries on.
///
/// Nor is a second name tried. A numbered variant beside the operator's file
/// would leave two subtitles in the same language on one item, with a client
/// picking between them by rules nobody here controls, and the operator's own
/// work is the one that would look like the duplicate.
///
/// This is separate from selection leaving out items that already have a subtitle
/// in the target language, and it is the half that has to hold. Selection reads
/// the item's streams, from a library scan that can be stale, and it runs minutes
/// or hours before the write; this asks the file system at the moment of writing.
/// </remarks>
public static class SubtitlePublisher
{
    /// <summary>
    /// Writes the subtitle unless something is already there.
    /// </summary>
    /// <param name="destinationPath">The name a reader will open.</param>
    /// <param name="content">The finished bytes of the subtitle.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>What became of the attempt.</returns>
    public static Task<SubtitlePublication> PublishAsync(
        string destinationPath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        return PublishAsync(
            destinationPath,
            (stream, token) => stream.WriteAsync(content, token).AsTask(),
            cancellationToken);
    }

    /// <summary>
    /// Writes the subtitle from something that produces it, unless something is
    /// already there.
    /// </summary>
    /// <param name="destinationPath">The name a reader will open.</param>
    /// <param name="writeContent">Writes the subtitle into the stream it is handed.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>What became of the attempt.</returns>
    public static async Task<SubtitlePublication> PublishAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeContent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(writeContent);

        // There is no check for the file here, deliberately. The write already
        // refuses a taken name, both before it starts and at the rename, so a
        // check on this side would be a second answer to a question that is
        // already answered and could be deleted without any run behaving
        // differently. What this adds is the reading: the refusal becomes an
        // outcome instead of a fault.
        try
        {
            await AtomicSubtitleFile.WriteAsync(destinationPath, writeContent, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IOException) when (File.Exists(destinationPath))
        {
            // The condition is what keeps an unrelated file system failure from
            // being read as a skip. A full disk, a directory that is not there and
            // a permission that was withdrawn are faults, they leave no file at the
            // destination, and they go on throwing. Only a name that is taken puts
            // a file there, and the write never overwrites one, so the file this
            // sees is not one this attempt produced.
            return SubtitlePublication.SkippedBecauseSomethingIsAlreadyThere(destinationPath);
        }

        return SubtitlePublication.Written(destinationPath);
    }
}
