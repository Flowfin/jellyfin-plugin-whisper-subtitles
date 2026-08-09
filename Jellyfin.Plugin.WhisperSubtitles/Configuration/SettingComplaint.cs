using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// One thing the configuration said that could not be honoured, and what is in
/// force instead.
/// </summary>
/// <remarks>
/// Three parts rather than a sentence, because the three have different readers.
/// The field is what a page highlights. The problem is what an operator has to
/// change. What is in force is the part a reader guesses wrongly if it is left
/// out: a setting that was refused is not a setting that was left alone, and an
/// operator who is not told which value the run will use will assume it is
/// theirs.
/// </remarks>
public sealed class SettingComplaint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingComplaint"/> class.
    /// </summary>
    /// <param name="field">The setting this is about.</param>
    /// <param name="problem">What was wrong with the value it held.</param>
    /// <param name="inForce">What the run uses instead.</param>
    public SettingComplaint(string field, string problem, string inForce)
    {
        Field = field;
        Problem = problem;
        InForce = inForce;
    }

    /// <summary>
    /// Gets the setting this is about.
    /// </summary>
    public string Field { get; }

    /// <summary>
    /// Gets what was wrong with the value it held.
    /// </summary>
    public string Problem { get; }

    /// <summary>
    /// Gets what the run uses instead.
    /// </summary>
    public string InForce { get; }

    /// <summary>
    /// The whole complaint as the one line a log or a page shows.
    /// </summary>
    /// <returns>The field, the reason and what is used instead.</returns>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0}: {1} {2}",
        Field,
        Problem,
        InForce);
}
