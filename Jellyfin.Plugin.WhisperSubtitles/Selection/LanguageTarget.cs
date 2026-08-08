using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Selection;

/// <summary>
/// What an operator can ask for as the language of a generated subtitle.
/// </summary>
/// <remarks>
/// One string rather than a language beside a flag saying whether to detect one.
/// Two fields for one decision is a shape that can hold a contradiction, and the
/// contradiction has to be resolved somewhere by a rule nobody wrote down: a
/// configuration carrying both German and detect is a question this plugin cannot
/// answer and would have to guess at.
///
/// The reserved word cannot collide with a language, and that is a property rather
/// than a hope. ISO 639 codes are two or three letters; this is six, so no code a
/// server, a container or a backend uses can be read as the request to detect one.
/// </remarks>
public static class LanguageTarget
{
    /// <summary>
    /// The request to let the backend say what language the audio is in.
    /// </summary>
    public const string Detect = "detect";

    /// <summary>
    /// Whether the target asks for detection.
    /// </summary>
    /// <param name="target">The configured target.</param>
    /// <returns>True where the backend is being asked to decide.</returns>
    public static bool IsDetection(string? target) =>
        target is not null && Detect.Equals(target.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether nothing has been asked for at all.
    /// </summary>
    /// <param name="target">The configured target.</param>
    /// <returns>True where there is no target.</returns>
    /// <remarks>
    /// Blank is not detection. An operator who has not chosen yet and an operator
    /// who chose detection are in different states, and running a library into
    /// whatever a backend guesses because a field was left empty is the failure
    /// this distinction exists against.
    /// </remarks>
    public static bool IsAbsent(string? target) => string.IsNullOrWhiteSpace(target);
}
