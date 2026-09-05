using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// What a publish needs in order to record itself: the record, the item the
/// subtitle belongs to, and the moment.
/// </summary>
/// <remarks>
/// Three values a run knows and the publisher does not, carried together so a
/// publish is either recorded with all three or not recorded at all. The moment
/// is the caller's for the reason every moment in this plugin is: nothing here
/// reads a wall clock on its own.
/// </remarks>
public sealed class SubtitleRecording
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleRecording"/> class.
    /// </summary>
    /// <param name="record">The record the entry is appended to.</param>
    /// <param name="itemId">The library item the subtitle belongs to.</param>
    /// <param name="writtenAt">When the publish is happening.</param>
    public SubtitleRecording(IPublishedSubtitleRecord record, Guid itemId, DateTimeOffset writtenAt)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        ItemId = itemId;
        WrittenAt = writtenAt;
    }

    /// <summary>
    /// Gets the record the entry is appended to.
    /// </summary>
    public IPublishedSubtitleRecord Record { get; }

    /// <summary>
    /// Gets the library item the subtitle belongs to.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets when the publish is happening.
    /// </summary>
    public DateTimeOffset WrittenAt { get; }
}
