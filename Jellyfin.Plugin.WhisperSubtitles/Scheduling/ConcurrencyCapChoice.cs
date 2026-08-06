using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// What came of the number an operator set: workers, or a refusal saying why
/// there are none.
/// </summary>
/// <remarks>
/// A type rather than an integer with a sentinel, so a caller that forgets to
/// ask whether the value was accepted does not run with a number that means
/// something else.
/// </remarks>
public sealed class ConcurrencyCapChoice
{
    private ConcurrencyCapChoice(int workers, string? refusal)
    {
        Workers = workers;
        Refusal = refusal;
    }

    /// <summary>
    /// Gets a value indicating whether the number was accepted.
    /// </summary>
    public bool IsAccepted => Refusal is null;

    /// <summary>
    /// Gets the number of items that may be transcribed at once, where the
    /// number was accepted.
    /// </summary>
    public int Workers { get; }

    /// <summary>
    /// Gets what an operator is told, where the number was refused.
    /// </summary>
    public string? Refusal { get; }

    /// <summary>
    /// A number a run may use.
    /// </summary>
    /// <param name="workers">How many items at once.</param>
    /// <returns>The choice.</returns>
    public static ConcurrencyCapChoice Accepted(int workers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1);

        return new ConcurrencyCapChoice(workers, refusal: null);
    }

    /// <summary>
    /// A number no run may use, and what to say about it.
    /// </summary>
    /// <param name="refusal">What an operator is told.</param>
    /// <returns>The choice.</returns>
    public static ConcurrencyCapChoice Refused(string refusal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refusal);

        return new ConcurrencyCapChoice(0, refusal);
    }
}
