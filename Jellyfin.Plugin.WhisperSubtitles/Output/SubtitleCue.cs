using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// One block a viewer sees: the lines, and the window they are on screen for.
/// </summary>
/// <remarks>
/// Not the same thing as a segment. A backend's segment is a stretch of speech
/// and may be forty words long, may overlap the next one and may run for eleven
/// seconds. A cue is what is left after that has been made readable.
/// </remarks>
public sealed class SubtitleCue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleCue"/> class.
    /// </summary>
    /// <param name="start">When the cue appears.</param>
    /// <param name="end">When it goes away.</param>
    /// <param name="lines">The lines, in the order they are shown.</param>
    public SubtitleCue(TimeSpan start, TimeSpan end, IReadOnlyList<string> lines)
    {
        Start = start;
        End = end;
        Lines = lines;
    }

    /// <summary>
    /// Gets when the cue appears.
    /// </summary>
    public TimeSpan Start { get; }

    /// <summary>
    /// Gets when the cue goes away.
    /// </summary>
    public TimeSpan End { get; }

    /// <summary>
    /// Gets the lines, in the order they are shown.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }
}
