namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Which of the two places a subtitle went.
/// </summary>
/// <remarks>
/// Two and not more. Anywhere else is a path the operator did not consent to
/// having files written into, and the choice between these two belongs to the
/// item's library rather than to this plugin.
/// </remarks>
public enum SubtitleDestinationKind
{
    /// <summary>
    /// Next to the media file, in the folder that holds it.
    /// </summary>
    BesideTheMedia,

    /// <summary>
    /// In the item's own metadata folder, inside the server's data directory.
    /// </summary>
    InTheMetadataFolder
}
