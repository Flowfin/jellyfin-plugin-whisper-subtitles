using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// A child process that has been started and has not been read yet.
/// </summary>
/// <remarks>
/// <see cref="Kill"/> is on this interface rather than hidden inside a runner
/// that watches a token, and that is the point of the seam. Cancellation has to
/// end the child, not merely stop waiting for it, and the difference between the
/// two is invisible to a caller unless ending it is something the caller asks
/// for. A test holding a double of this can see whether it was asked.
/// </remarks>
public interface IStartedProcess : IDisposable
{
    /// <summary>
    /// Gets the lines the program wrote to standard output, in order, as it writes them.
    /// </summary>
    /// <remarks>
    /// A line at a time rather than the whole output at once. The output of a
    /// transcription is proportional to the length of the media, and a caller that
    /// reads it as one string has agreed to hold all of it.
    /// </remarks>
    IAsyncEnumerable<string> StandardOutputLines { get; }

    /// <summary>
    /// Gets what the program wrote to standard error.
    /// </summary>
    /// <remarks>
    /// Read after the program has ended. It is what a failure message is built
    /// from, so it is bounded by the implementation rather than by the program.
    /// </remarks>
    string StandardError { get; }

    /// <summary>
    /// Waits for the program to end and reports its exit code.
    /// </summary>
    /// <returns>The exit code the program ended with.</returns>
    Task<int> WaitForExitAsync();

    /// <summary>
    /// Ends the program now, without waiting for it.
    /// </summary>
    /// <remarks>
    /// Safe to call more than once and safe to call after the program has already
    /// ended, because the caller asking for it and the program ending on its own
    /// race by construction. An implementation swallows that race rather than
    /// making every caller write the same try around it.
    /// </remarks>
    void Kill();
}
