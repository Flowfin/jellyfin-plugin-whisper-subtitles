using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// An item the work threw on, and what it threw.
/// </summary>
/// <typeparam name="T">What an item is.</typeparam>
/// <remarks>
/// The two together, because a count of failures with no items is a number
/// nobody can act on, and an exception with no item is a stack trace about
/// something the reader cannot name.
/// </remarks>
public sealed class FailedItem<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FailedItem{T}"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="failure">What the work threw.</param>
    public FailedItem(T item, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        Item = item;
        Failure = failure;
    }

    /// <summary>
    /// Gets the item.
    /// </summary>
    public T Item { get; }

    /// <summary>
    /// Gets what the work threw on it.
    /// </summary>
    public Exception Failure { get; }
}
