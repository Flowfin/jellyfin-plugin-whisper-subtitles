using System;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The reader turns text a program this repository did not build into timestamps
/// a player will act on. These are the shapes that must never become a cue.
/// </summary>
public class WhisperOutputReaderTests
{
    [Fact]
    public void A_transcript_line_becomes_a_segment_with_the_times_it_names()
    {
        var reader = new WhisperOutputReader();

        Assert.True(reader.TryAccept("[00:00:01.500 --> 00:00:04.250]   Hello there.", out var problem));
        Assert.Null(problem);

        var segment = Assert.Single(reader.Segments);

        Assert.Equal(TimeSpan.FromMilliseconds(1500), segment.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(4250), segment.End);
        Assert.Equal("Hello there.", segment.Text);
    }

    [Fact]
    public void A_blank_line_is_ignored_rather_than_refused()
    {
        // Every tool prints them and none of them means anything.
        var reader = new WhisperOutputReader();

        Assert.True(reader.TryAccept(string.Empty, out _));
        Assert.True(reader.TryAccept("   ", out _));

        Assert.Empty(reader.Segments);
    }

    [Fact]
    public void A_cue_with_no_text_is_kept_rather_than_dropped()
    {
        // Dropping it here would hide it from the formatter, which is the one place
        // that decides what an empty cue means.
        var reader = new WhisperOutputReader();

        Assert.True(reader.TryAccept("[00:00:01.000 --> 00:00:02.000]", out _));

        Assert.Equal(string.Empty, Assert.Single(reader.Segments).Text);
    }

    [Fact]
    public void An_hour_field_wider_than_two_digits_is_read()
    {
        // Media longer than a hundred hours is absurd rather than impossible, and a
        // reader that refused it would fail an item for being long.
        var reader = new WhisperOutputReader();

        Assert.True(reader.TryAccept("[100:00:00.000 --> 100:00:01.000] late", out _));

        Assert.Equal(TimeSpan.FromHours(100), Assert.Single(reader.Segments).Start);
    }

    [Theory]
    [InlineData("[00:00:0X.000 --> 00:00:02.000] a", "a letter where a digit belongs")]
    [InlineData("[00:00:01.00 --> 00:00:02.000] a", "a milliseconds field one digit short")]
    [InlineData("[00:00:01,000 --> 00:00:02.000] a", "a comma where the milliseconds separator belongs, which is how SubRip writes it")]
    [InlineData("[00:70:01.000 --> 00:00:02.000] a", "seventy minutes")]
    [InlineData("[00:00:61.000 --> 00:01:02.000] a", "sixty-one seconds")]
    [InlineData("[00:00:01.000 00:00:02.000] a", "no arrow between the two times")]
    [InlineData("[00:00:01.000 --> 00:00:02.000 a", "no closing bracket, which is output that stopped inside one")]
    [InlineData("whisper_print_timings: total time = 1234.00 ms", "a diagnostic line on the transcript stream")]
    public void A_line_this_reader_does_not_understand_is_refused_rather_than_skipped(string line, string why)
    {
        // Skipping it would turn a tool printing something unexpected into a
        // subtitle quietly missing the speech underneath, which nothing downstream
        // could tell from a quiet passage.
        var reader = new WhisperOutputReader();

        Assert.False(reader.TryAccept(line, out var problem), why);
        Assert.NotNull(problem);
        Assert.Empty(reader.Segments);
    }

    [Fact]
    public void A_segment_that_ends_before_it_starts_is_refused()
    {
        var reader = new WhisperOutputReader();

        Assert.False(reader.TryAccept("[00:00:05.000 --> 00:00:02.000] backwards", out var problem));
        Assert.Contains("ends before it starts", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_segment_starting_before_the_one_printed_before_it_is_refused()
    {
        // A tool that jumps backwards is transcribing something other than what it
        // was handed, or has lost its place. Either way the times are not the
        // item's.
        var reader = new WhisperOutputReader();

        Assert.True(reader.TryAccept("[00:00:10.000 --> 00:00:12.000] second", out _));
        Assert.False(reader.TryAccept("[00:00:01.000 --> 00:00:02.000] first", out var problem));

        Assert.Contains("before the one printed before it", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_longer_than_the_ceiling_is_refused_without_being_read()
    {
        // The bound is the reader's, not the program's. A program that never ends a
        // line would otherwise decide how much a server holds.
        var reader = new WhisperOutputReader();

        var line = "[00:00:01.000 --> 00:00:02.000] " + new string('a', WhisperOutputReader.LineLengthCeiling);

        Assert.False(reader.TryAccept(line, out var problem));
        Assert.Contains("longer than", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_that_ended_inside_a_line_is_refused()
    {
        // The reader is handed null where the stream ended without a newline.
        var reader = new WhisperOutputReader();

        Assert.False(reader.TryAccept(null, out var problem));
        Assert.Contains("ended in the middle of a line", problem, StringComparison.Ordinal);
    }
}
