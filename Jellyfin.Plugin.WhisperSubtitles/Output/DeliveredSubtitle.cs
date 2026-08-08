namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// A subtitle that is on a disk, and what happened when the server was told.
/// </summary>
/// <remarks>
/// Two facts and they are not the same fact. The file is written or it is not, and
/// that is the item's outcome; the server was told or it was not, and that is a
/// note beside it. Collapsing the two would make an item whose subtitle is
/// finished and correct into a failed item because a metadata refresh did not go
/// through, and the operator would be told to look at the transcription.
/// </remarks>
public sealed class DeliveredSubtitle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveredSubtitle"/> class.
    /// </summary>
    /// <param name="path">The file, under the name a reader opens.</param>
    /// <param name="wasRefreshRequested">Whether the server was successfully asked to look at the item again.</param>
    /// <param name="refreshProblem">What went wrong asking, or null when nothing did.</param>
    public DeliveredSubtitle(string path, bool wasRefreshRequested, string? refreshProblem)
    {
        Path = path;
        WasRefreshRequested = wasRefreshRequested;
        RefreshProblem = refreshProblem;
    }

    /// <summary>
    /// Gets the file, under the name a reader opens.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets a value indicating whether the server was successfully asked to look at the item again.
    /// </summary>
    /// <remarks>
    /// False does not mean the subtitle is missing. It means the new file will
    /// become selectable when the server next looks at the item on its own, which
    /// is later than it should have been rather than never.
    /// </remarks>
    public bool WasRefreshRequested { get; }

    /// <summary>
    /// Gets what went wrong asking, or null when nothing did.
    /// </summary>
    public string? RefreshProblem { get; }
}
