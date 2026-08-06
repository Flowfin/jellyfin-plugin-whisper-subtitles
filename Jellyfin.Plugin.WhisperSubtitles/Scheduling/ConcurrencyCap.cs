using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// How many items a run may transcribe at once, and what an operator is allowed
/// to set it to.
/// </summary>
/// <remarks>
/// Transcription is the most expensive thing this plugin can ask a server to do,
/// so the number of them happening at once is a number somebody chose rather
/// than a consequence of how the code happens to await. Where nobody has chosen,
/// it is one.
///
/// A value above the ceiling is refused rather than quietly reduced. An operator
/// who typed sixteen and got four would go on believing the server was doing
/// sixteen, and the setting would be a suggestion. The refusal says the number
/// and the reason instead.
/// </remarks>
public static class ConcurrencyCap
{
    /// <summary>
    /// What a run does where nobody has set anything.
    /// </summary>
    /// <remarks>
    /// One item at a time. A media server's job is to serve media, and the
    /// default for a background task that competes with that is the setting an
    /// operator does not have to notice.
    /// </remarks>
    public const int Default = 1;

    /// <summary>
    /// The most workers this machine may be asked for.
    /// </summary>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>The largest value an operator may set.</returns>
    /// <remarks>
    /// One per processor. A transcription saturates whatever processor it is
    /// given, so past this the items do not finish sooner, they finish together
    /// and later, and everything the server does for a person watching something
    /// waits behind them. The number comes from the machine rather than from a
    /// constant because the machine is what the limit is about.
    /// </remarks>
    public static int CeilingFor(int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        return processorCount;
    }

    /// <summary>
    /// Decides what a run does with the number an operator set.
    /// </summary>
    /// <param name="requested">What the operator asked for.</param>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>The number of workers, or the reason there is none.</returns>
    public static ConcurrencyCapChoice Choose(int requested, int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        var ceiling = CeilingFor(processorCount);

        if (requested < 1)
        {
            return ConcurrencyCapChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"A run transcribes at least one item at a time, and {requested} is not a number of items."));
        }

        if (requested > ceiling)
        {
            return ConcurrencyCapChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"{requested} is above the {ceiling} this server can transcribe at once. The ceiling is one per processor and this machine reports {processorCount}."));
        }

        return ConcurrencyCapChoice.Accepted(requested);
    }
}
