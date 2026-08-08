namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// What a sweep found and what it could not remove.
/// </summary>
/// <remarks>
/// Two counts rather than nothing, because a sweep that silently removes files is
/// a sweep nobody can tell from one that ran over the wrong directory and found
/// none. Where these numbers are reported is the run's own lines, which is #73.
/// </remarks>
public sealed class SweepOutcome
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SweepOutcome"/> class.
    /// </summary>
    /// <param name="collected">How many stale files were removed.</param>
    /// <param name="left">How many were found and would not go.</param>
    public SweepOutcome(int collected, int left)
    {
        Collected = collected;
        Left = left;
    }

    /// <summary>
    /// Gets how many stale files were removed.
    /// </summary>
    public int Collected { get; }

    /// <summary>
    /// Gets how many were found and would not go.
    /// </summary>
    /// <remarks>
    /// Not a failure of the run. The usual cause is that something still has the
    /// file open, and the next sweep collects it.
    /// </remarks>
    public int Left { get; }
}
