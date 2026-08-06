using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Selection;

/// <summary>
/// What a run would touch, and what that adds up to.
/// </summary>
public sealed class SelectionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionResult"/> class.
    /// </summary>
    /// <param name="candidates">The items a run would touch, in the order it would touch them.</param>
    /// <param name="totalDuration">The media duration of those items added up.</param>
    public SelectionResult(IReadOnlyList<ItemDescription> candidates, TimeSpan totalDuration)
    {
        Candidates = candidates;
        TotalDuration = totalDuration;
    }

    /// <summary>
    /// Gets the items a run would touch, in the order it would touch them.
    /// </summary>
    public IReadOnlyList<ItemDescription> Candidates { get; }

    /// <summary>
    /// Gets the media duration of those items added up.
    /// </summary>
    /// <remarks>
    /// The number the cost estimate is built on, so it is produced here rather
    /// than recomputed by whoever wants it. Two places adding up the same list is
    /// two places that can disagree, and the one an operator sees before starting
    /// a run is the one that has to be right.
    /// </remarks>
    public TimeSpan TotalDuration { get; }
}
