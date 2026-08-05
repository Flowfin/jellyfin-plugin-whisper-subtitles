using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// One stretch of speech with the times it covers in the media.
/// </summary>
/// <remarks>
/// A backend returns these rather than a formatted subtitle file, because
/// formatting, naming and marking belong to this plugin and must not differ
/// between backends.
/// </remarks>
public sealed class TimedSegment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimedSegment"/> class.
    /// </summary>
    /// <param name="start">Where the segment starts, measured from the start of the media.</param>
    /// <param name="end">Where the segment ends, measured from the start of the media.</param>
    /// <param name="text">What was said.</param>
    public TimedSegment(TimeSpan start, TimeSpan end, string text)
    {
        Start = start;
        End = end;
        Text = text;
    }

    /// <summary>
    /// Gets where the segment starts, measured from the start of the media.
    /// </summary>
    public TimeSpan Start { get; }

    /// <summary>
    /// Gets where the segment ends, measured from the start of the media.
    /// </summary>
    public TimeSpan End { get; }

    /// <summary>
    /// Gets what was said.
    /// </summary>
    public string Text { get; }
}
