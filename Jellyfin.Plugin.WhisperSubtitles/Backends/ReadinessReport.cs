namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// What the configuration page is told when it asks whether the backend an
/// operator has chosen can be used right now.
/// </summary>
/// <remarks>
/// One backend, one answer and one sentence. The sentence is the one selection
/// would hand a run for the same settings, so what the page shows before a save
/// is what a run would have reported after one, and the two cannot say different
/// things about one state.
///
/// A ready answer means what the backend's own probe means by it and no more.
/// For the local backend it means both paths hold files this plugin could hand
/// to a run; for the remote backend it means the host answered and did not
/// refuse the key. Nothing has been transcribed to produce it, and each probe
/// says so in its own remarks rather than letting a green answer imply otherwise.
/// </remarks>
public sealed class ReadinessReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReadinessReport"/> class.
    /// </summary>
    /// <param name="backend">The name of the backend the question was about.</param>
    /// <param name="isReady">Whether that backend can be used right now.</param>
    /// <param name="reason">What stands in the way, or null when nothing does.</param>
    public ReadinessReport(string backend, bool isReady, string? reason)
    {
        Backend = backend;
        IsReady = isReady;
        Reason = reason;
    }

    /// <summary>
    /// Gets the name of the backend the question was about, as this plugin
    /// spells it.
    /// </summary>
    public string Backend { get; }

    /// <summary>
    /// Gets a value indicating whether that backend can be used right now.
    /// </summary>
    public bool IsReady { get; }

    /// <summary>
    /// Gets what stands in the way, or null when nothing does.
    /// </summary>
    public string? Reason { get; }
}
