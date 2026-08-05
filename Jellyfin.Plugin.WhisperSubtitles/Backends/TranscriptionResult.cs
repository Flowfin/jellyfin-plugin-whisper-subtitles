using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// What a backend produced for one piece of media.
/// </summary>
public sealed class TranscriptionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionResult"/> class.
    /// </summary>
    /// <param name="segments">The timed segments, in the order they occur.</param>
    /// <param name="language">The language the segments are in.</param>
    public TranscriptionResult(IReadOnlyList<TimedSegment> segments, string language)
    {
        Segments = segments;
        Language = language;
    }

    /// <summary>
    /// Gets the timed segments, in the order they occur.
    /// </summary>
    public IReadOnlyList<TimedSegment> Segments { get; }

    /// <summary>
    /// Gets the language the segments are in, whether it was asked for or detected.
    /// </summary>
    public string Language { get; }
}
