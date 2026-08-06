namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// What selection settled on, and why.
/// </summary>
public sealed class BackendChoice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackendChoice"/> class.
    /// </summary>
    /// <param name="backend">The backend to use, which is the do-nothing one unless the outcome is <see cref="BackendSelectionOutcome.Selected"/>.</param>
    /// <param name="outcome">Why this is the backend.</param>
    /// <param name="reason">The one line an operator is shown, naming the value that could not be honoured.</param>
    public BackendChoice(ITranscriptionBackend backend, BackendSelectionOutcome outcome, string reason)
    {
        Backend = backend;
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>
    /// Gets the backend to use.
    /// </summary>
    public ITranscriptionBackend Backend { get; }

    /// <summary>
    /// Gets why this is the backend.
    /// </summary>
    public BackendSelectionOutcome Outcome { get; }

    /// <summary>
    /// Gets the one line an operator is shown, naming the value that could not be
    /// honoured.
    /// </summary>
    /// <remarks>
    /// The caller logs it and the configuration page shows it. It is a sentence
    /// rather than a code because it is read by a person, and the code beside it
    /// is <see cref="Outcome"/>, which is what anything else branches on.
    /// </remarks>
    public string Reason { get; }
}
