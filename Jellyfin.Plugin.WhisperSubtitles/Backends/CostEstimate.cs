using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// How long a backend expects to take, as a range rather than a number.
/// </summary>
/// <remarks>
/// A range, because the honest answer varies with the machine, the model and the
/// audio, and a single number invites an operator to treat it as a promise.
/// </remarks>
public sealed class CostEstimate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CostEstimate"/> class.
    /// </summary>
    /// <param name="shortest">The shortest wall-clock time the backend expects to take.</param>
    /// <param name="longest">The longest wall-clock time the backend expects to take.</param>
    public CostEstimate(TimeSpan shortest, TimeSpan longest)
    {
        Shortest = shortest;
        Longest = longest;
    }

    /// <summary>
    /// Gets the shortest wall-clock time the backend expects to take.
    /// </summary>
    public TimeSpan Shortest { get; }

    /// <summary>
    /// Gets the longest wall-clock time the backend expects to take.
    /// </summary>
    public TimeSpan Longest { get; }
}
