using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Emby.Naming.Common;
using Emby.Naming.ExternalFiles;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using MediaBrowser.Model.Dlna;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A viewer is told a machine wrote the subtitle by the file's name, and the name
/// is read by the server rather than by this plugin. So the assertions here run
/// the server's own parser over the names this plugin builds: what a client shows
/// in a track list is whatever that parser has left over, and nothing in this
/// repository decides it.
/// </summary>
public class MachineMadeMarkerTests
{
    private static readonly NamingOptions _namingOptions = new();

    private static readonly IReadOnlyList<TimedSegment> _segments =
    [
        new TimedSegment(TimeSpan.Zero, TimeSpan.FromMilliseconds(1500), "First line."),
        new TimedSegment(TimeSpan.FromMilliseconds(1500), new TimeSpan(0, 1, 2, 3, 456), "Second line.")
    ];

    [Theory]
    [InlineData("/media/Films/Arrival (2016)/Arrival (2016).mkv", "en", "eng")]
    [InlineData("/media/Films/Arrival (2016)/Arrival (2016).mkv", "eng", "eng")]
    // German has two three letter codes and the server keeps the first one its
    // culture file lists, which is deu. This row said ger until the double behind
    // it stopped being written by hand.
    [InlineData("/media/Films/Das Boot (1981)/Das Boot (1981).mp4", "de", "deu")]
    [InlineData("/media/Films/Das Boot (1981)/Das Boot (1981).mp4", "DE", "deu")]
    [InlineData("/media/Shows/Show/Season 1/Show - S01E02 - Title.mkv", "ja", "jpn")]
    [InlineData("/media/Films/Two.Dots.In.The.Name (2007)/Two.Dots.In.The.Name (2007).avi", "en", "eng")]
    public void The_name_parses_back_to_the_language_and_the_marker(
        string mediaPath,
        string languageCode,
        string expectedLanguage)
    {
        var parsed = ParseAsTheServerWould(mediaPath, languageCode);

        Assert.Equal(expectedLanguage, parsed.Language);
        Assert.Equal(GeneratedSubtitleName.Marker, parsed.Title);
        Assert.False(parsed.IsForced);
        Assert.False(parsed.IsHearingImpaired);
        Assert.False(parsed.IsDefault);
    }

    [Fact]
    public void The_server_matches_the_file_to_the_item_it_was_written_for()
    {
        // Guards the assertions above rather than the name. The server only offers
        // a file to its parser once the name has passed this comparison against the
        // media file's own base name, so a name that failed it would be parsed
        // perfectly here and never reach a track list at all.
        const string MediaPath = "/media/Films/Arrival (2016)/Arrival (2016).mkv";

        var generated = GeneratedSubtitleName.For(MediaPath, "en", "srt");

        var prefix = Path.GetFileNameWithoutExtension(MediaPath);
        var generatedWithoutExtension = Path.GetFileNameWithoutExtension(generated);

        Assert.StartsWith(prefix, generatedWithoutExtension, StringComparison.Ordinal);
        Assert.Contains(generatedWithoutExtension[prefix.Length], _namingOptions.MediaFlagDelimiters);
    }

    [Fact]
    public void The_extension_is_one_the_server_reads_as_a_subtitle()
    {
        // The parser answers null for a file whose extension is not in its subtitle
        // list, and a null there would make every assertion above a test of nothing.
        var generated = GeneratedSubtitleName.For("/media/Films/Film/Film.mkv", "en", new SubRipWriter().FileExtension);

        Assert.Contains(Path.GetExtension(generated), _namingOptions.SubtitleFileExtensions, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("e")]
    [InlineData("engl")]
    [InlineData("en.forced")]
    [InlineData("en/de")]
    [InlineData("..")]
    [InlineData("../../etc")]
    [InlineData("e n")]
    public void A_language_code_that_is_not_one_is_refused(string languageCode)
    {
        // The language is the one part of the name that does not come off an
        // existing path: it arrives from a configuration file a person edited or
        // from a backend's own answer. Letters only, so a separator or a traversal
        // sequence cannot reach the file system through a name this plugin built,
        // and a marker cannot be pushed out of the title by a second flag smuggled
        // in beside the code.
        Assert.Throws<ArgumentException>(
            () => GeneratedSubtitleName.For("/media/Films/Film/Film.mkv", languageCode, "srt"));
    }

    [Fact]
    public void The_marker_is_a_title_and_not_a_flag_the_server_knows()
    {
        // The marker survives as a title only because the server's own flag
        // vocabularies do not claim it. They are read here rather than restated, so
        // a server line that adds a flag colliding with the marker turns this red
        // instead of quietly moving the marker out of the track title.
        var marker = GeneratedSubtitleName.Marker;

        Assert.DoesNotContain(marker, _namingOptions.MediaDefaultFlags, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(marker, _namingOptions.MediaForcedFlags, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(marker, _namingOptions.MediaHearingImpairedFlags, StringComparer.OrdinalIgnoreCase);

        // The default and forced vocabularies are matched as substrings rather than
        // whole words, so a marker containing one of them is claimed as a flag even
        // though it equals none of them.
        Assert.DoesNotContain(
            _namingOptions.MediaDefaultFlags.Concat(_namingOptions.MediaForcedFlags),
            flag => marker.Contains(flag, StringComparison.OrdinalIgnoreCase));

        // A delimiter inside the marker would be split on, leaving a title that is
        // only the tail of it.
        Assert.DoesNotContain(marker, c => _namingOptions.MediaFlagDelimiters.Contains(c));
    }

    [Fact]
    public void The_header_names_the_backend_and_the_model_that_actually_ran()
    {
        // What decides whether a bad transcription is worth running again is which
        // backend and which model produced it, so those two facts are what the
        // header has to carry, and they are the ones that ran rather than the ones
        // configured now.
        var provenance = new SubtitleProvenance("openai-remote", "large-v3");

        var text = Decode(MarkedSubtitleFile.Compose(new SubRipWriter(), _segments, provenance));

        Assert.Contains("; Backend: openai-remote\r\n", text, StringComparison.Ordinal);
        Assert.Contains("; Model: large-v3\r\n", text, StringComparison.Ordinal);
        Assert.Contains("; Generated by: " + SubtitleProvenance.Plugin + "\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_header_sits_before_the_first_cue_and_leaves_the_body_alone()
    {
        var provenance = new SubtitleProvenance("local-whisper", "ggml-base.en.bin");

        var composed = MarkedSubtitleFile.Compose(new SubRipWriter(), _segments, provenance);
        var body = new SubRipWriter().Write(_segments);
        var header = MarkedSubtitleFile.Header(provenance);

        Assert.Equal(header.Length + body.Length, composed.Length);
        Assert.Equal(header, composed[..header.Length]);
        Assert.Equal(body, composed[header.Length..]);

        // A blank line is what separates the header from the first cue. Without it
        // the last header line and the first cue index are one line and the file is
        // no longer SubRip.
        Assert.EndsWith("\r\n\r\n", Decode(header), StringComparison.Ordinal);
    }

    [Fact]
    public void No_header_line_can_be_read_as_a_cue_index_or_a_timing()
    {
        var provenance = new SubtitleProvenance("openai-remote", "large-v3");

        foreach (var line in provenance.HeaderLines())
        {
            Assert.StartsWith(SubtitleProvenance.HeaderPrefix, line, StringComparison.Ordinal);
            Assert.False(char.IsDigit(line[0]), line + " starts with a digit, which is what a cue index looks like");
            Assert.DoesNotContain("-->", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_backend_or_model_name_carrying_a_line_ending_cannot_add_a_line()
    {
        // Both values arrive from outside this plugin: one from configuration, one
        // from a backend's own output. A line ending in either of them would
        // otherwise let whatever wrote it add a line to a block a reader trusts to
        // say what produced the file.
        var provenance = new SubtitleProvenance("remote\r\nfake: line", "model\nsecond\rthird");

        var lines = provenance.HeaderLines();

        Assert.Equal(3, lines.Count);
        Assert.DoesNotContain(lines, line => line.Contains('\r', StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains('\n', StringComparison.Ordinal));
    }

    [Fact]
    public void A_provenance_missing_either_fact_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new SubtitleProvenance(string.Empty, "large-v3"));
        Assert.Throws<ArgumentException>(() => new SubtitleProvenance("openai-remote", "  "));
    }

    /// <summary>
    /// Runs the name this plugin built through the server's own parser, the way the
    /// server reaches it.
    /// </summary>
    /// <remarks>
    /// The extra string is not the whole name. The server strips the media file's
    /// base name off the front and hands the parser only what is left, in
    /// <c>MediaInfoResolver</c>, so a media file whose own name holds dots does not
    /// have them read as flags. Deriving it the same way here is what makes the
    /// assertions above about a name in a real library rather than about a string.
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

    private static string Decode(byte[] bytes) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
}
