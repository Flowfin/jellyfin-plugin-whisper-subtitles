namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// A subtitle that is on a disk, and where it went.
/// </summary>
/// <remarks>
/// The path is carried out rather than recomputed by whoever wants it. Telling the
/// library the file arrived is #29 and removing what this plugin wrote is #43, and
/// both need the path this write actually used; deriving it a second time is how
/// two answers to one question come to disagree.
/// </remarks>
public sealed class WrittenSubtitle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WrittenSubtitle"/> class.
    /// </summary>
    /// <param name="path">The file, under the name a reader opens.</param>
    /// <param name="kind">Which of the two places it went.</param>
    public WrittenSubtitle(string path, SubtitleDestinationKind kind)
    {
        Path = path;
        Kind = kind;
    }

    /// <summary>
    /// Gets the file, under the name a reader opens.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets which of the two places it went.
    /// </summary>
    public SubtitleDestinationKind Kind { get; }
}
