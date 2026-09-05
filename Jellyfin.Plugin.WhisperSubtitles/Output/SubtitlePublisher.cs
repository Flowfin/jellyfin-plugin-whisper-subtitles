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
    public static Task<SubtitlePublication> PublishAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeContent,
        CancellationToken cancellationToken) =>
        PublishAsync(destinationPath, writeContent, recording: null, cancellationToken);

    /// <summary>
    /// Writes the subtitle unless something is already there, and records what it
    /// wrote before the file takes its name.
    /// </summary>
    /// <param name="destinationPath">The name a reader will open.</param>
    /// <param name="content">The finished bytes of the subtitle.</param>
    /// <param name="recording">The record to append to, and the item and moment the entry carries.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>What became of the attempt.</returns>
    public static Task<SubtitlePublication> PublishAsync(
        string destinationPath,
        byte[] content,
        SubtitleRecording recording,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(recording);

        return PublishAsync(
            destinationPath,
            (stream, token) => stream.WriteAsync(content, token).AsTask(),
            recording,
            cancellationToken);
    }

    /// <summary>
    /// Writes the subtitle from something that produces it, unless something is
    /// already there, and records what it wrote before the file takes its name.
    /// </summary>
    /// <param name="destinationPath">The name a reader will open.</param>
    /// <param name="writeContent">Writes the subtitle into the stream it is handed.</param>
    /// <param name="recording">The record to append to, and the item and moment the entry carries, or null to record nothing.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>What became of the attempt.</returns>
    /// <remarks>
    /// The bytes are hashed on their way into the file, and the entry is appended
    /// between the last byte reaching the disk and the file taking its name. A
    /// record that refuses the entry therefore leaves no file under the final
    /// name, which is the direction #43 decided: what the record does not name
    /// was never published. A skip because something is already there appends
    /// nothing, because nothing was written.
    ///
    /// The overloads without a recording are what the suite drives the write with,
    /// and a run publishes with one; which run supplies it is #183.
    /// </remarks>
    public static async Task<SubtitlePublication> PublishAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeContent,
        SubtitleRecording? recording,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(writeContent);

        DigestingStream? digest = null;

        Func<Stream, CancellationToken, Task> digesting = recording is null
            ? writeContent
            : (stream, token) =>
            {
                digest = new DigestingStream(stream);

                return writeContent(digest, token);
            };

        Func<CancellationToken, Task>? beforeReveal = recording is null
            ? null
            : token => recording.Record.AppendAsync(
                new PublishedSubtitle(
                    recording.ItemId,
                    destinationPath,
                    digest!.BytesWritten,
                    digest.Sha256Hex(),
                    recording.WrittenAt),
                token);

        // There is no check for the file here, deliberately. The write already
        // refuses a taken name, both before it starts and at the rename, so a
        // check on this side would be a second answer to a question that is
        // already answered and could be deleted without any run behaving
        // differently. What this adds is the reading: the refusal becomes an
        // outcome instead of a fault.
        try
        {
            await AtomicSubtitleFile.WriteAsync(destinationPath, digesting, beforeReveal, cancellationToken)
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
        finally
        {
            if (digest is not null)
            {
                await digest.DisposeAsync().ConfigureAwait(false);
            }
        }

        return SubtitlePublication.Written(destinationPath);
    }
}
