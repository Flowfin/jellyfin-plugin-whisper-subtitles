using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Work that does nothing except say how much of it was happening at once.
/// </summary>
/// <remarks>
/// It stands where a transcription would, and it is not a backend: what the cap
/// is about is how many pieces of work are in flight, which is the same question
/// whatever the work is. Keeping a model, a process and a failure vocabulary out
/// of it is what lets these tests be about the counting.
///
/// It yields inside the work so that a run with room to overlap does overlap. A
/// piece of work that never awaits would be finished before the next worker was
/// scheduled, and a cap that was never applied would look exactly like one that
/// was.
/// </remarks>
internal sealed class ConcurrencyWatcher
{
    private readonly ConcurrentQueue<int> _taken = new();
    private readonly Func<int, bool>? _failsOn;
    private readonly Action<int>? _onEntry;
    private int _inFlight;
    private int _maximumInFlight;

    public ConcurrencyWatcher(Func<int, bool>? failsOn = null, Action<int>? onEntry = null)
    {
        _failsOn = failsOn;
        _onEntry = onEntry;
    }

    /// <summary>
    /// Gets the most pieces of work that were in flight at one instant.
    /// </summary>
    public int MaximumInFlight => Volatile.Read(ref _maximumInFlight);

    /// <summary>
    /// Gets the items that were taken, in no particular order.
    /// </summary>
    public IReadOnlyList<int> Taken => _taken.ToArray();

    /// <summary>
    /// The work itself.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when this item is done with.</returns>
    public async Task RunAsync(int item, CancellationToken cancellationToken)
    {
        var now = Interlocked.Increment(ref _inFlight);
        Remember(now);
        _taken.Enqueue(item);

        try
        {
            _onEntry?.Invoke(item);

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            if (_failsOn?.Invoke(item) == true)
            {
                throw new InvalidOperationException($"item {item} could not be read");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private void Remember(int seen)
    {
        var highest = Volatile.Read(ref _maximumInFlight);
        while (seen > highest)
        {
            var was = Interlocked.CompareExchange(ref _maximumInFlight, seen, highest);
            if (was == highest)
            {
                return;
            }

            highest = was;
        }
    }
}
