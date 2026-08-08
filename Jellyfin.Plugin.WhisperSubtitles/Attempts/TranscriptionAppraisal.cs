using System;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// Looks at what a backend handed back and decides whether it is a subtitle.
/// </summary>
/// <remarks>
/// Only the modes that are visible from here. A backend answering successfully can
/// still have produced nothing, or produced segments for a different file, and
/// neither of those raises anything: they arrive as a result that looks ordinary.
/// The modes that need something this cannot see - no audio stream, silence, music
/// with no speech, several languages, a detection below the floor - are reported by
/// whatever can see them and are in the vocabulary for that reason. What is here is
/// the last gate before bytes are written.
///
/// The timing check is the cheap one and the one worth having. A backend pointed at
/// the wrong file produces a perfectly well formed transcription of something else,
/// and the only thing about it that does not fit is that it runs past the end of
/// the item. Nobody reports that as a plugin problem; they report subtitles
/// drifting, months later, on one item out of a thousand.
/// </remarks>
public static class TranscriptionAppraisal
{
    /// <summary>
    /// How far past the end of an item the last segment may reach.
    /// </summary>
    /// <remarks>
    /// Two seconds. A tolerance rather than nothing, because the duration a library
    /// holds for an item and the length of the audio a decoder produces from it
    /// differ by a frame or two routinely, and a check with no slack would refuse
    /// good transcriptions of short items. Two seconds is far below the drift this
    /// exists to catch, which is a segment ending minutes or hours late.
    /// </remarks>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Decides what to do with what a backend produced.
    /// </summary>
    /// <param name="result">What the backend produced.</param>
    /// <param name="itemDuration">How long the item is, as the library holds it.</param>
    /// <param name="tolerance">How far past the end the last segment may reach, or null for the default.</param>
    /// <returns>An outcome that either writes or says why it does not.</returns>
    public static TranscriptionOutcome Appraise(
        TranscriptionResult result,
        TimeSpan itemDuration,
        TimeSpan? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Segments is null || result.Segments.Count == 0)
        {
            return TranscriptionOutcome.WritesNothing(TranscriptionFailureReason.NoSegments);
        }

        // Every segment blank is the same nothing as no segments at all, and it is
        // what a tool that emitted a timing grid over silence produces. Written out
        // it is a subtitle track a viewer can select and read nothing in.
        if (result.Segments.All(segment => string.IsNullOrWhiteSpace(segment?.Text)))
        {
            return TranscriptionOutcome.WritesNothing(TranscriptionFailureReason.NoSegments);
        }

        // The last segment by its end rather than the last in the list. A backend is
        // asked for them in order and is trusted for it everywhere else, but the one
        // thing being checked here is whether this transcription belongs to this
        // item, and taking the ordering on trust while asking that question would
        // let a single stray segment past.
        var lastEnd = result.Segments.Max(segment => segment.End);
        var latestAllowed = itemDuration + (tolerance ?? DefaultTolerance);

        if (lastEnd > latestAllowed)
        {
            return TranscriptionOutcome.WritesNothing(TranscriptionFailureReason.TimingsDoNotFitTheItem);
        }

        return TranscriptionOutcome.Writes(result);
    }
}
