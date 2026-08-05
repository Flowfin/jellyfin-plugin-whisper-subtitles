using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Turns timed segments into the bytes of one subtitle file.
/// </summary>
/// <remarks>
/// Cues in, bytes out, and nothing else. A second format is a second
/// implementation of this rather than a change to the one that exists, which is
/// what keeps the first release writing one format from becoming a decision that
/// has to be unpicked later.
/// </remarks>
public interface ISubtitleFormatWriter
{
    /// <summary>
    /// Gets the file extension this format uses, without the leading dot.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Writes the segments as one subtitle file.
    /// </summary>
    /// <param name="segments">The timed segments, in the order they occur.</param>
    /// <returns>The bytes of the file, ready to be written as they are.</returns>
    byte[] Write(IReadOnlyList<TimedSegment> segments);
}
