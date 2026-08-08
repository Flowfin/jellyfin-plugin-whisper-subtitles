using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
/// The rows come out of the server's own culture file rather than being written
/// here, and <c>Fixtures/server-cultures/README.md</c> says which file, at which
/// refs, and what was left out. They are read the way <c>LocalizationManager</c>
/// reads them, below, because the loader is where two of the answers are decided:
/// which of a language's three letter codes comes first, and that a row with no two
/// letter code is dropped altogether.
///
/// This used to be three rows written by hand, and one of them was wrong in a way
/// no test could see. German was given as <c>["ger", "deu"]</c>, so a file named
/// for German parsed back to <c>ger</c>, and a real server stores <c>deu</c>: the
/// file holds the terminological code first and the bibliographic one second, and
/// the parser keeps the first. A hand written table is an opinion about somebody
/// else's data, and a round trip that compares an opinion against itself passes
/// whatever the server does.
///
/// The two lines do not declare the same interface. The newer one adds two
/// members, and they are written under a conditional rather than hidden behind a
/// shim, because a shim would be a place for the lines to drift without the
/// compiler noticing.
/// </remarks>
internal sealed class LanguagesOnly : ILocalizationManager
{
    private static readonly CultureDto[] _cultures = LoadCultures();

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

    /// <summary>
    /// Where the rows are, so a test that wants to read them for itself does not
    /// have to know how they were copied in.
    /// </summary>
    internal static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "server-cultures", "cultures.txt");

    private static string Reason =>
        "Parsing a subtitle file name asks which part of it is a language, and nothing else.";

    // Every line of this is a decision LocalizationManager.LoadCultures makes, and
    // each one changes an answer the parser gives. Five fields or the row is not a
    // row. A blank display name or a blank two letter code and the row never enters
    // the table, which is the only reason Hawaiian cannot be named on either
    // supported line. The name is the display name unless the two letter field
    // carries a hyphen, and the parser keeps that name rather than a code wherever
    // it does. The terminological code is first and the bibliographic one second,
    // and the parser answers with the first.
    private static CultureDto[] LoadCultures()
    {
        var path = FixturePath;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "The server culture rows were not copied next to the test assembly, so every name would parse back to nothing.",
                path);
        }

        var cultures = new List<CultureDto>();

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"'{line}' is not a culture row.");
            }

            var displayName = parts[3];
            var twoLetter = parts[2];
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(twoLetter))
            {
                continue;
            }

            var name = twoLetter.Contains('-', StringComparison.Ordinal) ? twoLetter : displayName;
            string[] threeLetter = string.IsNullOrWhiteSpace(parts[1])
                ? [parts[0]]
                : [parts[0], parts[1]];

            cultures.Add(new CultureDto(name, displayName, twoLetter, threeLetter));
        }

        return [.. cultures];
    }
}
