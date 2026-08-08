using System;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// Whether a language may be detected at all, and whether a detected one is good
/// enough to write under.
/// </summary>
/// <remarks>
/// Pure and asked twice, once on each side of a transcription, because the two
/// refusals cost different amounts. A backend that reports no confidence refuses
/// every item it is asked to detect for, and finding that out before the audio is
/// extracted saves the whole run; a score below the floor is a fact about one
/// item and can only be known afterwards.
///
/// Detection is allowed here rather than discouraged. What is refused is a
/// detected language nobody can weigh, because that is the shape the failure
/// takes: a file labelled as one language containing another, written into the
/// library and believed by everything downstream. A wrong subtitle is harder to
/// find than a missing one.
/// </remarks>
public static class LanguageAcceptance
{
    /// <summary>
    /// Decides what may happen before anything is transcribed.
    /// </summary>
    /// <param name="requestedLanguage">The language the library named, or null to ask the backend to detect one.</param>
    /// <param name="backend">What the backend says it offers.</param>
    /// <returns>The decision, which is never an accepted detection because nothing has been detected yet.</returns>
    /// <remarks>
    /// The condition is what the backend can weigh and not what it can detect. A
    /// backend that detects nothing reports no confidence either, so it lands in
    /// the same refusal, which is the safe direction: nothing may be written under
    /// a language nobody has. That such a backend also refuses the request itself,
    /// as <see cref="Backends.Local.LocalWhisperBackend"/> does, is a second
    /// refusal with a better sentence in it and not a reason to let this one pass.
    /// </remarks>
    public static LanguageDecision BeforeTheRun(string? requestedLanguage, BackendDescription backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        if (!string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return LanguageDecision.AsRequested(requestedLanguage.Trim());
        }

        if (!backend.CanReportLanguageConfidence)
        {
            return LanguageDecision.DetectionCannotBeWeighed(backend.Name);
        }

        return LanguageDecision.DetectionMayProceed(backend.Name);
    }

    /// <summary>
    /// Decides what may be written once the backend has answered.
    /// </summary>
    /// <param name="requestedLanguage">The language the library named, or null when the backend was asked to detect one.</param>
    /// <param name="backend">What the backend says it offers.</param>
    /// <param name="result">What it returned.</param>
    /// <param name="options">The floor a detected language has to reach.</param>
    /// <returns>The decision, and on a refusal the candidate and the score behind it.</returns>
    public static LanguageDecision OnTheResult(
        string? requestedLanguage,
        BackendDescription backend,
        TranscriptionResult result,
        DetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        // The named language wins whatever came back. A backend that echoes the
        // language it was handed, which is what an OpenAI-shaped endpoint does,
        // would otherwise have its echo weighed against a floor as though it were
        // a detection, and an operator who named a language would find items
        // refused for a confidence in a decision they had already made.
        if (!string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return LanguageDecision.AsRequested(requestedLanguage.Trim());
        }

        if (!backend.CanReportLanguageConfidence)
        {
            return LanguageDecision.DetectionCannotBeWeighed(backend.Name);
        }

        if (result.LanguageConfidence is not double score)
        {
            return LanguageDecision.DetectionCarriedNoScore(backend.Name, result.Language);
        }

        // At the floor passes. Written as a comparison against the floor rather
        // than against the floor minus an epsilon, so the number an operator types
        // is the number that decides, and a score reported as exactly that number
        // is not refused by a rounding nobody can see.
        return score >= options.ConfidenceFloor
            ? LanguageDecision.DetectionAccepted(result.Language, score, options.ConfidenceFloor)
            : LanguageDecision.BelowTheConfidenceFloor(result.Language, score, options.ConfidenceFloor);
    }
}
