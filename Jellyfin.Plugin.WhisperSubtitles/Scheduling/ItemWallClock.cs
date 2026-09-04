using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// How long one item may take before the run abandons it.
/// </summary>
/// <remarks>
/// The limit an operator sets against an item that will never finish. A
/// transcription that has stopped making progress looks exactly like one that is
/// slow, and without a wall clock the difference is a run that never ends.
///
/// THE NUMBER IS A MULTIPLE OF THE MEDIA'S DURATION RATHER THAN A CONSTANT, which
/// #22 decided on 2026-09-04. A constant is wrong in both directions on the same
/// server: it abandons a film that was going to finish and it waits an hour on a
/// twenty second clip. What a transcription costs scales with how much audio there
/// is, so the limit does too.
///
/// THE FLOOR IS WHY THERE ARE TWO NUMBERS. Four times a ninety second clip is six
/// minutes, and a backend that has to load a multi-gigabyte model from a spinning
/// disk before it says anything spends more than that on the load alone. The floor
/// is what stops the limit abandoning items for being short. Ten minutes is the
/// decided default and the operator sets it, because how long a model takes to load
/// is a fact of their machine rather than of this plugin.
///
/// WHAT THIS DOES NOT DO. It says how long an item may take and never what happens
/// when it does: abandoning the item, recording it, and deciding whether it is
/// retried are the attempt ledger's, and this hands them a deadline rather than a
/// verdict. It also assumes the duration it is given is the duration of the audio
/// being transcribed, which is the item's own duration; an item whose metadata
/// disagrees with its file gets a limit computed from the metadata.
/// </remarks>
public static class ItemWallClock
{
    /// <summary>
    /// The multiple of the media's duration an item gets where nobody has set one.
    /// </summary>
    public const int DefaultMultiple = 4;

    /// <summary>
    /// The shortest limit an item gets where nobody has set one, in minutes.
    /// </summary>
    public const int DefaultFloorMinutes = 10;

    /// <summary>
    /// The value in a configuration file that means nobody chose.
    /// </summary>
    /// <remarks>
    /// Zero, matching every other number this plugin reads from that file. A file
    /// written before these settings existed deserialises with zero in both fields,
    /// and reading that as a multiple of nothing would abandon every item the moment
    /// it started.
    /// </remarks>
    public const int LetThePolicyDecide = 0;

    /// <summary>
    /// The largest multiple an operator may set.
    /// </summary>
    /// <remarks>
    /// Bounded so a typed value stays a limit rather than becoming the absence of
    /// one. Past this the wall clock is longer than any run an operator would wait
    /// through, and a setting that cannot bite is worse than no setting because the
    /// page says it is there.
    /// </remarks>
    public const int LargestMultiple = 100;

    /// <summary>
    /// The longest floor an operator may set, in minutes.
    /// </summary>
    public const int LargestFloorMinutes = 24 * 60;

    /// <summary>
    /// How long an item may take.
    /// </summary>
    /// <param name="duration">How long the media is.</param>
    /// <param name="multiple">The multiple of that duration in force.</param>
    /// <param name="floorMinutes">The shortest limit in force, in minutes.</param>
    /// <returns>The deadline for one item.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A value outside what an operator may set reached here.</exception>
    public static TimeSpan For(TimeSpan duration, int multiple, int floorMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration.Ticks);
        ArgumentOutOfRangeException.ThrowIfLessThan(multiple, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(multiple, LargestMultiple);
        ArgumentOutOfRangeException.ThrowIfLessThan(floorMinutes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(floorMinutes, LargestFloorMinutes);

        var scaled = duration * multiple;
        var floor = TimeSpan.FromMinutes(floorMinutes);

        return scaled > floor ? scaled : floor;
    }

    /// <summary>
    /// Decides what a run does with the multiple an operator set.
    /// </summary>
    /// <param name="requested">What the operator asked for.</param>
    /// <returns>The multiple, or the reason there is none.</returns>
    public static WallClockChoice ChooseMultiple(int requested)
    {
        if (requested < 1)
        {
            return WallClockChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"An item is given at least one times its own length, and {requested} is not a multiple of anything."));
        }

        if (requested > LargestMultiple)
        {
            return WallClockChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"{requested} times the length of the media is not a limit anybody waits through. The largest this release accepts is {LargestMultiple}."));
        }

        return WallClockChoice.Accepted(requested);
    }

    /// <summary>
    /// Decides what a run does with the floor an operator set.
    /// </summary>
    /// <param name="requested">What the operator asked for, in minutes.</param>
    /// <returns>The floor, or the reason there is none.</returns>
    public static WallClockChoice ChooseFloorMinutes(int requested)
    {
        if (requested < 1)
        {
            return WallClockChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"The shortest limit an item gets is a whole minute, and {requested} is not a number of minutes."));
        }

        if (requested > LargestFloorMinutes)
        {
            return WallClockChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"{requested} minutes is longer than a day, which is a floor no item would ever reach past. The largest this release accepts is {LargestFloorMinutes}."));
        }

        return WallClockChoice.Accepted(requested);
    }
}
