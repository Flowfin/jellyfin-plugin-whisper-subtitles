using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

// Named for what it holds rather than for the one type in it, so the double that
// arrives with the next thing a name has to be parsed against lands beside this
// one instead of in a second file nobody thinks to look in.
#pragma warning disable SA1649 // File name should match first type name

// Reason is the sentence every refusal above carries. It sits under the members
// it explains because a reader arrives at it from one of them, never the other
// way round.
#pragma warning disable SA1201 // Elements should appear in the correct order

/// <summary>
/// The one collaborator the server's own external subtitle parser needs, cut down
/// to the one question it asks.
/// </summary>
/// <remarks>
/// <c>ExternalPathParser</c> takes an <see cref="ILocalizationManager"/> and uses
/// it to decide whether a part of a file name names a language. Everything else on
/// that interface is about ratings, countries and translated strings, and a parse
/// that reached any of them would be doing something these tests are not about, so
/// those refuse rather than answer.
///
/// The language table here is small and fixed. The server's real one is loaded
/// from a resource file, and a test asserting on a file name has to fail because
/// the name is wrong and never because a resource moved. The rows are shaped the
/// way the real loader shapes them: the display name in the first position, then
/// the two letter code, then every three letter code the language answers to.
///
/// The two lines do not declare the same interface. The newer one adds two
/// members, and they are written under a conditional rather than hidden behind a
/// shim, because a shim would be a place for the lines to drift without the
/// compiler noticing.
/// </remarks>
internal sealed class LanguagesOnly : ILocalizationManager
{
    private static readonly CultureDto[] _cultures =
    [
        new("English", "English", "en", ["eng"]),
        new("German", "German", "de", ["ger", "deu"]),
        new("Japanese", "Japanese", "ja", ["jpn"])
    ];

    public CultureDto? FindLanguageInfo(string language) =>
        Array.Find(
            _cultures,
            c => c.Name.Equals(language, StringComparison.OrdinalIgnoreCase)
                || c.DisplayName.Equals(language, StringComparison.OrdinalIgnoreCase)
                || c.TwoLetterISOLanguageName.Equals(language, StringComparison.OrdinalIgnoreCase)
                || c.ThreeLetterISOLanguageNames.Contains(language, StringComparer.OrdinalIgnoreCase));

    public IEnumerable<CultureDto> GetCultures() => _cultures;

    public IReadOnlyList<CountryInfo> GetCountries() => throw new NotSupportedException(Reason);

    public IReadOnlyList<ParentalRating> GetParentalRatings() => throw new NotSupportedException(Reason);

    public IEnumerable<LocalizationOption> GetLocalizationOptions() => throw new NotSupportedException(Reason);

    public string GetLocalizedString(string phrase) => throw new NotSupportedException(Reason);

    public string GetLocalizedString(string phrase, string culture) => throw new NotSupportedException(Reason);

    public ParentalRatingScore? GetRatingScore(string rating, string? countryCode = null) =>
        throw new NotSupportedException(Reason);

    public bool TryGetISO6392TFromB(string isoB, [NotNullWhen(true)] out string? isoT) =>
        throw new NotSupportedException(Reason);

#if NET10_0_OR_GREATER
    public string GetServerLocalizedString(string phrase) => throw new NotSupportedException(Reason);

    public string? GetLanguageDisplayName(string language) => throw new NotSupportedException(Reason);
#endif

    private static string Reason =>
        "Parsing a subtitle file name asks which part of it is a language, and nothing else.";
}
