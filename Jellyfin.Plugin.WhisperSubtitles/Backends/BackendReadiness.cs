namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Whether a backend can be used right now, and if not, why not.
/// </summary>
public sealed class BackendReadiness
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackendReadiness"/> class.
    /// </summary>
    /// <param name="isReady">Whether the backend can transcribe right now.</param>
    /// <param name="reason">What stands in the way, or null when nothing does.</param>
    public BackendReadiness(bool isReady, string? reason)
    {
        IsReady = isReady;
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the backend can transcribe right now.
    /// </summary>
    public bool IsReady { get; }

    /// <summary>
    /// Gets what stands in the way, or null when nothing does.
    /// </summary>
    public string? Reason { get; }
}
