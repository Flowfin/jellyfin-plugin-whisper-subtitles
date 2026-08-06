using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The formatter is text in and text out, so it is asserted on exhaustively. A
/// defect here is one a viewer sees within a second of pressing play.
/// </summary>
public class SegmentFormatterTests
{
    public static TheoryData<string, string[]> WrappingCases()
    {
        var data = new TheoryData<string, string[]>
        {
            { "Short line.", new[] { "Short line." } },
            { "   ", Array.Empty<string>() },
            { string.Empty, Array.Empty<string>() },
            { "  padded  words   here  ", new[] { "padded words here" } },
            {
                "This sentence is deliberately longer than one subtitle line may be, so it has to wrap.",
                new[] { "This sentence is deliberately longer than", "one subtitle line may be, so it has to", "wrap." }
            },
            {
                new string('x', 60) + " tail",
                new[] { new string('x', 60), "tail" }
            }
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(WrappingCases))]
    public void Text_is_put_on_lines_the_way_the_limits_say(string text, string[] expectedLines)
    {
        var cues = Format(Segment(0, 60, text));

        Assert.Equal(expectedLines, cues.SelectMany(c => c.Lines));
        Assert.All(cues, c => Assert.InRange(c.Lines.Count, 1, SubtitleLimits.MaximumLinesPerCue));
    }

    [Fact]
    public void No_line_is_wider_than_the_limit_unless_one_word_is()
    {
        var cues = Format(Segment(
            0,
            60,
            "This sentence is deliberately longer than one subtitle line may be, so it has to wrap."));

        Assert.All(cues.SelectMany(c => c.Lines), line =>
            Assert.True(
                line.Length <= SubtitleLimits.MaximumCharactersPerLine,
                $"\"{line}\" is {line.Length} characters"));
    }

    [Fact]
    public void A_word_longer_than_a_line_is_not_cut_in_half()
    {
        // A URL or a long compound is the ordinary case rather than the exotic
        // one, and two fragments of a word are not words in any language.
        var word = new string('a', SubtitleLimits.MaximumCharactersPerLine + 20);

        var lines = Format(Segment(0, 10, word + " after")).SelectMany(c => c.Lines).ToList();

        Assert.Contains(word, lines);
    }

    [Fact]
    public void A_long_segment_becomes_several_cues_inside_its_own_span()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 60));

        var cues = Format(Segment(10, 40, text));

        Assert.True(cues.Count > 1, "one cue would have carried more lines than a cue may");
        Assert.Equal(TimeSpan.FromSeconds(10), cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(40), cues[^1].End);
        Assert.All(cues, c => Assert.InRange(c.Lines.Count, 1, SubtitleLimits.MaximumLinesPerCue));
    }

    [Fact]
    public void A_segment_with_no_text_produces_no_cue()
    {
        // A blank cue is a blank box on the picture, and a blank line in SubRip is
        // what ends a cue, so it damages the file as well as the view.
        Assert.Empty(Format(
            Segment(0, 5, "   "),
            Segment(10, 15, string.Empty),
            Segment(20, 25, "\r\n\t")));
    }

    [Fact]
    public void A_segment_shorter_than_the_minimum_is_held_on_screen_for_it()
    {
        var cues = Format(Segment(0, 0.2, "Yes."));

        var only = Assert.Single(cues);

        Assert.Equal(SubtitleLimits.MinimumDuration, only.End - only.Start);
    }

    [Fact]
    public void A_segment_longer_than_the_maximum_is_cut_back_to_it()
    {
        var cues = Format(Segment(0, 30, "One short line."));

        var only = Assert.Single(cues);

        Assert.Equal(SubtitleLimits.MaximumDuration, only.End - only.Start);
    }

    [Fact]
    public void Overlapping_segments_come_out_not_overlapping()
    {
        var cues = Format(
            Segment(0, 5, "First."),
            Segment(3, 8, "Second."),
            Segment(4, 9, "Third."));

        Assert.Equal(3, cues.Count);

        for (var i = 1; i < cues.Count; i++)
        {
            Assert.True(
                cues[i].Start - cues[i - 1].End >= SubtitleLimits.MinimumGap,
                $"cue {i} starts {cues[i].Start - cues[i - 1].End} after the one before it");
        }
    }

    [Fact]
    public void The_earlier_cue_keeps_its_time_and_the_later_one_gives_way()
    {
        // The earlier cue is already being read. Cutting it short to make room for
        // the next one takes the words away from under the viewer.
        var cues = Format(
            Segment(0, 5, "First."),
            Segment(3, 8, "Second."));

        Assert.Equal(TimeSpan.FromSeconds(0), cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(5), cues[0].End);
        Assert.Equal(TimeSpan.FromSeconds(5) + SubtitleLimits.MinimumGap, cues[1].Start);
    }

    [Fact]
    public void Every_cue_ends_after_it_starts()
    {
        // The one thing the format cannot survive. Asserted over an awkward set
        // rather than a tidy one.
        var cues = Format(
            Segment(0, 0, "Instant."),
            Segment(0, 0, "Also instant."),
            Segment(1, 0.5, "Backwards."),
            Segment(2, 2.05, "Almost nothing."),
            Segment(60, 61, "Later."));

        Assert.All(cues, c => Assert.True(c.End > c.Start, $"{c.Start} to {c.End}"));
    }

    [Fact]
    public void Segments_out_of_order_are_put_in_order()
    {
        var cues = Format(
            Segment(30, 32, "Third."),
            Segment(10, 12, "First."),
            Segment(20, 22, "Second."));

        Assert.Equal(new[] { "First.", "Second.", "Third." }, cues.Select(c => c.Lines.Single()));
    }

    [Fact]
    public void Nothing_in_produces_nothing_out()
    {
        Assert.Empty(SegmentFormatter.Format(Array.Empty<TimedSegment>()));
    }

    private static IReadOnlyList<SubtitleCue> Format(params TimedSegment[] segments) =>
        SegmentFormatter.Format(segments);

    private static TimedSegment Segment(double startSeconds, double endSeconds, string text) =>
        new(TimeSpan.FromSeconds(startSeconds), TimeSpan.FromSeconds(endSeconds), text);
}
