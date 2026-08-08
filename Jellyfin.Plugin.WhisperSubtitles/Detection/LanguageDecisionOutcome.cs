namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// What was decided about the language a subtitle would be written under.
/// </summary>
/// <remarks>
/// Five values rather than a boolean, because the four ways this ends are four
/// different things to tell an operator. A refusal below the floor is a fact
/// about one item and its audio; a backend that cannot be weighed is a fact
/// about the configuration and will refuse every item until it changes.
/// </remarks>
public enum LanguageDecisionOutcome
{
    /// <summary>
    /// The language was named, so nothing was detected and nothing was weighed.
    /// </summary>
    AsRequested = 0,

    /// <summary>
    /// No language was named and this backend may be asked to detect one.
    /// </summary>
    /// <remarks>
    /// Said before a transcription and never after one. It is permission to ask,
    /// not an answer, and no subtitle may be written on it.
    /// </remarks>
    DetectionMayProceed = 1,

    /// <summary>
    /// A language was detected and its score reached the floor.
    /// </summary>
    DetectionAccepted = 2,

    /// <summary>
    /// A language was detected and its score did not reach the floor.
    /// </summary>
    BelowTheConfidenceFloor = 3,

    /// <summary>
    /// No language was named and this backend reports no confidence in what it detects.
    /// </summary>
    DetectionCannotBeWeighed = 4,
}
