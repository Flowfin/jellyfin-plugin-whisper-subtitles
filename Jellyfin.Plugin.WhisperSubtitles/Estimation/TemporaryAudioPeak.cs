using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Selection;

namespace Jellyfin.Plugin.WhisperSubtitles.Estimation;

/// <summary>
/// How much temporary disk the extracted audio needs at the worst moment of a
/// run.
/// </summary>
/// <remarks>
/// THE PEAK IS NOT THE TOTAL, and reporting the total is the mistake this type
/// exists against. Every item's audio is extracted, transcribed and removed, so a
/// library of ten thousand films never has ten thousand WAV files on disk at
/// once; what is on disk is however many items the run transcribes at a time. A
/// figure built from the whole selection would tell an operator with a modest
/// disk that they cannot run this plugin, which is false in the direction that
/// stops them using it.
///
/// The worst moment is the one where the longest items happen to be the ones in
/// flight. Nothing orders a run so that they are, and nothing orders it so that
/// they are not, so the honest figure is the one that survives the ordering:
/// the largest items the run can hold at once.
///
/// IT IS A FLOOR AND IT SAYS SO. <see cref="PcmAudio.SmallestSizeFor"/> answers
/// the smallest the format can be for a length of media, and an encoder that
/// writes a longer header or rounds a partial sample up produces a little more.
/// A dry run reporting this as a prediction would be claiming a precision the
/// underlying answer does not have.
/// </remarks>
public static class TemporaryAudioPeak
{
    /// <summary>
    /// The smallest amount of temporary disk a run of these items can peak at.
    /// </summary>
    /// <param name="items">The items the run would transcribe.</param>
    /// <param name="itemsAtOnce">How many of them are in flight together.</param>
    /// <returns>Bytes, as a floor.</returns>
    public static long BytesFor(IReadOnlyList<ItemDescription> items, int itemsAtOnce)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(itemsAtOnce, 1);

        return items
            .Select(item => item.Duration)
            .OrderByDescending(duration => duration)
            .Take(itemsAtOnce)
            .Sum(duration => PcmAudio.SmallestSizeFor(duration));
    }
}
