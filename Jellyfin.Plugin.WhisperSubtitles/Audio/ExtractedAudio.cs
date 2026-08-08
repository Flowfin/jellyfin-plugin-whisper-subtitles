using System;
using System.IO;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// A file of extracted audio, and the obligation to remove it.
/// </summary>
/// <remarks>
/// The file is returned as a thing that owns itself rather than as a path,
/// because a path is something a caller can forget. Handing back a string leaves
/// the deletion in whatever the caller wrote around it, and a caller that
/// returns early, throws, or is cancelled leaves an hour of audio on the disk
/// for every item it did that to.
///
/// Disposing twice is allowed and disposing after the file has already gone is
/// allowed, because a caller unwinding through a failure has no way to know
/// which of those it is doing.
/// </remarks>
public sealed class ExtractedAudio : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractedAudio"/> class.
    /// </summary>
    /// <param name="path">The file that was written.</param>
    /// <param name="sizeInBytes">How large it was when it was measured.</param>
    public ExtractedAudio(string path, long sizeInBytes)
    {
        Path = path;
        SizeInBytes = sizeInBytes;
    }

    /// <summary>
    /// Gets the file that was written.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets how large the file was when the extraction finished.
    /// </summary>
    public long SizeInBytes { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // The file is in a directory this plugin owns, and what stays there is
            // what TemporaryAudioSweep collects. Throwing out of a dispose that is
            // usually running inside somebody else's failure would replace the
            // reason that failure had with a file system one.
        }
        catch (UnauthorizedAccessException)
        {
            // Same, and the same sweep collects it.
        }
    }
}
