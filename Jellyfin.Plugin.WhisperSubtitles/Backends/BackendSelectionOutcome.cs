namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Why selection ended up with the backend it did.
/// </summary>
/// <remarks>
/// Separate values rather than one failure, because a caller has four different
/// things to say to an operator and only one of them is an error. They also have
/// different repairs: one is a typo in a name, one is a setting that was never
/// filled in, one is a machine that is not ready, and one is an install nobody
/// has configured yet.
/// </remarks>
public enum BackendSelectionOutcome
{
    /// <summary>
    /// The configured backend was found, its settings were complete and it
    /// reported itself ready.
    /// </summary>
    Selected,

    /// <summary>
    /// The configuration names no backend at all, which is what a fresh install
    /// looks like.
    /// </summary>
    NothingConfigured,

    /// <summary>
    /// The configuration names a backend this plugin does not have.
    /// </summary>
    UnknownName,

    /// <summary>
    /// The configured backend exists, and a setting it cannot run without is not
    /// filled in.
    /// </summary>
    MissingSetting,

    /// <summary>
    /// The configured backend exists and is fully configured, and its readiness
    /// check says it cannot transcribe right now.
    /// </summary>
    NotReady
}
