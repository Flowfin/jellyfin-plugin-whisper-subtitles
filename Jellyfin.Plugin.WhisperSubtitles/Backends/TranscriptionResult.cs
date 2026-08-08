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
    /// <param name="languageConfidence">How sure the backend is of a language it detected, or null when it reports none.</param>
    public TranscriptionResult(
        IReadOnlyList<TimedSegment> segments,
        string language,
        double? languageConfidence = null)
    {
        Segments = segments;
        Language = language;
        LanguageConfidence = languageConfidence;
    }

    /// <summary>
    /// Gets the timed segments, in the order they occur.
    /// </summary>
    public IReadOnlyList<TimedSegment> Segments { get; }

    /// <summary>
    /// Gets the language the segments are in, whether it was asked for or detected.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets how sure the backend is of a language it detected, between zero and
    /// one, or null when it reports no confidence.
    /// </summary>
    /// <remarks>
    /// Null is a real answer and not a missing one. A backend that detects a
    /// language and cannot say how sure it is has told the plugin something, and
    /// <see cref="Detection.LanguageAcceptance"/> is where that answer is acted
    /// on. Defaulting it to one here would turn "I cannot say" into "I am
    /// certain", which is the failure the floor in #31 exists against.
    ///
    /// Nothing is claimed about what the number means across backends. It is the
    /// score the backend reported, compared against a floor the operator set for
    /// the backend they chose, and it is not a probability this plugin computed.
    /// </remarks>
    public double? LanguageConfidence { get; }
}
