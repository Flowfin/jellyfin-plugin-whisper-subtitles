namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Where one item's files are, and what its library has said about writing
/// beside them.
/// </summary>
/// <remarks>
/// A flat description rather than a server type, for the same reason
/// <see cref="Selection.ItemDescription"/> is one: the decision about where a
/// subtitle goes has to be answerable and testable without a library, and a test
/// cannot fabricate one out of types that exist only inside a server. Whatever
/// reads the real library fills these in.
///
/// The library option is carried here rather than looked up where the write
/// happens. It belongs to the item's library and not to the plugin, so the type
/// that describes an item is where it can be seen next to the paths it decides
/// between.
/// </remarks>
public sealed class ItemLocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemLocation"/> class.
    /// </summary>
    /// <param name="mediaFilePath">The media file itself.</param>
    /// <param name="metadataFolderPath">The item's own metadata folder, inside the server's data directory.</param>
    /// <param name="saveSubtitlesWithMedia">What the item's library has said about writing subtitles beside the media.</param>
    public ItemLocation(string mediaFilePath, string metadataFolderPath, bool saveSubtitlesWithMedia)
    {
        MediaFilePath = mediaFilePath;
        MetadataFolderPath = metadataFolderPath;
        SaveSubtitlesWithMedia = saveSubtitlesWithMedia;
    }

    /// <summary>
    /// Gets the media file itself.
    /// </summary>
    /// <remarks>
    /// The file and not its folder. What a subtitle has to be named after is the
    /// media file, and the folder is derived from it here rather than passed in
    /// beside it, so the two cannot arrive disagreeing.
    /// </remarks>
    public string MediaFilePath { get; }

    /// <summary>
    /// Gets the item's own metadata folder, inside the server's data directory.
    /// </summary>
    public string MetadataFolderPath { get; }

    /// <summary>
    /// Gets a value indicating whether the item's library has said subtitles may be written beside the media.
    /// </summary>
    /// <remarks>
    /// The server's own subtitle manager honours this when it saves a downloaded
    /// subtitle, and this plugin honours it for the same reason: an operator who
    /// has told the server not to write into their media tree has said something
    /// the plugin does not get to overrule.
    /// </remarks>
    public bool SaveSubtitlesWithMedia { get; }
}
