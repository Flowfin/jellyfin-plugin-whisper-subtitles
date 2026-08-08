using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// What a backend's language code became, and what the server will make of it.
/// </summary>
/// <remarks>
/// Three vocabularies meet here and the type carries two of them apart on purpose.
/// <see cref="FileCode"/> is what goes between two dots in a file name;
/// <see cref="ServerLanguage"/> is what the server stores on the stream once it has
/// read that name back. They are usually the same string and they are not the same
/// fact, and a caller that wants to tell an operator what a library will show has
/// to say the second one.
/// </remarks>
public sealed class LanguageCodeMapping
{
    private LanguageCodeMapping(
        LanguageCodeOutcome outcome,
        string? fileCode,
        string? serverLanguage,
        string reason)
    {
        Outcome = outcome;
        FileCode = fileCode;
        ServerLanguage = serverLanguage;
        Reason = reason;
    }

    /// <summary>
    /// Gets what was decided.
    /// </summary>
    public LanguageCodeOutcome Outcome { get; }

    /// <summary>
    /// Gets the code a file name may carry, or null where nothing may be written.
    /// </summary>
    public string? FileCode { get; }

    /// <summary>
    /// Gets the language the server stores when it reads that name back, or null
    /// where nothing may be written.
    /// </summary>
    public string? ServerLanguage { get; }

    /// <summary>
    /// Gets the sentence an operator reads.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets a value indicating whether a subtitle may be named on this mapping.
    /// </summary>
    public bool MayWrite => FileCode is not null;

    /// <summary>
    /// The code names a language the server resolves.
    /// </summary>
    /// <param name="fileCode">The code the file name carries.</param>
    /// <param name="serverLanguage">What the server stores having read it.</param>
    /// <returns>The mapping.</returns>
    public static LanguageCodeMapping Mapped(string fileCode, string serverLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverLanguage);

        return new LanguageCodeMapping(
            LanguageCodeOutcome.Mapped,
            fileCode,
            serverLanguage,
            string.Format(
                CultureInfo.InvariantCulture,
                "The file name carries {0}, which the server reads back as {1}.",
                fileCode,
                serverLanguage));
    }

    /// <summary>
    /// The string is not a language code.
    /// </summary>
    /// <param name="given">What arrived, for the operator to recognise.</param>
    /// <returns>The mapping.</returns>
    public static LanguageCodeMapping NotALanguageCode(string given) =>
        new(
            LanguageCodeOutcome.NotALanguageCode,
            null,
            null,
            string.Format(
                CultureInfo.InvariantCulture,
                "'{0}' is not a language code, so no subtitle was named. A code is two or three letters, as in de or deu.",
                given));

    /// <summary>
    /// The code is one and the server has no language under it.
    /// </summary>
    /// <param name="given">The code that resolves to nothing.</param>
    /// <returns>The mapping.</returns>
    /// <remarks>
    /// Named as a fact about the server rather than about the code, because the code
    /// is usually right and the operator can do nothing about the table it is missing
    /// from.
    /// </remarks>
    public static LanguageCodeMapping NoLanguageOnTheServer(string given) =>
        new(
            LanguageCodeOutcome.NoLanguageOnTheServer,
            null,
            null,
            string.Format(
                CultureInfo.InvariantCulture,
                "The server resolves no language from '{0}', so a subtitle named with it would arrive with no language at all. Nothing was written.",
                given));
}
