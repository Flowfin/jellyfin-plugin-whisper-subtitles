using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// What a run managed, which is what an operator is later told about it.
/// </summary>
/// <typeparam name="T">What an item is.</typeparam>
/// <remarks>
/// Numbers that add up, and one deliberate hole in the sum. Completed, failed
/// and never started account for every item of a run that was not stopped. In a
/// run that was stopped they do not, and the difference is exactly the items a
/// worker had in flight when the stop arrived: those are counted nowhere,
/// because they were started, they did not finish, and they say nothing about
/// whether they would have.
/// </remarks>
public sealed class RunOutcome<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOutcome{T}"/> class.
    /// </summary>
    /// <param name="workersStarted">How many workers the run created.</param>
    /// <param name="completed">Items the work finished.</param>
    /// <param name="failures">Items the work threw on, with what it threw.</param>
    /// <param name="neverStarted">Items no worker took.</param>
    /// <param name="wasCancelled">Whether the run was stopped.</param>
    public RunOutcome(
        int workersStarted,
        int completed,
        IReadOnlyList<FailedItem<T>> failures,
        int neverStarted,
        bool wasCancelled)
    {
        ArgumentNullException.ThrowIfNull(failures);

        WorkersStarted = workersStarted;
        Completed = completed;
        Failures = failures;
        NeverStarted = neverStarted;
        WasCancelled = wasCancelled;
    }

    /// <summary>
    /// Gets how many workers the run created, which is the most items it could
    /// have had in flight at once.
    /// </summary>
    public int WorkersStarted { get; }

    /// <summary>
    /// Gets how many items the work finished.
    /// </summary>
    public int Completed { get; }

    /// <summary>
    /// Gets the items the work threw on, each with what it threw.
    /// </summary>
    public IReadOnlyList<FailedItem<T>> Failures { get; }

    /// <summary>
    /// Gets how many items no worker took.
    /// </summary>
    /// <remarks>
    /// Nought unless the run was stopped. An item that was taken and abandoned
    /// mid work is not counted here, because it was started.
    /// </remarks>
    public int NeverStarted { get; }

    /// <summary>
    /// Gets a value indicating whether the run was stopped before it ran out of
    /// items.
    /// </summary>
    public bool WasCancelled { get; }
}
