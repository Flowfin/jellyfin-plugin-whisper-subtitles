using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Publishes a subtitle file, so that nothing ever sees a half written one.
/// </summary>
/// <remarks>
/// The library watches the folders the media is in. A file that appears under
/// its final name while it is still being written can be read in that state, and
/// a run that is killed halfway leaves a truncated file that looks like a
/// finished subtitle for as long as the item exists. Neither failure announces
/// itself: what an operator sees is a subtitle that stops in the middle, months
/// after the run that wrote it.
///
/// So the bytes go to a name in the destination directory that no watcher is
/// looking for, and the file takes its final name only once every byte is
/// written and flushed. The rename is within one directory, which is what makes
/// it a single operation on every file system a server runs on rather than a
/// copy with a window in the middle.
///
/// This type does not decide where the file goes, which is #25, and it does not
/// decide what to do about a name that is already taken, which is #28. What it
/// holds is narrower and is the floor under both: a file this plugin has not
/// finished writing is never visible under the name a reader would open, and a
/// file this plugin did not write is never overwritten by it.
/// </remarks>
public static class AtomicSubtitleFile
{
    /// <summary>
    /// The extension a file still being written carries.
    /// </summary>
    /// <remarks>
    /// Not the subtitle extension, and not one either the server or a player
    /// reads. The point of writing under another name is lost if the name still
    /// looks like a subtitle to whatever is watching the folder.
    /// </remarks>
    public const string TemporaryExtension = ".whisper-part";

    private const int BufferBytes = 64 * 1024;

    /// <summary>
    /// Writes a subtitle and publishes it under its final name.
    /// </summary>
    /// <param name="destinationPath">The name a reader will open, in the directory the file belongs in.</param>
    /// <param name="content">The finished bytes of the subtitle.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once the file carries its final name.</returns>
    public static Task WriteAsync(string destinationPath, byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        return WriteAsync(
            destinationPath,
            (stream, token) => stream.WriteAsync(content, token).AsTask(),
            cancellationToken);
    }

    /// <summary>
    /// Writes a subtitle from something that produces it, and publishes it under
    /// its final name.
    /// </summary>
    /// <param name="destinationPath">The name a reader will open, in the directory the file belongs in.</param>
    /// <param name="writeContent">Writes the subtitle into the stream it is handed.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once the file carries its final name.</returns>
    /// <exception cref="IOException">The destination name is already taken.</exception>
    public static async Task WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, Task> writeContent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(writeContent);

        var directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException(
                "A subtitle is published into a directory and this path names none.",
                nameof(destinationPath));
        }

        // Asked before anything is written as well as enforced by the rename
        // below, and the two are not the same question. This one spares a
        // transcription's worth of bytes where the answer is already known; the
        // rename is what holds where the file appeared while this was running.
        if (File.Exists(destinationPath))
        {
            throw new IOException(FormattableString.Invariant(
                $"{destinationPath} already exists, and this plugin does not overwrite a subtitle it finds."));
        }

        var temporaryPath = Path.Combine(directory, TemporaryNameFor(destinationPath));

        try
        {
            var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferBytes,
                FileOptions.Asynchronous);

            await using (stream.ConfigureAwait(false))
            {
                await writeContent(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                // To the disk rather than to the operating system's buffers. A
                // rename that outruns the bytes it is renaming survives a clean
                // shutdown and not a power cut.
                //
                // The analyser asks for FlushAsync here and it is wrong for this
                // one case: FlushAsync flushes to the operating system and takes
                // no argument that reaches the disk, so taking its advice would
                // leave the property this line exists for unheld. There is no
                // asynchronous spelling of this call.
#pragma warning disable CA1849 // Call async methods when in an async method
                stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
            }

            // A run cancelled after the last byte was written has still not been
            // asked for a subtitle, so the file does not appear.
            cancellationToken.ThrowIfCancellationRequested();

            // Without an overwrite argument, which is the refusal rather than an
            // omission: this throws where the name was taken between the check
            // above and here.
            File.Move(temporaryPath, destinationPath);
        }
        catch
        {
            Delete(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// The name a file being written carries, in the directory it will be
    /// published into.
    /// </summary>
    /// <param name="destinationPath">The name it will be published under.</param>
    /// <returns>A file name, without a directory.</returns>
    /// <remarks>
    /// It carries the destination's own name so that a leftover says which item
    /// it came from, a unique part so two attempts on one item cannot collide,
    /// and a leading dot so it is hidden where a leading dot hides a file.
    /// </remarks>
    public static string TemporaryNameFor(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        return string.Create(
            CultureInfo.InvariantCulture,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}{TemporaryExtension}");
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The caller is already unwinding through the failure that brought
            // us here, and replacing its reason with a file system one would
            // send whoever reads the log after the wrong thing.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
