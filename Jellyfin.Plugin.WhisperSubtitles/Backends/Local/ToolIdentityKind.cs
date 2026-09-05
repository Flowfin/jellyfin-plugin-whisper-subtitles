namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// What the tool said about itself when the probe asked, which is one of three
/// things and never a guess.
/// </summary>
public enum ToolIdentityKind
{
    /// <summary>
    /// The tool exited cleanly from <c>--version</c> and printed a line, which is reported as its version.
    /// </summary>
    Version,

    /// <summary>
    /// The tool printed no version, and the first line it printed to <c>--help</c> is reported as its own description.
    /// </summary>
    Description,

    /// <summary>
    /// The tool printed nothing to either flag. It is there and silent.
    /// </summary>
    Silent,
}
