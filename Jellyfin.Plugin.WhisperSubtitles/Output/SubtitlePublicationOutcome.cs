namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The states an attempt to publish a subtitle can end in without anything having
/// gone wrong.
/// </summary>
/// <remarks>
/// Two, and there is no third. A fault is an exception rather than a value here,
/// because a value nobody looks at is how a fault becomes a silent one, and a
/// list of outcomes that grows to hold faults ends up being read as the whole
/// story of a run when it is not.
/// </remarks>
public enum SubtitlePublicationOutcome
{
    /// <summary>
    /// The subtitle was written and carries its final name.
    /// </summary>
    Written,

    /// <summary>
    /// A file was already at the destination, so nothing was written, nothing was
    /// removed, and the file that was there was not opened for writing.
    /// </summary>
    SkippedTargetExists
}
