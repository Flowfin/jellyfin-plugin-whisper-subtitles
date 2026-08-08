using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.ExternalFiles;
using Jellyfin.Plugin.WhisperSubtitles.Detection;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using MediaBrowser.Model.Dlna;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The language a generated subtitle is written under has to survive a round trip
/// through somebody else's reader, so these assertions run the server's own parser
/// over the names this plugin builds, against rows copied out of the server's own
/// culture file.
/// </summary>
/// <remarks>
/// A mapping that is internally consistent and disagrees with that parser is the
/// failure this is written against, and it is not hypothetical: the double these
/// tests share carried German with its two three letter codes the wrong way round,
/// and the round trip that existed passed because both sides were the same
/// opinion. So nothing here compares the plugin against itself. The parser is the
/// server's, the rows are the server's, and the only thing this repository
/// contributes is the code it puts in the name.
/// </remarks>
public class LanguageCodeTests
{
    private const string MediaPath = "/media/Films/Arrival (2016)/Arrival (2016).mkv";

    private static readonly NamingOptions _namingOptions = new();

    /// <summary>
    /// The cases worth a row of their own, being the ones where the obvious answer
    /// is wrong.
    /// </summary>
    public static TheoryData<string, string, string> AwkwardCases =>
        new()
        {
            // Chinese: script matters and region does not, and the server has a row
            // for each script carrying a hyphen. This plugin writes the plain code
            // and adds no script, so the library shows Chinese rather than a variant
            // nothing asked for.
            { "zh", "zho", "zho" },
            { "chi", "zho", "zho" },
            { "zho", "zho", "zho" },

            // Portuguese: the server carries Brazil and Portugal as their own rows
            // and they are the two rows the supported lines disagree about. The
            // plain code is the same on both.
            { "pt", "por", "por" },
            { "por", "por", "por" },

            // Hebrew and Indonesian: ISO 639-1 moved both, and the codes they moved
            // from resolve to nothing on the server.
            { "he", "heb", "heb" },
            { "iw", "heb", "heb" },
            { "id", "ind", "ind" },
            { "in", "ind", "ind" },

            // Hindi: hi is also one of the server's hearing impaired flags, and the
            // three letter code is not.
            { "hi", "hin", "hin" },
            { "hin", "hin", "hin" },

            // Yiddish and Javanese: the same shape. ji was reassigned, and jw is not
            // an ISO code at all, it is what Whisper answers.
            { "ji", "yid", "yid" },
            { "jw", "jav", "jav" },

            // Moldavian was withdrawn into Romanian.
            { "mo", "ron", "ron" },

            // Greek is the one language the server does not store under a code. Its
            // English name carries a hyphen, and the parser keeps the name wherever
            // it does.
            { "el", "ell", "Greek, Modern (1453-)" },
            { "gre", "ell", "Greek, Modern (1453-)" },

            // Case and blanks are not decisions.
            { "EN", "eng", "eng" },
            { "  de  ", "deu", "deu" },
        };

    /// <summary>
    /// Strings that are not language codes, whatever they were meant to be.
    /// </summary>
    public static TheoryData<string> NotACode =>
        new(string.Empty, "  ", "e", "engl", "en.forced", "en/de", "..", "../../etc", "e n", "e1", "dé");

    /// <summary>
    /// Codes a backend really answers with that the server resolves no language
    /// from.
    /// </summary>
    public static TheoryData<string> NoLanguageOnTheServer =>
        new("haw", "yue", "qqq", "zzz");

    public static TheoryData<string> EveryAcceptedCode =>
        new(SubtitleLanguageCode.AcceptedCodes.ToArray());

    [Theory]
    [MemberData(nameof(AwkwardCases))]
    public void The_awkward_codes_map_to_what_the_server_resolves(
        string given,
        string expectedFileCode,
        string expectedServerLanguage)
    {
        var mapping = SubtitleLanguageCode.For(given);

        Assert.Equal(LanguageCodeOutcome.Mapped, mapping.Outcome);
        Assert.Equal(expectedFileCode, mapping.FileCode);
        Assert.Equal(expectedServerLanguage, mapping.ServerLanguage);
        Assert.True(mapping.MayWrite);
    }

    [Theory]
    [MemberData(nameof(AwkwardCases))]
    public void The_awkward_codes_survive_the_round_trip(
        string given,
        string expectedFileCode,
        string expectedServerLanguage)
    {
        var mapping = SubtitleLanguageCode.For(given);

        var parsed = ParseAsTheServerWould(MediaPath, mapping.FileCode!);

        Assert.Equal(expectedFileCode, mapping.FileCode);
        Assert.Equal(expectedServerLanguage, parsed.Language);
    }

    [Theory]
    [MemberData(nameof(EveryAcceptedCode))]
    public void Every_name_this_plugin_builds_parses_back_to_the_language_it_meant(string given)
    {
        // The clause this whole file is for, and it runs over the mapping's whole
        // domain rather than over a sample, because a table is exactly the thing
        // whose hundredth row is the wrong one.
        var mapping = SubtitleLanguageCode.For(given);

        Assert.True(mapping.MayWrite, given + " is an accepted code and produced nothing: " + mapping.Reason);

        var parsed = ParseAsTheServerWould(MediaPath, mapping.FileCode!);

        Assert.Equal(mapping.ServerLanguage, parsed.Language);

        // The marker has to arrive as the track title rather than as a flag or as
        // part of the language, or the name would parse back to the right language
        // and tell a viewer nothing.
        Assert.Equal(GeneratedSubtitleName.Marker, parsed.Title);
        Assert.False(parsed.IsForced);
        Assert.False(parsed.IsDefault);
        Assert.False(parsed.IsHearingImpaired);
    }

    [Theory]
    [MemberData(nameof(EveryAcceptedCode))]
    public void Every_code_this_plugin_writes_is_one_the_server_carries(string given)
    {
        // A second reading of the same rows, and not the same assertion. The one
        // above asks what the parser answers; this asks whether the row it answered
        // from is the row for the language the code was given for. A mapping that
        // sent Swedish to the Slovenian row would pass the round trip and fail here.
        var mapping = SubtitleLanguageCode.For(given);

        var row = ServerRows().Single(r => r.ThreeLetterCodes.Contains(mapping.FileCode!, StringComparer.Ordinal));

        Assert.Equal(mapping.FileCode, row.ThreeLetterCodes[0]);

        var lower = given.Trim().ToLowerInvariant();
        if (lower.Length == 2 && row.TwoLetterCode.Length == 2)
        {
            // Where the code given is a two letter one the server itself carries,
            // the row it belongs to is the row this mapping chose. The codes the
            // server dropped or never had are the exception and they are the rows
            // below.
            Assert.True(
                string.Equals(lower, row.TwoLetterCode, StringComparison.Ordinal)
                || IsACodeTheServerHasNoRowFor(lower),
                lower + " was mapped onto the row for " + row.EnglishName + ", whose own two letter code is " + row.TwoLetterCode);
        }
    }

    [Theory]
    [MemberData(nameof(NotACode))]
    public void A_string_that_is_not_a_code_is_refused_and_names_no_file(string given)
    {
        var mapping = SubtitleLanguageCode.For(given);

        Assert.Equal(LanguageCodeOutcome.NotALanguageCode, mapping.Outcome);
        Assert.Null(mapping.FileCode);
        Assert.Null(mapping.ServerLanguage);
        Assert.False(mapping.MayWrite);
        Assert.Contains("not a language code", mapping.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(NoLanguageOnTheServer))]
    public void A_code_the_server_has_no_language_for_is_refused_and_names_no_file(string given)
    {
        var mapping = SubtitleLanguageCode.For(given);

        Assert.Equal(LanguageCodeOutcome.NoLanguageOnTheServer, mapping.Outcome);
        Assert.Null(mapping.FileCode);
        Assert.False(mapping.MayWrite);

        // And the refusal is the server's answer rather than this plugin's opinion
        // of it: the same code put through the same parser resolves nothing.
        var parsed = ParseAsTheServerWould(MediaPath, given);

        Assert.Null(parsed.Language);
    }

    [Fact]
    public void Hawaiian_is_refused_because_the_server_drops_the_row_it_has()
    {
        // The sharpest of the refusals and the one most likely to be read as a gap
        // in the copied rows. The server's culture file HAS a row for Hawaiian; its
        // loader throws the row away because it carries no two letter code, so no
        // file name can name Hawaiian on either supported line.
        var row = File.ReadAllLines(LanguagesOnly.FixturePath)
            .Single(line => line.StartsWith("haw|", StringComparison.Ordinal));

        Assert.Equal(string.Empty, row.Split('|')[2]);
        Assert.DoesNotContain(ServerRows(), r => r.ThreeLetterCodes.Contains("haw", StringComparer.Ordinal));
        Assert.Equal(LanguageCodeOutcome.NoLanguageOnTheServer, SubtitleLanguageCode.For("haw").Outcome);
    }

    [Fact]
    public void The_name_builder_alone_would_have_accepted_a_code_that_resolves_to_nothing()
    {
        // What this mapping is worth. The name builder bounds the shape of the code
        // so nothing can reach the file system through it, and a shape is all it
        // bounds: iw is two letters and all of them are letters, so it passes, and
        // the file it names arrives in the library with no language on it at all.
        var named = GeneratedSubtitleName.For(MediaPath, "iw", "srt");

        Assert.Contains(".iw.", named, StringComparison.Ordinal);
        Assert.Null(ParseAsTheServerWould(MediaPath, "iw").Language);

        var mapping = SubtitleLanguageCode.For("iw");

        Assert.Equal("heb", mapping.FileCode);
        Assert.Equal("heb", ParseAsTheServerWould(MediaPath, mapping.FileCode!).Language);
    }

    [Fact]
    public void The_rows_the_round_trip_is_judged_against_are_there()
    {
        // Guards every leg above. A missing fixture would make the parser resolve
        // nothing, and the refusal legs would pass for a reason that has nothing to
        // do with the codes they name.
        var rows = ServerRows();

        Assert.True(rows.Length > 90, $"only {rows.Length} culture rows were loaded from {LanguagesOnly.FixturePath}");
        Assert.Contains(rows, r => string.Equals(r.EnglishName, "German", StringComparison.Ordinal));
        Assert.Equal("deu", rows.Single(r => string.Equals(r.EnglishName, "German", StringComparison.Ordinal)).ThreeLetterCodes[0]);
    }

    [Fact]
    public void Every_code_the_mapping_can_produce_is_a_code_the_name_builder_accepts()
    {
        // The two bounds are written in different files and have to agree, or the
        // mapping hands the name builder something it throws on and the item fails
        // for a reason no operator can act on.
        foreach (var fileCode in SubtitleLanguageCode.FileCodes)
        {
            Assert.Equal(fileCode, GeneratedSubtitleName.Language(fileCode));
        }
    }

    private static bool IsACodeTheServerHasNoRowFor(string code) =>
        code is "iw" or "in" or "ji" or "jw" or "mo";

    private static ServerRow[] ServerRows() =>
        new LanguagesOnly().GetCultures()
            .Select(c => new ServerRow(c.DisplayName, c.TwoLetterISOLanguageName, [.. c.ThreeLetterISOLanguageNames]))
            .ToArray();

    /// <summary>
    /// Runs the name this plugin built through the server's own parser, the way the
    /// server reaches it.
    /// </summary>
    /// <remarks>
    /// The extra string is not the whole name. The server strips the media file's
    /// base name off the front and hands the parser only what is left, in
    /// <c>MediaInfoResolver</c>, so a media file whose own name holds dots does not
    /// have them read as flags.
    /// </remarks>
    private static ExternalPathParserResult ParseAsTheServerWould(string mediaPath, string languageCode)
    {
        var generated = GeneratedSubtitleName.For(mediaPath, languageCode, "srt");

        var prefix = Path.GetFileNameWithoutExtension(mediaPath);
        var extra = Path.GetFileNameWithoutExtension(generated)[prefix.Length..];

        var parser = new ExternalPathParser(_namingOptions, new LanguagesOnly(), DlnaProfileType.Subtitle);

        var parsed = parser.ParseFile(Path.Combine(Path.GetDirectoryName(mediaPath)!, generated), extra);

        Assert.NotNull(parsed);

        return parsed!;
    }

    private sealed record ServerRow(string EnglishName, string TwoLetterCode, string[] ThreeLetterCodes);
}
