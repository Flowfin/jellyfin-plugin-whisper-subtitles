using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Hygiene;

/// <summary>
/// What one rule decided about one pull request.
/// </summary>
/// <remarks>
/// A rule reports whether it held and why, in that order, whichever tier it is in.
/// A tier decides what a verdict costs; it does not change what a verdict is, so
/// the advisory tier reports the same shape and the run reads it and moves on.
/// </remarks>
internal sealed class Verdict
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Verdict"/> class.
    /// </summary>
    /// <param name="rule">What the rule is called.</param>
    /// <param name="held">Whether the pull request satisfies it.</param>
    /// <param name="detail">What it found, whether or not it held.</param>
    public Verdict(string rule, bool held, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ArgumentNullException.ThrowIfNull(detail);

        Rule = rule;
        Held = held;
        Detail = detail;
    }

    /// <summary>
    /// Gets what the rule is called.
    /// </summary>
    public string Rule { get; }

    /// <summary>
    /// Gets a value indicating whether the pull request satisfies it.
    /// </summary>
    public bool Held { get; }

    /// <summary>
    /// Gets what the rule found, whether or not it held.
    /// </summary>
    /// <remarks>
    /// Written for the person reading a red check with no other context, so it says
    /// what would make it green rather than only that it is not.
    /// </remarks>
    public string Detail { get; }
}
