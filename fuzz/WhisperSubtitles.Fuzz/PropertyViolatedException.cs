using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Fuzz;

/// <summary>
/// Raised when a parse succeeded and answered with something that cannot be true.
/// </summary>
/// <remarks>
/// Its own type rather than one of the framework's, so a finding this harness
/// decided is never confused with an exception the parser threw on its own. The
/// two are different results: one is a property the parser broke while claiming
/// success, the other is a parser that stopped answering.
/// </remarks>
[Serializable]
internal sealed class PropertyViolatedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyViolatedException"/> class.
    /// </summary>
    /// <param name="message">What was wrong.</param>
    public PropertyViolatedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyViolatedException"/> class.
    /// </summary>
    public PropertyViolatedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyViolatedException"/> class.
    /// </summary>
    /// <param name="message">What was wrong.</param>
    /// <param name="innerException">The exception underneath it.</param>
    public PropertyViolatedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
