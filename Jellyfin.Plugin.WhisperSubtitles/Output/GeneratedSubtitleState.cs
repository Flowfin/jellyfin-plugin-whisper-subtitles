namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// What became of one file the record names, as a listing found it.
/// </summary>
public enum GeneratedSubtitleState
{
    /// <summary>
    /// The bytes are the ones this plugin wrote, so the file may be offered for removal.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The bytes differ from what this plugin wrote, so the file is somebody's work and is kept.
    /// </summary>
    Edited,

    /// <summary>
    /// Nothing is at the path any more.
    /// </summary>
    Gone,
}
