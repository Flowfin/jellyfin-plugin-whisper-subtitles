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
    /// Asks the operating system to schedule the program below ordinary work.
    /// </summary>
    /// <remarks>
    /// On the seam for the same reason <see cref="Kill"/> is: a caller that meant
    /// to ask and did not is indistinguishable from one that asked, unless the
    /// asking is something a double can see.
    ///
    /// Downward only. Raising a priority is the direction that needs a privilege,
    /// and nothing in this plugin offers it.
    ///
    /// THIS ONE MAY THROW, and that is the difference from <see cref="Kill"/>.
    /// Lowering a priority is not available on every platform and is not
    /// permitted to every account, and the caller decides what a refusal costs.
    /// Swallowing it here would make the promise that a refused priority does not
    /// spoil the work a promise no test could read.
    /// </remarks>
    void LowerPriority();

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
