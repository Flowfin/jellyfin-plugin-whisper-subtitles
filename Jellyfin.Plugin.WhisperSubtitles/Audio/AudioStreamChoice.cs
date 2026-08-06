using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// Which audio stream of an item gets transcribed.
/// </summary>
/// <remarks>
/// A pure function, because the answer decides what the operator gets and a
/// wrong one is invisible: a film with a commentary track transcribed instead of
/// its dialogue produces a subtitle that is well formed, correctly timed and
/// about something else.
///
/// The rule, in order, and each step is here because the step before it can be
/// silent about the answer.
///
/// A stream whose language is the one being asked for wins, because that is the
/// audio the operator said they wanted words from.
///
/// Otherwise the stream the container marks as default wins, because that is the
/// one a viewer hears when they press play, and a subtitle of anything else
/// disagrees with what they are listening to.
///
/// Otherwise the stream with the most channels wins, which separates a dialogue
/// track from a commentary in the common case where a container marks neither:
/// the commentary is the mono one.
///
/// Otherwise the lowest index wins, so that two streams a container says nothing
/// distinguishing about produce the same answer on every run rather than
/// whichever order they arrived in.
/// </remarks>
public static class AudioStreamChoice
{
    /// <summary>
    /// Chooses the stream to transcribe.
    /// </summary>
    /// <param name="streams">The item's audio streams, in any order.</param>
    /// <param name="wantedLanguage">The language being asked for, or null when detection will decide.</param>
    /// <returns>The stream to transcribe, or null when there is none.</returns>
    public static AudioStreamDescription? Choose(
        IReadOnlyList<AudioStreamDescription> streams,
        string? wantedLanguage)
    {
        ArgumentNullException.ThrowIfNull(streams);

        if (streams.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(wantedLanguage))
        {
            var wanted = streams
                .Where(s => string.Equals(s.Language, wantedLanguage, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .FirstOrDefault();

            if (wanted is not null)
            {
                return wanted;
            }
        }

        return streams
            .OrderByDescending(s => s.IsDefault)
            .ThenByDescending(s => s.Channels)
            .ThenBy(s => s.Index)
            .First();
    }
}
