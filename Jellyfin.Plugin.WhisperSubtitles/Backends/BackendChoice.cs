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
        : this(backend, outcome, reason, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendChoice"/> class
    /// carrying the readiness answer the selection read.
    /// </summary>
    /// <param name="backend">The backend to use.</param>
    /// <param name="outcome">Why it is that one.</param>
    /// <param name="reason">What the operator is told where it is not the configured one.</param>
    /// <param name="readiness">The readiness answer the selected backend gave, or null where none was asked.</param>
    public BackendChoice(ITranscriptionBackend backend, BackendSelectionOutcome outcome, string reason, BackendReadiness? readiness)
    {
        Backend = backend;
        Outcome = outcome;
        Reason = reason;
        Readiness = readiness;
    }

    /// <summary>
    /// Gets the readiness answer the selected backend gave, or null where the
    /// selection fell back before asking one.
    /// </summary>
    /// <remarks>
    /// Carried so the page can show what the tool said about itself without
    /// asking the backend a second time, which would run the tool twice for one
    /// press of the button.
    /// </remarks>
    public BackendReadiness? Readiness { get; }

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
