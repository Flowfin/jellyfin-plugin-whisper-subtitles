using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// One line of the record of what this plugin published: which item, which file,
/// and the bytes as they were when the file took its name.
/// </summary>
/// <remarks>
/// The record is what separates a file this plugin wrote from one a person made,
/// and it is matched against rather than a name pattern, because a person can
/// name a file anything. The size and the SHA-256 are what "edited since the
/// plugin wrote it" is compared against, which #43 decided on 2026-09-05: a hash
/// answers exactly and costs one read per file at listing time, and a size or a
/// timestamp alone is wrong after a copy.
///
/// The moment is handed in rather than read, because nothing in this plugin
/// reads a wall clock on its own; the caller that publishes is the caller that
/// knows when.
/// </remarks>
public sealed class PublishedSubtitle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishedSubtitle"/> class.
    /// </summary>
    /// <param name="itemId">The library item the subtitle belongs to.</param>
    /// <param name="path">The name the file took.</param>
    /// <param name="sizeInBytes">How many bytes were written.</param>
    /// <param name="sha256">The SHA-256 of those bytes, as lower case hexadecimal.</param>
    /// <param name="writtenAt">When the file took its name.</param>
    public PublishedSubtitle(Guid itemId, string path, long sizeInBytes, string sha256, DateTimeOffset writtenAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes);

        ItemId = itemId;
        Path = path;
        SizeInBytes = sizeInBytes;
        Sha256 = sha256;
        WrittenAt = writtenAt;
    }

    /// <summary>
    /// Gets the library item the subtitle belongs to.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets the name the file took.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets how many bytes were written.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the SHA-256 of the bytes as written, as lower case hexadecimal.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Gets when the file took its name.
    /// </summary>
    public DateTimeOffset WrittenAt { get; }
}
