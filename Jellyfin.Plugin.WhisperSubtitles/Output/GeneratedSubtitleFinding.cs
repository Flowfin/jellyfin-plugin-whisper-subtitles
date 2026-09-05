using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// One recorded file and what a listing found at its path.
/// </summary>
public sealed class GeneratedSubtitleFinding
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSubtitleFinding"/> class.
    /// </summary>
    /// <param name="entry">The record's entry for the file.</param>
    /// <param name="state">What the listing found.</param>
    /// <param name="now">The file's bytes as read at listing time, or null when nothing was there.</param>
    public GeneratedSubtitleFinding(PublishedSubtitle entry, GeneratedSubtitleState state, FileDigest? now)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        State = state;
        Now = now;
    }

    /// <summary>
    /// Gets the record's entry for the file.
    /// </summary>
    public PublishedSubtitle Entry { get; }

    /// <summary>
    /// Gets what the listing found.
    /// </summary>
    public GeneratedSubtitleState State { get; }

    /// <summary>
    /// Gets the file's bytes as read at listing time, or null when nothing was there.
    /// </summary>
    public FileDigest? Now { get; }
}
