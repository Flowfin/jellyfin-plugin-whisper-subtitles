using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// What came of a wall-clock number an operator set: the number, or a refusal
/// saying why there is none.
/// </summary>
/// <remarks>
/// One type for both numbers rather than two, because they are refused for the
/// same two reasons and a caller reads them the same way. A type rather than an
/// integer with a sentinel, for the reason <see cref="ThreadCountChoice"/> is one:
/// zero and a refused zero are the same integer and different states, and here the
/// difference is an item abandoned the moment it starts.
/// </remarks>
public sealed class WallClockChoice
{
    private WallClockChoice(int value, string? refusal)
    {
        Value = value;
        Refusal = refusal;
    }

    /// <summary>
    /// Gets a value indicating whether the number was accepted.
    /// </summary>
    public bool IsAccepted => Refusal is null;

    /// <summary>
    /// Gets the number in force, where it was accepted.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets what an operator is told, where the number was refused.
    /// </summary>
    public string? Refusal { get; }

    /// <summary>
    /// A number a run may use.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The choice.</returns>
    public static WallClockChoice Accepted(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

        return new WallClockChoice(value, refusal: null);
    }

    /// <summary>
    /// A number no run may use, and what to say about it.
    /// </summary>
    /// <param name="refusal">What an operator is told.</param>
    /// <returns>The choice.</returns>
    public static WallClockChoice Refused(string refusal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refusal);

        return new WallClockChoice(0, refusal);
    }
}
