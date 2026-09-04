using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// Whether an item may start while the server is doing something for somebody
/// else.
/// </summary>
/// <remarks>
/// A run that finishes fast and makes playback stutter for everyone in the house
/// is a failure even though every subtitle was written. This is the limit that
/// says so.
///
/// WHAT BUSY MEANS IS WRITTEN HERE BECAUSE IT WAS DECIDED, not because a code
/// comment is the right home for a definition. #22 says the definition belongs in
/// the issue rather than in a comment, and it was taken there on 2026-09-04: the
/// server is busy when it has at least one active playback session or at least one
/// transcode at the moment an item would start. This is that sentence and nothing
/// more; where the two numbers come from is <see cref="IServerActivitySource"/>.
///
/// At least ONE, rather than a threshold. A threshold is a number somebody would
/// have to defend on every machine this plugin runs on, and the thing being
/// protected is one person's playback rather than an average. One session that
/// stutters is the failure this exists against.
///
/// The reading is taken at the moment an item would start and never during one.
/// Stopping an item halfway throws away the work already done and gives the person
/// watching nothing back, so the rule holds the queue rather than interrupting it.
///
/// WHAT THIS DOES NOT DO. It does not know how heavy the run is, so a server that
/// is busy because of this plugin's own extraction is busy here too. It sees a
/// count rather than a load, so one session on a machine built for forty is a
/// server this refuses to start on, and the operator who wants that is the operator
/// who sets the level to start anyway.
/// </remarks>
public static class BusyServerRule
{
    /// <summary>
    /// The level an operator has not chosen, which is the level a run uses.
    /// </summary>
    /// <remarks>
    /// An empty string rather than a value, for the reason every other setting in
    /// this plugin takes a sentinel: a configuration file written before this
    /// setting existed deserialises with nothing in the field, and a run that read
    /// that as a level would silently take whichever level the enumeration happened
    /// to put first. Nobody choosing and choosing are different states.
    /// </remarks>
    public const string NobodyChose = "";

    /// <summary>
    /// Hold the item until the server has nothing else in flight.
    /// </summary>
    public const string Pause = "pause";

    /// <summary>
    /// Start the item whatever the server is doing.
    /// </summary>
    public const string StartAnyway = "start anyway";

    /// <summary>
    /// The level a run uses where nobody has chosen one.
    /// </summary>
    /// <remarks>
    /// Pausing, which is the conservative half of the answer to question 6 of #8:
    /// this plugin ships to operators whose servers stream alongside the run, and an
    /// unattended first run must not be the thing that spoils an evening. An
    /// operator with hardware bought for this raises it on the page.
    /// </remarks>
    public const string Default = Pause;

    private static readonly string[] _levels = [Pause, StartAnyway];

    /// <summary>
    /// Gets every level this release writes, in the order the page offers them.
    /// </summary>
    /// <remarks>
    /// A named level rather than a switch, which is what #22 decided on 2026-09-04
    /// and for the reason it gave: a level survives an upgrade that adds a third
    /// value and a switch does not. An operator who set a switch to off has said
    /// nothing about which of two later meanings they wanted.
    /// </remarks>
    public static IReadOnlyList<string> Levels => _levels;

    /// <summary>
    /// Whether a level is one this release knows.
    /// </summary>
    /// <param name="level">Whatever the file held.</param>
    /// <returns><see langword="true"/> where the level is one of the levels above.</returns>
    public static bool IsALevel(string? level) =>
        level is not null && _levels.Contains(level, StringComparer.Ordinal);

    /// <summary>
    /// Decides whether an item may start now.
    /// </summary>
    /// <param name="activity">What the server is doing for somebody else.</param>
    /// <param name="level">The level in force.</param>
    /// <returns>Whether the item starts, and the reason where it does not.</returns>
    /// <exception cref="ArgumentException">The level is not one this release knows.</exception>
    public static BusyServerDecision Decide(ServerActivity activity, string level)
    {
        if (!IsALevel(level))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"\"{level}\" is not a level this release knows. A level reaches here only after the configuration rule has resolved it, so a value this refuses is a caller that skipped that rule."),
                nameof(level));
        }

        if (string.Equals(level, StartAnyway, StringComparison.Ordinal))
        {
            return BusyServerDecision.Starts();
        }

        if (activity.Transcodes > 0)
        {
            return BusyServerDecision.Held(string.Create(
                CultureInfo.InvariantCulture,
                $"The server is transcoding {activity.Transcodes} stream(s), and a transcription would take the processors that is using."));
        }

        if (activity.PlaybackSessions > 0)
        {
            return BusyServerDecision.Held(string.Create(
                CultureInfo.InvariantCulture,
                $"The server is playing to {activity.PlaybackSessions} session(s), and this run waits until it is not."));
        }

        return BusyServerDecision.Starts();
    }
}
