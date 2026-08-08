using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// Turns the language code a backend answered with into the one a file name has to
/// carry for the server to read the subtitle back as that language.
/// </summary>
/// <remarks>
/// Three vocabularies have to agree and only one of them belongs to this plugin.
/// A backend answers in its own, usually ISO 639-1; the server resolves a file
/// name through its culture table and keeps the ISO 639-2/T code, or the culture's
/// own name where that name carries a hyphen; and the file name is the only place
/// the two ever meet. So this maps the first onto something the second resolves,
/// and says what the second will make of it.
///
/// Pure, and a table rather than a question put to the server. The server's own
/// <c>ILocalizationManager</c> would answer exactly this, and reaching it means a
/// running server, which would put a language decision behind the one thing every
/// test here refuses to need. The cost is that the table is a copy of a part of
/// somebody else's and can fall behind it. What holds it honest is
/// <c>LanguageCodeTests</c>, which runs every row of this table through the
/// server's own parser against rows copied out of the server's own culture file.
///
/// An unmappable code is a refusal and never a guess. A file named with a code the
/// server resolves nothing from arrives in the library with no language on it and
/// the code sitting in the track title, which is worse than the item being left
/// alone: it looks finished.
/// </remarks>
public static class SubtitleLanguageCode
{
    /// <summary>
    /// The two letter code a backend answers with, and the three letter code the
    /// server resolves it to.
    /// </summary>
    /// <remarks>
    /// The value rather than the key is what goes in the file name. Both resolve to
    /// the same language, so passing the two letter code through would work, and the
    /// three letter one is written instead for two reasons: it is the string the
    /// server stores, so the name in a directory listing and the language in a track
    /// list say the same thing, and <c>hi</c> is also one of the server's hearing
    /// impaired flags, so a file name is a better place for <c>hin</c>.
    /// </remarks>
    private static readonly FrozenDictionary<string, string> _byTwoLetterCode = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["af"] = "afr",
        ["am"] = "amh",
        ["ar"] = "ara",
        ["as"] = "asm",
        ["az"] = "aze",
        ["ba"] = "bak",
        ["be"] = "bel",
        ["bn"] = "ben",
        ["bo"] = "bod",
        ["bs"] = "bos",
        ["br"] = "bre",
        ["bg"] = "bul",
        ["ca"] = "cat",
        ["cs"] = "ces",
        ["cy"] = "cym",
        ["da"] = "dan",
        ["de"] = "deu",
        ["el"] = "ell",
        ["en"] = "eng",
        ["et"] = "est",
        ["eu"] = "eus",
        ["fo"] = "fao",
        ["fa"] = "fas",
        ["fi"] = "fin",
        ["fr"] = "fra",
        ["gl"] = "glg",
        ["gu"] = "guj",
        ["ht"] = "hat",
        ["ha"] = "hau",
        ["he"] = "heb",
        ["hi"] = "hin",
        ["hr"] = "hrv",
        ["hu"] = "hun",
        ["hy"] = "hye",
        ["is"] = "isl",
        ["id"] = "ind",
        ["it"] = "ita",
        ["jv"] = "jav",
        ["ja"] = "jpn",
        ["kn"] = "kan",
        ["ka"] = "kat",
        ["kk"] = "kaz",
        ["km"] = "khm",
        ["ko"] = "kor",
        ["lo"] = "lao",
        ["la"] = "lat",
        ["lv"] = "lav",
        ["ln"] = "lin",
        ["lt"] = "lit",
        ["lb"] = "ltz",
        ["ml"] = "mal",
        ["mr"] = "mar",
        ["mk"] = "mkd",
        ["mg"] = "mlg",
        ["mt"] = "mlt",
        ["mn"] = "mon",
        ["mi"] = "mri",
        ["ms"] = "msa",
        ["my"] = "mya",
        ["ne"] = "nep",
        ["nl"] = "nld",
        ["nn"] = "nno",
        ["no"] = "nor",
        ["oc"] = "oci",
        ["pa"] = "pan",
        ["pl"] = "pol",
        ["pt"] = "por",
        ["ps"] = "pus",
        ["ro"] = "ron",
        ["ru"] = "rus",
        ["sa"] = "san",
        ["sr"] = "srp",
        ["si"] = "sin",
        ["sk"] = "slk",
        ["sl"] = "slv",
        ["sn"] = "sna",
        ["sd"] = "snd",
        ["so"] = "som",
        ["es"] = "spa",
        ["sq"] = "sqi",
        ["su"] = "sun",
        ["sw"] = "swa",
        ["sv"] = "swe",
        ["ta"] = "tam",
        ["tt"] = "tat",
        ["te"] = "tel",
        ["tg"] = "tgk",
        ["tl"] = "tgl",
        ["th"] = "tha",
        ["tk"] = "tuk",
        ["tr"] = "tur",
        ["uk"] = "ukr",
        ["ur"] = "urd",
        ["uz"] = "uzb",
        ["vi"] = "vie",
        ["yi"] = "yid",
        ["yo"] = "yor",
        ["zh"] = "zho",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Codes that are somebody's real answer and are not what the server resolves,
    /// with the code that means the same language.
    /// </summary>
    /// <remarks>
    /// Two kinds, and neither is a guess.
    ///
    /// The first is ISO 639-2/B, the bibliographic code, which several languages have
    /// beside the terminological one. An operator who types <c>ger</c> means German,
    /// the server resolves it, and this repairs it to <c>deu</c> rather than refusing
    /// it, because the alternative is a setting that used to work being read as a
    /// mistake.
    ///
    /// The second is the codes that moved. ISO 639-1 reassigned Hebrew from
    /// <c>iw</c>, Indonesian from <c>in</c> and Yiddish from <c>ji</c>, so a file
    /// written years ago and a backend written against an older table both still say
    /// the old ones, and the server's table holds neither. <c>jw</c> is the same
    /// shape from the other direction: it is not an ISO code at all and it is what
    /// Whisper answers for Javanese. <c>mo</c> was Moldavian until it was withdrawn
    /// into Romanian.
    /// </remarks>
    private static readonly FrozenDictionary<string, string> _byCodeTheServerDroppedOrNeverHad = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tib"] = "bod",
        ["cze"] = "ces",
        ["wel"] = "cym",
        ["ger"] = "deu",
        ["gre"] = "ell",
        ["baq"] = "eus",
        ["per"] = "fas",
        ["fre"] = "fra",
        ["arm"] = "hye",
        ["ice"] = "isl",
        ["geo"] = "kat",
        ["mac"] = "mkd",
        ["mao"] = "mri",
        ["may"] = "msa",
        ["bur"] = "mya",
        ["dut"] = "nld",
        ["rum"] = "ron",
        ["slo"] = "slk",
        ["alb"] = "sqi",
        ["chi"] = "zho",
        ["iw"] = "heb",
        ["in"] = "ind",
        ["ji"] = "yid",
        ["jw"] = "jav",
        ["mo"] = "ron",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The one language the server does not store under its own three letter code.
    /// </summary>
    /// <remarks>
    /// The server keeps the culture's name where that name carries a hyphen, and the
    /// English name of modern Greek is "Greek, Modern (1453-)", whose closing
    /// parenthesis follows one. So a subtitle named <c>ell</c> arrives in the library
    /// under that whole sentence rather than under <c>ell</c>, on both supported
    /// lines, and nothing this plugin writes changes it. Recorded here so a caller
    /// telling an operator what their library will show says the true thing.
    /// </remarks>
    private static readonly FrozenDictionary<string, string> _serverStoresSomethingElse = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ell"] = "Greek, Modern (1453-)",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> _fileCodes =
        _byTwoLetterCode.Values.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets every code this plugin will write into a file name.
    /// </summary>
    /// <remarks>
    /// The set rather than the mapping, because it is what a caller checking a code
    /// it already holds needs, and because it is what the round trip runs over.
    /// </remarks>
    public static IReadOnlyCollection<string> FileCodes => _fileCodes;

    /// <summary>
    /// Gets every code a backend or a configuration may answer with.
    /// </summary>
    public static IReadOnlyCollection<string> AcceptedCodes =>
        _byTwoLetterCode.Keys
            .Concat(_byCodeTheServerDroppedOrNeverHad.Keys)
            .Concat(_fileCodes)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Maps a language code onto the one a file name may carry.
    /// </summary>
    /// <param name="languageCode">Whatever the backend or the configuration said.</param>
    /// <returns>The mapping, carrying its reason where nothing may be written.</returns>
    /// <remarks>
    /// Case and surrounding blanks are not a decision. A backend answering
    /// <c>EN</c> and one answering <c>en</c> mean the same language, and the server
    /// compares case insensitively itself, so normalising is not the plugin
    /// inventing tolerance. Everything past that is a lookup: a code not in one of
    /// the three tables is refused rather than passed through in the hope that the
    /// server knows it.
    /// </remarks>
    public static LanguageCodeMapping For(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return LanguageCodeMapping.NotALanguageCode(languageCode ?? string.Empty);
        }

        var given = languageCode.Trim();
        var normalised = given.ToLowerInvariant();

        // The shape is refused before the tables are read, so a string carrying a
        // separator or a traversal sequence is answered as what it is rather than as
        // a language nobody has. The bound is the same one the file name builder
        // holds, and it is checked here as well because this is the earlier of the
        // two and the one that has a sentence to give back.
        if (normalised.Length is < 2 or > 3
            || !normalised.All(static c => c is >= 'a' and <= 'z'))
        {
            return LanguageCodeMapping.NotALanguageCode(given);
        }

        if (_byTwoLetterCode.TryGetValue(normalised, out var fromTwoLetter))
        {
            return Mapped(fromTwoLetter);
        }

        if (_byCodeTheServerDroppedOrNeverHad.TryGetValue(normalised, out var repaired))
        {
            return Mapped(repaired);
        }

        if (_fileCodes.Contains(normalised))
        {
            return Mapped(normalised);
        }

        return LanguageCodeMapping.NoLanguageOnTheServer(given);
    }

    private static LanguageCodeMapping Mapped(string fileCode) =>
        LanguageCodeMapping.Mapped(
            fileCode,
            _serverStoresSomethingElse.TryGetValue(fileCode, out var stored) ? stored : fileCode);
}
