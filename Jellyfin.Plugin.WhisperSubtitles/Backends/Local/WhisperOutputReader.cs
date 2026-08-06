using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// Turns the lines a whisper.cpp compatible tool prints into timed segments, one
/// line at a time.
/// </summary>
/// <remarks>
/// This is untrusted input. The tool is a program the operator chose and this
/// repository did not build, its output arrives unattended on somebody else's
/// server, and every number in it becomes a timestamp a player will act on. So
/// the reader answers rather than throws, refuses anything it does not
/// understand, and holds its own bounds instead of trusting the length of what
/// arrives.
///
/// Strict on purpose. A reader that skipped what it could not parse would turn a
/// tool printing a diagnostic mid-transcript into a subtitle file quietly missing
/// the speech underneath it, and nothing downstream could tell that from a quiet
/// passage. Refusing the whole output is the loud failure, and loud is what an
/// unattended run needs.
///
/// The shape it accepts is the tool's default transcript line:
/// <c>[00:00:01.000 --> 00:00:02.500]   what was said</c>. Blank lines are
/// ignored, because a blank line carries nothing and every tool emits them.
/// </remarks>
public sealed class WhisperOutputReader
{
    /// <summary>
    /// The longest line the reader will look at.
    /// </summary>
    /// <remarks>
    /// A transcript line is one stretch of speech, so this is generous by two
    /// orders of magnitude and still bounds a program printing without newlines.
    /// The bound exists so a caller reading line by line cannot be made to hold an
    /// arbitrary amount by a program that never ends one.
    /// </remarks>
    public const int LineLengthCeiling = 8192;

    /// <summary>
    /// The most segments the reader will collect.
    /// </summary>
    /// <remarks>
    /// One segment is a few seconds of speech, so this covers media far longer than
    /// anything in a library and refuses a program that prints for ever.
    /// </remarks>
    public const int SegmentCeiling = 200000;

    private readonly List<TimedSegment> _segments = new();

    /// <summary>
    /// Gets the segments read so far, in the order they were printed.
    /// </summary>
    public IReadOnlyList<TimedSegment> Segments => _segments;

    /// <summary>
    /// Offers one line to the reader.
    /// </summary>
    /// <param name="line">The line the tool printed.</param>
    /// <param name="problem">What was wrong with it, when the answer is false.</param>
    /// <returns>Whether the line was understood.</returns>
    public bool TryAccept(string? line, out string? problem)
    {
        problem = null;

        if (line is null)
        {
            problem = "The tool's output ended in the middle of a line.";
            return false;
        }

        if (line.Length > LineLengthCeiling)
        {
            problem = "A line of the tool's output is longer than "
                + LineLengthCeiling.ToString(CultureInfo.InvariantCulture)
                + " characters, which no transcript line is.";
            return false;
        }

        var span = line.AsSpan().Trim();

        if (span.IsEmpty)
        {
            return true;
        }

        if (!TryReadCue(span, out var start, out var end, out var text, out problem))
        {
            return false;
        }

        if (end < start)
        {
            problem = "A segment ends before it starts, at " + Describe(start) + ".";
            return false;
        }

        if (_segments.Count > 0 && start < _segments[_segments.Count - 1].Start)
        {
            problem = "A segment starts before the one printed before it, at " + Describe(start) + ".";
            return false;
        }

        if (_segments.Count == SegmentCeiling)
        {
            problem = "The tool printed more than "
                + SegmentCeiling.ToString(CultureInfo.InvariantCulture)
                + " segments.";
            return false;
        }

        _segments.Add(new TimedSegment(start, end, text));

        return true;
    }

    private static string Describe(TimeSpan value) =>
        value.ToString("c", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads one transcript line, by hand rather than by pattern.
    /// </summary>
    /// <remarks>
    /// Hand written because the format is fixed width and because a pattern over
    /// attacker-shaped input is a second thing to reason about. What this does is
    /// linear in the length of the line and allocates only the text at the end.
    /// </remarks>
    private static bool TryReadCue(
        ReadOnlySpan<char> line,
        out TimeSpan start,
        out TimeSpan end,
        out string text,
        out string? problem)
    {
        start = default;
        end = default;
        text = string.Empty;
        problem = null;

        if (line[0] != '[')
        {
            problem = "A line of the tool's output is not a transcript line and is not blank.";
            return false;
        }

        var close = line.IndexOf(']');
        if (close < 0)
        {
            problem = "A transcript line has no closing bracket, so the tool's output stopped inside one.";
            return false;
        }

        var timings = line.Slice(1, close - 1);
        var arrow = timings.IndexOf(" --> ".AsSpan(), StringComparison.Ordinal);
        if (arrow < 0)
        {
            problem = "A transcript line carries no start and end pair.";
            return false;
        }

        if (!TryReadTimestamp(timings.Slice(0, arrow).Trim(), out start))
        {
            problem = "A transcript line carries a start time this plugin cannot read.";
            return false;
        }

        if (!TryReadTimestamp(timings.Slice(arrow + 5).Trim(), out end))
        {
            problem = "A transcript line carries an end time this plugin cannot read.";
            return false;
        }

        text = line.Slice(close + 1).Trim().ToString();

        return true;
    }

    /// <summary>
    /// Reads one timestamp, in hours, minutes, seconds and milliseconds.
    /// </summary>
    /// <remarks>
    /// The hour field is allowed more than two digits, because media longer than a
    /// hundred hours is absurd rather than impossible, and is bounded so that no
    /// arithmetic here can overflow. Every other field is exactly as wide as the
    /// format says: accepting a shorter one would make 0:1:2.3 and 00:01:02.300
    /// two readings of the same bytes.
    /// </remarks>
    private static bool TryReadTimestamp(ReadOnlySpan<char> value, out TimeSpan parsed)
    {
        parsed = default;

        var firstColon = value.IndexOf(':');
        if (firstColon < 1 || firstColon > 4)
        {
            return false;
        }

        if (!TryReadDigits(value.Slice(0, firstColon), firstColon, out var hours))
        {
            return false;
        }

        var rest = value.Slice(firstColon + 1);
        if (rest.Length != 9 || rest[2] != ':' || rest[5] != '.')
        {
            return false;
        }

        if (!TryReadDigits(rest.Slice(0, 2), 2, out var minutes)
            || !TryReadDigits(rest.Slice(3, 2), 2, out var seconds)
            || !TryReadDigits(rest.Slice(6, 3), 3, out var milliseconds))
        {
            return false;
        }

        if (minutes > 59 || seconds > 59)
        {
            return false;
        }

        parsed = new TimeSpan(0, hours, minutes, seconds, milliseconds);

        return true;
    }

    private static bool TryReadDigits(ReadOnlySpan<char> value, int expectedLength, out int number)
    {
        number = 0;

        if (value.Length != expectedLength)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var digit = value[i];
            if (digit < '0' || digit > '9')
            {
                return false;
            }

            number = (number * 10) + (digit - '0');
        }

        return true;
    }
}
