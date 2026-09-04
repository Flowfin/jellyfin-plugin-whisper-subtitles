using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// How much of the machine's attention a transcription's child process gets.
/// </summary>
/// <remarks>
/// A NAMED LEVEL RATHER THAN A SWITCH, which #22 decided on 2026-09-04, and the
/// reason is what an upgrade does to each. A third level added later - something
/// between these two, or a level that also lowers the input and output priority -
/// leaves every stored level meaning what it meant. A switch cannot take a third
/// value, and an operator who set it to off has said nothing about which of two
/// later meanings they wanted, so the release that adds the third has to guess on
/// their behalf.
///
/// The two levels this release writes are the two the tree already had, one of them
/// unnamed. <see cref="IdleOnly"/> is what the local backend does today: it asks
/// the platform to run the child below ordinary work, best effort, and carries on
/// when the platform refuses. <see cref="Normal"/> is the level that was not
/// available: leave the child where the platform started it.
///
/// LOWERING IS BEST EFFORT AND ITS FAILURE IS NOT THE ITEM'S. Not every platform
/// lets a process lower its own priority, and none of them may be made to by
/// anything that would need elevation. So a refusal is logged and the transcription
/// goes on, and the test that holds that is
/// <c>A_platform_that_refuses_the_lower_priority_still_gets_its_transcript</c>.
/// That is a property of the backend rather than of this type, and it does not
/// change with the level: a level of <see cref="Normal"/> never asks, so it has
/// nothing to fail at.
/// </remarks>
public static class ChildProcessPriority
{
    /// <summary>
    /// The level in a configuration file that means nobody chose.
    /// </summary>
    /// <remarks>
    /// An empty string, for the reason <see cref="BusyServerRule.NobodyChose"/> is
    /// one: a file written before this setting existed deserialises with nothing in
    /// the field, and a run that read that as a level would take whichever the
    /// enumeration happened to put first rather than the documented default.
    /// </remarks>
    public const string NobodyChose = "";

    /// <summary>
    /// Ask the platform to run the child below ordinary work.
    /// </summary>
    public const string IdleOnly = "idle-only";

    /// <summary>
    /// Leave the child where the platform started it.
    /// </summary>
    public const string Normal = "normal";

    /// <summary>
    /// The level a run uses where nobody has chosen one.
    /// </summary>
    /// <remarks>
    /// The lowered one, which is what the tree does today and the conservative half
    /// of the answer to question 6 of #8. An operator who has not been to the page
    /// still has a server that answers while a run is under way.
    /// </remarks>
    public const string Default = IdleOnly;

    private static readonly string[] _levels = [IdleOnly, Normal];

    /// <summary>
    /// Gets every level this release writes, in the order the page offers them.
    /// </summary>
    public static IReadOnlyList<string> Levels => _levels;

    /// <summary>
    /// Whether a level is one this release knows.
    /// </summary>
    /// <param name="level">Whatever the file held.</param>
    /// <returns><see langword="true"/> where the level is one of the levels above.</returns>
    public static bool IsALevel(string? level) =>
        level is not null && _levels.Contains(level, StringComparer.Ordinal);

    /// <summary>
    /// Whether a level asks the platform to lower the child's priority.
    /// </summary>
    /// <param name="level">The level in force.</param>
    /// <returns><see langword="true"/> where the ask is made.</returns>
    /// <exception cref="ArgumentException">The level is not one this release knows.</exception>
    public static bool LowersPriority(string level)
    {
        if (!IsALevel(level))
        {
            throw new ArgumentException(
                $"\"{level}\" is not a level this release knows. A level reaches here only after the configuration rule has resolved it, so a value this refuses is a caller that skipped that rule.",
                nameof(level));
        }

        return string.Equals(level, IdleOnly, StringComparison.Ordinal);
    }
}
