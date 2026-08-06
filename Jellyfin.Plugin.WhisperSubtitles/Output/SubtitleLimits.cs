using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The numbers the formatter works to, each with the reason it is that number.
/// </summary>
/// <remarks>
/// These are readability limits rather than format limits: SubRip permits all of
/// the things refused here. They are conventions from subtitling practice, and
/// they are stated as constants so that changing one is a change somebody argues
/// with rather than a literal buried in a loop.
/// </remarks>
public static class SubtitleLimits
{
    /// <summary>
    /// The most characters a single line may carry.
    /// </summary>
    /// <remarks>
    /// Forty-two. Subtitling practice for Latin scripts puts a line between about
    /// thirty-seven and forty-two characters, and players lay their subtitle area
    /// out for roughly that. Beyond it a line either runs off the picture or is
    /// wrapped by the player at a place nobody chose.
    /// </remarks>
    public const int MaximumCharactersPerLine = 42;

    /// <summary>
    /// The most lines a single cue may carry.
    /// </summary>
    /// <remarks>
    /// Two. A third line covers enough of the frame that a viewer loses the shot
    /// they are reading the subtitle about, which is the trade the whole exercise
    /// is for.
    /// </remarks>
    public const int MaximumLinesPerCue = 2;

    /// <summary>
    /// The shortest a cue may stay on screen, where there is room for it.
    /// </summary>
    /// <remarks>
    /// One second. Below that a viewer sees a flash and cannot finish reading it,
    /// which is worse than the line being absent because they know they missed
    /// something. It is a target rather than a guarantee: where the next cue
    /// starts too soon, the shorter cue is the lesser harm against overlapping
    /// them.
    /// </remarks>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The longest a cue may stay on screen.
    /// </summary>
    /// <remarks>
    /// Seven seconds. A cue that outstays its speech by more than that invites a
    /// viewer to read it again and to assume the second reading is new speech.
    /// </remarks>
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(7);

    /// <summary>
    /// The shortest gap between one cue going away and the next appearing.
    /// </summary>
    /// <remarks>
    /// A hundred milliseconds, about two frames at the frame rates this material
    /// is in. Without a gap, two consecutive cues read as one cue that changed
    /// under the viewer's eyes rather than as two.
    /// </remarks>
    public static readonly TimeSpan MinimumGap = TimeSpan.FromMilliseconds(100);
}
