using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// How many threads one transcription may use, and what an operator is allowed
/// to set it to.
/// </summary>
/// <remarks>
/// The sibling limit to <see cref="ConcurrencyCap"/> and the other half of the
/// same question. That one decides how many items run at once; this one decides
/// how much of the machine each of them takes. A run of one item on every
/// processor and a run of every processor's worth of items are the same load on
/// a server, so a budget that names only one of the two names none.
///
/// The default is BELOW the processor count rather than equal to it. A media
/// server's job is to serve media, and a transcription that is given the whole
/// machine leaves nothing for the thing the machine was bought for. Where the
/// machine has one processor there is no value below it, and the default is that
/// one processor, which is stated at <see cref="DefaultFor"/> rather than left to
/// be discovered.
///
/// A value above the ceiling is refused rather than quietly reduced, for the
/// reason the concurrency cap refuses one: an operator who typed thirty-two and
/// got eight would go on believing the tool was given thirty-two, and the setting
/// would be a suggestion.
/// </remarks>
public static class ThreadCount
{
    /// <summary>
    /// What a transcription gets where nobody has set anything.
    /// </summary>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>The thread count to use when the operator has chosen none.</returns>
    /// <remarks>
    /// Half the processors, rounded down, and never below one. Half rather than
    /// some other fraction because the property this default is for is stated in
    /// halves: whatever a run takes, the server keeps the rest, and an operator
    /// who has not been to the page still has a machine that answers.
    ///
    /// The number comes from the machine rather than from a constant, for the
    /// reason the concurrency cap's ceiling does: the limit is about the machine.
    /// A constant would be conservative on one server and the whole of another.
    ///
    /// ON A SINGLE PROCESSOR MACHINE THIS RETURNS THAT PROCESSOR, so on that one
    /// machine the default is not below the processor count. There is no value
    /// below it that is a number of threads, and refusing to transcribe at all on
    /// a one-processor server would be a limit that removed the feature rather
    /// than bounding it.
    /// </remarks>
    public static int DefaultFor(int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        return Math.Max(1, processorCount / 2);
    }

    /// <summary>
    /// The most threads one transcription may be asked for.
    /// </summary>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>The largest value an operator may set.</returns>
    /// <remarks>
    /// One per processor. Past this the threads do not make the item finish
    /// sooner; they take turns on the processors that exist and add the cost of
    /// switching between them, and everything the server does for a person
    /// watching something waits behind that.
    ///
    /// This is the ceiling for ONE transcription and it does not know how many
    /// are running. An operator who raises both limits to the ceiling has asked
    /// for the whole machine several times over, and what stops that is the
    /// concurrency cap's own ceiling rather than this one.
    /// </remarks>
    public static int CeilingFor(int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        return processorCount;
    }

    /// <summary>
    /// Decides what a transcription does with the number an operator set.
    /// </summary>
    /// <param name="requested">What the operator asked for.</param>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>The number of threads, or the reason there is none.</returns>
    public static ThreadCountChoice Choose(int requested, int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        var ceiling = CeilingFor(processorCount);

        if (requested < 1)
        {
            return ThreadCountChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"A transcription runs on at least one thread, and {requested} is not a number of threads."));
        }

        if (requested > ceiling)
        {
            return ThreadCountChoice.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"{requested} is above the {ceiling} threads this server has. The ceiling is one per processor and this machine reports {processorCount}."));
        }

        return ThreadCountChoice.Accepted(requested);
    }
}
