using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The bytes this writer produces are what an operator's client reads, so the
/// assertions here are on the bytes rather than on a string the test decoded the
/// way it hoped they were encoded.
/// </summary>
public class SubRipWriterTests
{
    private static readonly IReadOnlyList<TimedSegment> _fixture = new[]
    {
        new TimedSegment(TimeSpan.Zero, TimeSpan.FromMilliseconds(1500), "First line."),
        new TimedSegment(TimeSpan.FromMilliseconds(1500), new TimeSpan(0, 1, 2, 3, 456), "Second line.")
    };

    [Fact]
    public void The_fixture_is_written_as_valid_SubRip()
    {
        var text = Decode(new SubRipWriter().Write(_fixture));

        var expected =
            "1\r\n" +
            "00:00:00,000 --> 00:00:01,500\r\n" +
            "First line.\r\n" +
            "\r\n" +
            "2\r\n" +
            "00:00:01,500 --> 01:02:03,456\r\n" +
            "Second line.\r\n" +
            "\r\n";

        Assert.Equal(expected, text);
    }

    [Fact]
    public void The_file_is_UTF8_with_no_byte_order_mark()
    {
        var bytes = new SubRipWriter().Write(_fixture);

        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "the file starts with a UTF-8 byte order mark");

        // A mark is only absent for a good reason if the bytes are UTF-8 in the
        // first place, so decode strictly rather than leniently.
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        strict.GetString(bytes);
    }

    [Fact]
    public void Every_line_ends_with_a_carriage_return_and_a_line_feed()
    {
        var text = Decode(new SubRipWriter().Write(_fixture));

        var bare = text.Replace("\r\n", string.Empty, StringComparison.Ordinal);

        Assert.DoesNotContain('\n', bare);

        Assert.DoesNotContain('\r', bare);
    }

    [Fact]
    public void A_cue_outside_ASCII_round_trips()
    {
        const string Spoken = "Über den Wolken, 山の音, שלום, emoji \U0001F600";

        var bytes = new SubRipWriter().Write(new[]
        {
            new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), Spoken)
        });

        Assert.Equal(Spoken, CueTexts(bytes).Single());
    }

    [Fact]
    public void A_segment_whose_text_carries_a_line_break_still_produces_one_cue()
    {
        // Without this the blank line inside the text would end the cue early and
        // every cue after it would be read as part of the wrong block.
        var bytes = new SubRipWriter().Write(new[]
        {
            new TimedSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "one\r\ntwo\nthree\rfour"),
            new TimedSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "after")
        });

        var cues = CueTexts(bytes);

        Assert.Equal(2, cues.Count);

        Assert.Equal("one two three four", cues[0]);

        Assert.Equal("after", cues[1]);
    }

    [Fact]
    public void The_writer_names_the_extension_the_format_uses()
    {
        ISubtitleFormatWriter writer = new SubRipWriter();

        Assert.Equal("srt", writer.FileExtension);
    }

    private static string Decode(byte[] bytes) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);

    /// <summary>
    /// Reads the cue texts back out, by the same rule a parser uses: blocks are
    /// separated by a blank line, and the text is what follows the index and the
    /// timing line.
    /// </summary>
    private static IReadOnlyList<string> CueTexts(byte[] bytes)
    {
        var blocks = Decode(bytes)
            .Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries);

        return blocks
            .Select(b => b.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            .Select(lines => string.Join(" ", lines.Skip(2)))
            .ToList();
    }
}
