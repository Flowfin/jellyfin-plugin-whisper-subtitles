namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// What became of an attempt to turn a backend's language code into one a file
/// name can carry.
/// </summary>
/// <remarks>
/// Two refusals rather than one, because they are different things to tell an
/// operator. A string that is not a code at all is usually a typed setting; a code
/// the server has no language for is a real language this plugin cannot label,
/// and there is nothing to correct.
/// </remarks>
public enum LanguageCodeOutcome
{
    /// <summary>
    /// The code names a language the server resolves, and the file name may be built.
    /// </summary>
    Mapped = 0,

    /// <summary>
    /// The string is not a language code, whatever language it was meant to be.
    /// </summary>
    NotALanguageCode = 1,

    /// <summary>
    /// The string is a language code and the server has no language under it.
    /// </summary>
    /// <remarks>
    /// The server would read a file named with it and find no language, leaving the
    /// code in the track title and the track itself unlabelled. Nothing is written
    /// rather than something written under nothing.
    /// </remarks>
    NoLanguageOnTheServer = 2,
}
