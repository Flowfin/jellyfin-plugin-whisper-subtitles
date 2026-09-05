using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// A file's size and the SHA-256 of its bytes, read at one moment.
/// </summary>
public sealed class FileDigest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileDigest"/> class.
    /// </summary>
    /// <param name="sizeInBytes">How many bytes the file holds.</param>
    /// <param name="sha256">The SHA-256 of those bytes, as lower case hexadecimal.</param>
    public FileDigest(long sizeInBytes, string sha256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        SizeInBytes = sizeInBytes;
        Sha256 = sha256;
    }

    /// <summary>
    /// Gets how many bytes the file holds.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the SHA-256 of those bytes, as lower case hexadecimal.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Whether this is the same size and the same bytes a recorded entry was
    /// written with.
    /// </summary>
    /// <param name="entry">The entry the record holds for the file.</param>
    /// <returns>True when nothing about the bytes has changed.</returns>
    public bool Matches(PublishedSubtitle entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return SizeInBytes == entry.SizeInBytes
            && string.Equals(Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
