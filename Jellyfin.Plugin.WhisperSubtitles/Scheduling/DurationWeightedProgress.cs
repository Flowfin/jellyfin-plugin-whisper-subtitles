using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// The number a run shows while it works, weighted by how long the media is
/// rather than by how many items there are.
/// </summary>
/// <remarks>
/// Items finished over items total is useless here. One item is a three minute
/// clip and the next is a three hour film, so a bar built on counting sits at
/// half way through a run that has done a twentieth of the work, and an operator
/// deciding whether to leave it running is deciding on a number that means
/// nothing. The denominator is the total length of the selection instead, and an
/// item contributes its own length.
///
/// The number only ever goes up. Several workers report at once and a backend
/// that reports its own progress can revise a fraction downwards, and a bar that
/// goes backwards reads as a run that lost something. What is emitted is the
/// highest value reached so far, so a report that would lower it is absorbed
/// rather than shown.
///
/// Emitting sits inside the lock, which is what makes that true of what the sink
/// receives rather than only of what this class computes. Deciding the new
/// highest under the lock and then handing it over outside leaves two workers
/// free to arrive at the sink in the opposite order to the one they were
/// numbered in, and the operator sees the lower value last. Measured at 77 of
/// 200 runs over sixty-four items before this was closed, in #146. The cost is
/// that a sink is called with the lock held, so a sink that is slow serializes
/// the workers for as long as it takes and one that calls back in relies on the
/// lock being reentrant.
///
/// A failed item advances it by its whole length. The run is that much further
/// through the work whatever the item produced, and stalling the number on a
/// failure would say the opposite.
/// </remarks>
public sealed class DurationWeightedProgress
{
    private readonly long[] _lengths;
    private readonly double[] _fractions;
    private readonly IProgress<double> _report;
    private readonly long _total;
    private readonly object _gate = new();
    private double _highest;
    private bool _finished;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurationWeightedProgress"/> class.
    /// </summary>
    /// <param name="lengths">How long each item of the selection is, in the order the selection has them.</param>
    /// <param name="report">Where the number goes.</param>
    public DurationWeightedProgress(IReadOnlyList<TimeSpan> lengths, IProgress<double> report)
    {
        ArgumentNullException.ThrowIfNull(lengths);
        ArgumentNullException.ThrowIfNull(report);

        _lengths = new long[lengths.Count];
        _fractions = new double[lengths.Count];
        _report = report;

        for (var item = 0; item < lengths.Count; item++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lengths[item], TimeSpan.Zero);

            _lengths[item] = lengths[item].Ticks;
            _total += _lengths[item];
        }
    }

    /// <summary>
    /// Folds in how far through one item its backend says it is.
    /// </summary>
    /// <param name="item">Which item of the selection.</param>
    /// <param name="fraction">How much of it is done, between nought and one.</param>
    /// <remarks>
    /// For a backend that reports nothing this is never called, and the item
    /// contributes on completion only. Both are ordinary: what a backend can say
    /// about its own progress is the backend's, and this is where the difference
    /// stops mattering to the rest of the run.
    /// </remarks>
    public void ItemProgressed(int item, double fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(item);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(item, _lengths.Length);

        Fold(item, Math.Clamp(fraction, 0, 1));
    }

    /// <summary>
    /// Records that a run is done with one item, whether it produced a subtitle
    /// or failed.
    /// </summary>
    /// <param name="item">Which item of the selection.</param>
    public void ItemFinished(int item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(item);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(item, _lengths.Length);

        Fold(item, 1);
    }

    /// <summary>
    /// Records that the run is over, so the number ends where a finished run
    /// should leave it.
    /// </summary>
    /// <remarks>
    /// A hundred is emitted once and only if the run has not already reached it,
    /// so a run whose items all finished does not report it twice. A run where
    /// every item failed reaches it here, because it is over and there is nothing
    /// left to wait for.
    /// </remarks>
    public void RunFinished()
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;

            if (_highest >= 100)
            {
                return;
            }

            _highest = 100;
            _report.Report(100);
        }
    }

    private void Fold(int item, double fraction)
    {
        lock (_gate)
        {
            if (fraction <= _fractions[item])
            {
                return;
            }

            _fractions[item] = fraction;

            var reached = _total == 0 ? 0 : 100 * Done() / _total;
            if (reached <= _highest)
            {
                return;
            }

            _highest = reached;
            _report.Report(reached);
        }
    }

    private double Done()
    {
        double done = 0;
        for (var item = 0; item < _lengths.Length; item++)
        {
            done += _lengths[item] * _fractions[item];
        }

        return done;
    }
}
