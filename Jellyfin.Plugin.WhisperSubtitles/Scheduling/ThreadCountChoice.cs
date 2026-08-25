using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// What came of the thread count an operator set: a number of threads, or a
/// refusal saying why there is none.
/// </summary>
/// <remarks>
/// A type rather than an integer with a sentinel, for the reason
/// <see cref="ConcurrencyCapChoice"/> is one: a caller that forgets to ask
/// whether the value was accepted does not run with a number that means
/// something else. Zero threads and a refused zero are the same integer and
/// different states.
/// </remarks>
public sealed class ThreadCountChoice
{
    private ThreadCountChoice(int threads, string? refusal)
    {
        Threads = threads;
        Refusal = refusal;
    }

    /// <summary>
    /// Gets a value indicating whether the number was accepted.
    /// </summary>
    public bool IsAccepted => Refusal is null;

    /// <summary>
    /// Gets the number of threads a transcription may use, where the number was
    /// accepted.
    /// </summary>
    public int Threads { get; }

    /// <summary>
    /// Gets what an operator is told, where the number was refused.
    /// </summary>
    public string? Refusal { get; }

    /// <summary>
    /// A number a transcription may use.
    /// </summary>
    /// <param name="threads">How many threads.</param>
    /// <returns>The choice.</returns>
    public static ThreadCountChoice Accepted(int threads)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        return new ThreadCountChoice(threads, refusal: null);
    }

    /// <summary>
    /// A number no transcription may use, and what to say about it.
    /// </summary>
    /// <param name="refusal">What an operator is told.</param>
    /// <returns>The choice.</returns>
    public static ThreadCountChoice Refused(string refusal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refusal);

        return new ThreadCountChoice(0, refusal);
    }
}
