using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// What a read of the record gave back: the entries that could be read, and how
/// many lines could not be.
/// </summary>
/// <remarks>
/// The count is carried rather than the bad lines being dropped in silence,
/// because the record lives in plugin data a person can edit, and a line that
/// will not parse is a fact an operator listing their files should be told
/// rather than one the listing quietly reads past.
/// </remarks>
public sealed class PublishedSubtitleRecordReading
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishedSubtitleRecordReading"/> class.
    /// </summary>
    /// <param name="entries">The entries that could be read, oldest first.</param>
    /// <param name="unreadableLines">How many lines could not be read as an entry.</param>
    public PublishedSubtitleRecordReading(IReadOnlyList<PublishedSubtitle> entries, int unreadableLines)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegative(unreadableLines);

        Entries = entries;
        UnreadableLines = unreadableLines;
    }

    /// <summary>
    /// Gets the entries that could be read, oldest first.
    /// </summary>
    public IReadOnlyList<PublishedSubtitle> Entries { get; }

    /// <summary>
    /// Gets how many lines could not be read as an entry.
    /// </summary>
    public int UnreadableLines { get; }
}
