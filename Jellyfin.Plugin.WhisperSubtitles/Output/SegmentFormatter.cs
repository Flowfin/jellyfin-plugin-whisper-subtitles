using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Turns what a backend returned into cues a viewer can read.
/// </summary>
/// <remarks>
/// Text in, text out, and no other input, which is why this is the cheapest part
/// of the plugin to test exhaustively and the part where a defect is most visible
/// to somebody watching.
///
/// The order of the work matters and is the same every time: drop what has no
/// text, wrap what is too wide, cut what needs more lines than a cue may carry,
/// then fix the timing of what came out. Timing is last because splitting changes
/// how many cues there are, and every timing rule is about a cue's neighbours.
/// </remarks>
public static class SegmentFormatter
{
    private static readonly char[] _wordSeparators = [' ', '\t', '\r', '\n', ' '];

    /// <summary>
    /// Formats segments into cues.
    /// </summary>
    /// <param name="segments">What the backend returned.</param>
    /// <returns>The cues, in order, none overlapping.</returns>
    public static IReadOnlyList<SubtitleCue> Format(IReadOnlyList<TimedSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var split = segments
            .Where(segment => segment is not null)
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.End)
            .SelectMany(Split)
            .ToList();

        return Retime(split);
    }

    /// <summary>
    /// Breaks one segment's text into as many cues as it needs, keeping the
    /// segment's own span.
    /// </summary>
    private static IEnumerable<SubtitleCue> Split(TimedSegment segment)
    {
        var lines = Wrap(segment.Text);

        if (lines.Count == 0)
        {
            // Nothing was said, or nothing survived trimming. A cue with no text
            // is a blank box on the picture and a blank line in the file, which
            // the format reads as the end of a cue.
            yield break;
        }

        var groups = new List<List<string>>();

        for (var i = 0; i < lines.Count; i += SubtitleLimits.MaximumLinesPerCue)
        {
            groups.Add(lines.Skip(i).Take(SubtitleLimits.MaximumLinesPerCue).ToList());
        }

        var span = segment.End - segment.Start;

        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        var totalCharacters = groups.Sum(g => g.Sum(l => l.Length));

        var at = segment.Start;

        for (var i = 0; i < groups.Count; i++)
        {
            // Time is shared out by how much there is to read, so a two-word cue
            // does not sit as long as the sentence beside it.
            var characters = groups[i].Sum(l => l.Length);

            var share = totalCharacters == 0
                ? span
                : TimeSpan.FromTicks((long)(span.Ticks * ((double)characters / totalCharacters)));

            var end = i == groups.Count - 1 ? segment.End : at + share;

            yield return new SubtitleCue(at, end, groups[i]);

            at = end;
        }
    }

    /// <summary>
    /// Puts the text on lines no wider than the limit, breaking between words.
    /// </summary>
    /// <remarks>
    /// A word longer than a whole line is left whole on a line of its own. Cutting
    /// inside it would produce two fragments that are not words in any language,
    /// and a URL or a long compound is the ordinary case rather than the exotic
    /// one.
    /// </remarks>
    private static List<string> Wrap(string? text)
    {
        var words = (text ?? string.Empty).Split(
            _wordSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= SubtitleLimits.MaximumCharactersPerLine)
            {
                current.Append(' ').Append(word);
                continue;
            }

            lines.Add(current.ToString());
            current.Clear().Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    /// <summary>
    /// Makes the timings of a finished list of cues legal and readable.
    /// </summary>
    private static List<SubtitleCue> Retime(List<SubtitleCue> cues)
    {
        var result = new List<SubtitleCue>();
        var previousEnd = TimeSpan.MinValue;

        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];

            var start = cue.Start;

            if (previousEnd != TimeSpan.MinValue && start < previousEnd + SubtitleLimits.MinimumGap)
            {
                // Overlapping speech, which a backend produces often enough. The
                // later cue gives way rather than the earlier one being cut short,
                // because the earlier one is already being read.
                start = previousEnd + SubtitleLimits.MinimumGap;
            }

            var roomEnds = i + 1 < cues.Count
                ? cues[i + 1].Start - SubtitleLimits.MinimumGap
                : TimeSpan.MaxValue;

            var end = cue.End;

            if (end < start)
            {
                end = start;
            }

            if (end - start > SubtitleLimits.MaximumDuration)
            {
                end = start + SubtitleLimits.MaximumDuration;
            }

            if (end - start < SubtitleLimits.MinimumDuration)
            {
                var wanted = start + SubtitleLimits.MinimumDuration;

                // Held on screen for the minimum only where there is room for it.
                // The next cue's start is the bound, and where it comes too soon
                // the shorter cue is the lesser harm: overlapping two cues puts
                // both on the picture at once.
                end = wanted <= roomEnds
                    ? wanted
                    : (roomEnds > end ? roomEnds : end);
            }

            if (end <= start)
            {
                // No room at all, which needs two segments beginning at the same
                // instant. A cue that ends before it starts is not a subtitle, so
                // the choice is between dropping it and writing something no
                // player will show correctly.
                continue;
            }

            result.Add(new SubtitleCue(start, end, cue.Lines));
            previousEnd = end;
        }

        return result;
    }
}
