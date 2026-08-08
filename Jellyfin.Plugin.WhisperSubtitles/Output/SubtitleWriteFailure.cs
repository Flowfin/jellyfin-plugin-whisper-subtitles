namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Why a subtitle that was produced did not reach a file.
/// </summary>
/// <remarks>
/// Separate from <see cref="Attempts.TranscriptionFailureReason"/> and
/// deliberately so. Nothing about the transcription failed in either of these
/// cases: the segments are in hand and the machine that produced them did its
/// work. What is wrong is the destination, and the two want different sentences
/// in front of an operator.
///
/// How these appear in a run's outcome alongside the transcription reasons is
/// #32, which owns the failure vocabulary a run reports.
/// </remarks>
public enum SubtitleWriteFailure
{
    /// <summary>
    /// The destination directory refused the write, or could not be created.
    /// </summary>
    /// <remarks>
    /// A read-only media directory is the ordinary cause and is a normal
    /// configuration rather than a mistake. It is a fact about the destination, so
    /// nothing about the item or the transcription is implied by it.
    /// </remarks>
    DestinationUnwritable,

    /// <summary>
    /// The file name given would have put the file somewhere other than the
    /// destination.
    /// </summary>
    /// <remarks>
    /// A separator, a parent segment or a rooted path in what was meant to be a
    /// file name. Refused rather than trimmed into something plausible: this
    /// plugin writes into two folders, and a name that leaves one of them is not a
    /// name it can repair without guessing where the file was supposed to go.
    /// </remarks>
    NameWouldLeaveTheDestination
}
