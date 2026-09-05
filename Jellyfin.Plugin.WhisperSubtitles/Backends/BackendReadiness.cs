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
        : this(isReady, reason, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendReadiness"/> class
    /// carrying what the tool said about itself.
    /// </summary>
    /// <param name="isReady">Whether the backend can transcribe right now.</param>
    /// <param name="reason">What stands in the way, or null when nothing does.</param>
    /// <param name="tool">What the tool said about itself, or null where no tool was asked.</param>
    public BackendReadiness(bool isReady, string? reason, Local.ToolIdentity? tool)
    {
        IsReady = isReady;
        Reason = reason;
        Tool = tool;
    }

    /// <summary>
    /// Gets what the tool said about itself, or null where no tool was asked:
    /// the remote backend asks an endpoint and the do-nothing backend asks
    /// nothing.
    /// </summary>
    public Local.ToolIdentity? Tool { get; }

    /// <summary>
    /// Gets a value indicating whether the backend can transcribe right now.
    /// </summary>
    public bool IsReady { get; }

    /// <summary>
    /// Gets what stands in the way, or null when nothing does.
    /// </summary>
    public string? Reason { get; }
}
