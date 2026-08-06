using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Thrown when a transcription is asked of the backend that stands in for having
/// configured none.
/// </summary>
/// <remarks>
/// A type of its own rather than a message on a general exception. Everything
/// upstream has to tell "the operator has not set this up yet", which is not a
/// failure and needs a sentence pointing at the configuration page, apart from
/// "the backend was set up and broke", which is. A caller cannot make that
/// distinction by reading a message, and the two want different log levels,
/// different task outcomes and different words on the screen.
/// </remarks>
public sealed class BackendNotConfiguredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackendNotConfiguredException"/> class.
    /// </summary>
    public BackendNotConfiguredException()
        : base(NotConfiguredBackend.Explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendNotConfiguredException"/> class.
    /// </summary>
    /// <param name="message">What to tell the operator.</param>
    public BackendNotConfiguredException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendNotConfiguredException"/> class.
    /// </summary>
    /// <param name="message">What to tell the operator.</param>
    /// <param name="innerException">What went wrong underneath.</param>
    public BackendNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
