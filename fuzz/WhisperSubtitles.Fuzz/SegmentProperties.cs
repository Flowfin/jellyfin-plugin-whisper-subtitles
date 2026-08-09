using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

namespace Jellyfin.Plugin.WhisperSubtitles.Fuzz;

/// <summary>
/// What has to be true of every segment a parser hands back, whatever bytes it
/// was given.
/// </summary>
/// <remarks>
/// A crash is not the likely outcome here and it is not the interesting one. Both
/// parsers turn text into timestamps, and the failure that costs an operator
/// something is a parse that SUCCEEDS and answers with a cue a player will act
/// on: one that ends before it starts, one timed before the media begins, or one
/// timed years past anything in a library. A harness that only survived its
/// inputs would report clean runs on exactly the defects this pair of parsers can
/// have.
///
/// So a violation is raised as an exception. Under the fuzzer that is a crash and
/// is kept with the input that produced it; under the replay it is a non-zero
/// exit naming the file. Neither is repaired here: a finding is triaged with the
/// repair in the parser, never inside the harness.
/// </remarks>
internal static class SegmentProperties
{
    /// <summary>
    /// The furthest into the media a cue may be timed.
    /// </summary>
    /// <remarks>
    /// Read from the parser rather than written here, so the property and the
    /// bound cannot drift apart. A harness holding its own copy of a number would
    /// report a finding the day somebody raised the real one, and the finding
    /// would be about the harness.
    /// </remarks>
    internal static readonly TimeSpan TimeCeiling =
        TimeSpan.FromSeconds(TranscriptionResponseReader.SecondsCeiling);

    /// <summary>
    /// Checks every segment, and throws naming the first one that is wrong.
    /// </summary>
    /// <param name="segments">What the parser answered with.</param>
    /// <param name="target">The target that produced them, for the message.</param>
    internal static void Hold(IReadOnlyList<TimedSegment> segments, string target)
    {
        ArgumentNullException.ThrowIfNull(segments);

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];

            if (segment.Start < TimeSpan.Zero)
            {
                throw new PropertyViolatedException(
                    Describe(target, index, segment, "is timed before the media begins"));
            }

            if (segment.End < segment.Start)
            {
                throw new PropertyViolatedException(
                    Describe(target, index, segment, "ends before it starts"));
            }

            if (segment.Start > TimeCeiling || segment.End > TimeCeiling)
            {
                throw new PropertyViolatedException(
                    Describe(target, index, segment, "is timed past anything a library holds"));
            }
        }
    }

    /// <summary>
    /// Checks that a parse allocated in proportion to what it was given.
    /// </summary>
    /// <param name="allocated">Bytes allocated while the parse ran.</param>
    /// <param name="inputLength">The length of the input in bytes.</param>
    /// <param name="target">The target that ran, for the message.</param>
    /// <remarks>
    /// The shape this refuses is a parser that believes a number the input made up
    /// about itself, reserves that much, and turns a few bytes into a request for
    /// however much the input asked for. Neither parser announces a length today,
    /// so this is the arm that catches the one that starts to.
    ///
    /// The constant is generous on purpose. A JSON document is held in memory
    /// beside the bytes it was read from and the reader builds a list of segments
    /// out of it, so several times the input is ordinary. What is not ordinary is
    /// a multiple that does not fall as the input gets smaller, which is why the
    /// fixed allowance is separate from the multiple rather than folded into it.
    /// </remarks>
    internal static void AllocationFollowsTheInput(long allocated, int inputLength, string target)
    {
        const int Multiple = 64;
        const long FixedAllowance = 4L * 1024 * 1024;

        var ceiling = FixedAllowance + ((long)inputLength * Multiple);

        if (allocated > ceiling)
        {
            throw new PropertyViolatedException(string.Create(
                CultureInfo.InvariantCulture,
                $"{target} allocated {allocated} bytes reading {inputLength} bytes, over the ceiling of {ceiling}."));
        }
    }

    private static string Describe(string target, int index, TimedSegment segment, string what)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{target}: segment {index} {what}: {segment.Start:c} to {segment.End:c}.");
}
