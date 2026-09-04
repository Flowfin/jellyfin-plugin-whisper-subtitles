using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// What came of asking whether an item may start: it starts, or it is held and
/// the reason says what the server was doing.
/// </summary>
/// <remarks>
/// A type rather than a boolean, for the reason <see cref="ThreadCountChoice"/> is
/// one. An item that did not start and an item that started and produced nothing
/// look identical where only a flag was carried, and the operator asking why a run
/// did nothing overnight is the reader this is for.
/// </remarks>
public sealed class BusyServerDecision
{
    private BusyServerDecision(string? reason)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the item may start now.
    /// </summary>
    public bool MayStart => Reason is null;

    /// <summary>
    /// Gets what the server was doing, where the item was held.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// An item that may start.
    /// </summary>
    /// <returns>The decision.</returns>
    public static BusyServerDecision Starts() => new(reason: null);

    /// <summary>
    /// An item that is held, and what the server was doing.
    /// </summary>
    /// <param name="reason">What an operator is told.</param>
    /// <returns>The decision.</returns>
    public static BusyServerDecision Held(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new BusyServerDecision(reason);
    }
}
