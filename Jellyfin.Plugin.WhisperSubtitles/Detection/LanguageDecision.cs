using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// What the confidence floor decided, and enough of the working to argue with it.
/// </summary>
/// <remarks>
/// A refusal carries the candidate and the score rather than a sentence saying a
/// language was rejected. An operator holding "detected pt at 0.41, floor 0.80"
/// can lower the floor, name the language, or look at the audio. An operator
/// holding "detection was not confident enough" can only guess, and the guess is
/// usually that the plugin is broken.
/// </remarks>
public sealed class LanguageDecision
{
    private LanguageDecision(
        LanguageDecisionOutcome outcome,
        string? writtenLanguage,
        string? candidate,
        double? score,
        string reason)
    {
        Outcome = outcome;
        WrittenLanguage = writtenLanguage;
        Candidate = candidate;
        Score = score;
        Reason = reason;
    }

    /// <summary>
    /// Gets what was decided.
    /// </summary>
    public LanguageDecisionOutcome Outcome { get; }

    /// <summary>
    /// Gets the language a subtitle may be written under, or null when nothing may be written.
    /// </summary>
    /// <remarks>
    /// Null on every outcome but the two acceptances, including on
    /// <see cref="LanguageDecisionOutcome.DetectionMayProceed"/>, which is
    /// permission to ask a backend and not an answer from one.
    /// </remarks>
    public string? WrittenLanguage { get; }

    /// <summary>
    /// Gets the language that was detected, or null when nothing was detected.
    /// </summary>
    public string? Candidate { get; }

    /// <summary>
    /// Gets the score the backend reported for the candidate, or null when it reported none.
    /// </summary>
    public double? Score { get; }

    /// <summary>
    /// Gets the sentence an operator reads.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets a value indicating whether a subtitle may be written on this decision.
    /// </summary>
    public bool MayWrite => WrittenLanguage is not null;

    /// <summary>
    /// The operator named the language, so nothing was detected.
    /// </summary>
    /// <param name="language">The language the request named.</param>
    /// <returns>The decision.</returns>
    public static LanguageDecision AsRequested(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return new LanguageDecision(
            LanguageDecisionOutcome.AsRequested,
            language,
            null,
            null,
            string.Format(
                CultureInfo.InvariantCulture,
                "The language {0} was asked for, so nothing was detected and no confidence was weighed.",
                language));
    }

    /// <summary>
    /// This backend may be asked to detect a language.
    /// </summary>
    /// <param name="backendName">The backend that may be asked.</param>
    /// <returns>The decision.</returns>
    public static LanguageDecision DetectionMayProceed(string backendName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendName);

        return new LanguageDecision(
            LanguageDecisionOutcome.DetectionMayProceed,
            null,
            null,
            null,
            string.Format(
                CultureInfo.InvariantCulture,
                "No language was asked for and {0} reports a confidence with what it detects, so detection may be asked for and weighed afterwards.",
                backendName));
    }

    /// <summary>
    /// A detected language reached the floor.
    /// </summary>
    /// <param name="candidate">The language that was detected.</param>
    /// <param name="score">The score the backend reported for it.</param>
    /// <param name="floor">The floor it reached.</param>
    /// <returns>The decision.</returns>
    public static LanguageDecision DetectionAccepted(string candidate, double score, double floor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        return new LanguageDecision(
            LanguageDecisionOutcome.DetectionAccepted,
            candidate,
            candidate,
            score,
            string.Format(
                CultureInfo.InvariantCulture,
                "Detected {0} at {1}, which reaches the confidence floor of {2}.",
                candidate,
                Format(score),
                Format(floor)));
    }

    /// <summary>
    /// A detected language did not reach the floor, so nothing is written.
    /// </summary>
    /// <param name="candidate">The language that was detected.</param>
    /// <param name="score">The score the backend reported for it.</param>
    /// <param name="floor">The floor it did not reach.</param>
    /// <returns>The decision.</returns>
    public static LanguageDecision BelowTheConfidenceFloor(string candidate, double score, double floor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        return new LanguageDecision(
            LanguageDecisionOutcome.BelowTheConfidenceFloor,
            null,
            candidate,
            score,
            string.Format(
                CultureInfo.InvariantCulture,
                "Detected {0} at {1}, below the confidence floor of {2}, so nothing was written. Name the language for this library or lower the floor.",
                candidate,
                Format(score),
                Format(floor)));
    }

    /// <summary>
    /// This backend reports no confidence, so it is used with a named language only.
    /// </summary>
    /// <param name="backendName">The backend that reports none.</param>
    /// <returns>The decision.</returns>
    public static LanguageDecision DetectionCannotBeWeighed(string backendName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendName);

        return new LanguageDecision(
            LanguageDecisionOutcome.DetectionCannotBeWeighed,
            null,
            null,
            null,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} cannot say how sure it is of a language it would detect, so it is used with a language named for the library rather than with detection. Name one, or select a backend that reports a confidence.",
                backendName));
    }

    /// <summary>
    /// This backend said it reports a confidence and then returned none.
    /// </summary>
    /// <param name="backendName">The backend whose description and result disagree.</param>
    /// <param name="candidate">The language it detected without a score.</param>
    /// <returns>The decision.</returns>
    /// <remarks>
    /// The same outcome as a backend that never claimed to weigh anything, and a
    /// different sentence, because what an operator should do about it is
    /// different. This one is a defect in the backend rather than a setting to
    /// change, and the value that would hide it is a default score of one.
    /// </remarks>
    public static LanguageDecision DetectionCarriedNoScore(string backendName, string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        return new LanguageDecision(
            LanguageDecisionOutcome.DetectionCannotBeWeighed,
            null,
            candidate,
            null,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} offers a confidence with what it detects and returned {1} without one, so there is nothing to weigh and nothing was written.",
                backendName,
                candidate));
    }

    // Two decimals and an invariant culture, so the number in a reason reads the
    // same for every operator and can be compared against the floor they typed.
    // A machine's own formatting would put a comma in it on half the servers this
    // runs on, and a floor of 0,80 is a number nobody can paste back into a form.
    private static string Format(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}
