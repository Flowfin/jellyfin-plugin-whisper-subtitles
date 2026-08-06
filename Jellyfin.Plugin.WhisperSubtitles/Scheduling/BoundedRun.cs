using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// Takes an ordered list of items through a piece of work, never running more of
/// them at once than it was told to.
/// </summary>
/// <remarks>
/// The cap is held where the workers are created rather than by anything inside
/// the work. A run starts exactly as many workers as it is allowed, each one
/// takes the next item only once it has finished or abandoned the one it had,
/// and there is no path by which a worker starts another. So the number in
/// flight is the number of workers, and the number of workers is the number an
/// operator set.
///
/// What this does not do is decide what the work is. It is handed a function, so
/// the transcription pipeline, its backends and its failure vocabulary stay out
/// of the thing that counts, and the counting can be tested with work that does
/// nothing but say when it started and stopped.
///
/// A failure ends its item and nothing else. One unreadable file in a library of
/// thousands is the normal case rather than a reason to stop, so the exception is
/// recorded against the item and the worker takes the next one.
///
/// Cancellation stops the run rather than throwing out of it. A run somebody
/// stopped still has a report to make about what it managed, and a caller that
/// has to catch an exception to find out how far it got would be the wrong shape
/// for a scheduled task that has to write that report.
/// </remarks>
public static class BoundedRun
{
    /// <summary>
    /// Runs the work over the items, no more than the given number at a time.
    /// </summary>
    /// <typeparam name="T">What an item is.</typeparam>
    /// <param name="items">The items, in the order they should be taken.</param>
    /// <param name="workers">The most that may be in flight at once.</param>
    /// <param name="work">What is done to one item.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What the run managed.</returns>
    public static async Task<RunOutcome<T>> RunAsync<T>(
        IReadOnlyList<T> items,
        int workers,
        Func<T, CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1);

        var started = Math.Min(workers, items.Count);
        if (started == 0)
        {
            return new RunOutcome<T>(0, 0, [], 0, wasCancelled: false);
        }

        var next = -1;
        var completed = 0;
        var taken = 0;
        var failures = new ConcurrentQueue<FailedItem<T>>();
        var running = new Task[started];

        for (var worker = 0; worker < started; worker++)
        {
            running[worker] = Task.Run(
                async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var index = Interlocked.Increment(ref next);
                        if (index >= items.Count)
                        {
                            return;
                        }

                        Interlocked.Increment(ref taken);

                        try
                        {
                            await work(items[index], cancellationToken).ConfigureAwait(false);
                            Interlocked.Increment(ref completed);
                        }
                        catch (OperationCanceledException)
                        {
                            // The run is over and this item is not finished. It
                            // is neither completed nor failed, because a run
                            // somebody stopped says nothing about whether the
                            // item would have worked.
                            return;
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception failure)
#pragma warning restore CA1031
                        {
                            // Deliberately every exception. What a backend, a
                            // media tool or a file system can throw is not a set
                            // this type can enumerate, and a run of a thousand
                            // items that stops at the first surprise is the
                            // failure this catch exists against. The exception is
                            // kept whole and handed back with its item.
                            failures.Enqueue(new FailedItem<T>(items[index], failure));
                        }
                    }
                },
                CancellationToken.None);
        }

        await Task.WhenAll(running).ConfigureAwait(false);

        var wasCancelled = cancellationToken.IsCancellationRequested;

        return new RunOutcome<T>(
            started,
            completed,
            failures.ToArray(),
            items.Count - Volatile.Read(ref taken),
            wasCancelled);
    }
}
