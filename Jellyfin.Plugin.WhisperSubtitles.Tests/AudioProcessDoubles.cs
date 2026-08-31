using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

// The runner and the process it starts are one arrangement, as they are for the
// transcription tool. These are separate from those because what a media tool
// does is not what a transcription tool does: this one writes a file and prints
// nothing, and a double that could do both would be a double nobody can read.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// A process runner that starts nothing and writes the file the invocation
/// named, so a test can watch what happens to that file.
/// </summary>
/// <remarks>
/// The bytes are written when the process starts rather than when it ends,
/// which is what the real tool does and what makes the cancellation case worth
/// testing: there is a real file on the disk at the moment the run is stopped.
/// </remarks>
internal sealed class MediaToolRunner : IProcessRunner
{
    private readonly byte[]? _writes;
    private readonly int _exitCode;
    private readonly bool _runsUntilKilled;
    private readonly Exception? _refusal;
    private Exception? _refusesToLowerPriority;

    private MediaToolRunner(byte[]? writes, int exitCode, bool runsUntilKilled, Exception? refusal)
    {
        _writes = writes;
        _exitCode = exitCode;
        _runsUntilKilled = runsUntilKilled;
        _refusal = refusal;
    }

    /// <summary>
    /// Gets what the extractor asked to be run, or null when it asked for nothing.
    /// </summary>
    public ProcessInvocation? Invocation { get; private set; }

    /// <summary>
    /// Gets the process this runner handed back, or null when it handed back none.
    /// </summary>
    public MediaToolProcess? Started { get; private set; }

    /// <summary>
    /// Gets the file the invocation named, or null when nothing was started.
    /// </summary>
    public string? OutputPath => Invocation is null ? null : Invocation.Arguments[^1];

    /// <summary>
    /// A tool that writes the file and ends cleanly.
    /// </summary>
    public static MediaToolRunner Writing(int bytes) =>
        new(new byte[bytes], 0, runsUntilKilled: false, refusal: null);

    /// <summary>
    /// A tool that writes something and then ends non-zero, which is a decode
    /// that started and gave up partway.
    /// </summary>
    public static MediaToolRunner WritingThenFailing(int bytes, int exitCode) =>
        new(new byte[bytes], exitCode, runsUntilKilled: false, refusal: null);

    /// <summary>
    /// A tool that writes something and goes on working until it is ended, which
    /// is every extraction for most of its life.
    /// </summary>
    public static MediaToolRunner WritingAndStillRunning(int bytes) =>
        new(new byte[bytes], 0, runsUntilKilled: true, refusal: null);

    /// <summary>
    /// A tool that reports success and writes nothing at all.
    /// </summary>
    public static MediaToolRunner WritingNothing() =>
        new(null, 0, runsUntilKilled: false, refusal: null);

    /// <summary>
    /// A media tool that cannot be started, which is the path naming nothing
    /// executable rather than the tool failing.
    /// </summary>
    public static MediaToolRunner Refusing(Exception refusal) =>
        new(null, 0, runsUntilKilled: false, refusal);

    /// <summary>
    /// Makes the tool this runner starts one whose priority the platform will not
    /// lower.
    /// </summary>
    /// <remarks>
    /// On the runner rather than on the process because the extractor is handed
    /// the runner and starts the process itself, so a test has no other moment at
    /// which to reach the tool it is about.
    /// </remarks>
    /// <param name="refusal">What the platform answers with.</param>
    /// <returns>This runner, so a test reads as one sentence.</returns>
    public MediaToolRunner RefusingToLowerPriority(Exception refusal)
    {
        _refusesToLowerPriority = refusal;

        return this;
    }

    public IStartedProcess Start(ProcessInvocation invocation)
    {
        Invocation = invocation;

        if (_refusal is not null)
        {
            throw _refusal;
        }

        if (_writes is not null)
        {
            File.WriteAllBytes(invocation.Arguments[^1], _writes);
        }

        Started = new MediaToolProcess(_exitCode, _runsUntilKilled);

        if (_refusesToLowerPriority is not null)
        {
            Started.RefusingToLowerPriority(_refusesToLowerPriority);
        }

        return Started;
    }
}

/// <summary>
/// A started media tool. It prints nothing, because the real one is told not to.
/// </summary>
internal sealed class MediaToolProcess : IStartedProcess
{
    private readonly int _exitCode;
    private readonly bool _runsUntilKilled;
    private readonly TaskCompletionSource _killed = new();
    private Exception? _refusesToLowerPriority;
    private bool _waitBegun;

    public MediaToolProcess(int exitCode, bool runsUntilKilled)
    {
        _exitCode = exitCode;
        _runsUntilKilled = runsUntilKilled;
    }

    /// <summary>
    /// Gets a value indicating whether the caller asked for the tool to be ended.
    /// </summary>
    /// <remarks>
    /// A caller that only watched the token would leave this false and leave a
    /// media tool decoding an eight hour item nobody is waiting for any more.
    /// </remarks>
    public bool KillRequested { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the caller disposed the process.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the caller asked for a lower priority.
    /// </summary>
    /// <remarks>
    /// <c>AudioExtractor</c> asks for it on every extraction, which is the half of
    /// #22's priority limit that reaches the media tool. A caller that meant to
    /// ask and did not is indistinguishable from one that asked, unless the asking
    /// is something this double can say.
    /// </remarks>
    public bool LowerPriorityRequested { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the wait for the tool had already been
    /// entered when the caller asked for a lower priority, or null when nothing
    /// asked at all.
    /// </summary>
    /// <remarks>
    /// The two are different states and a single boolean would collapse them. A
    /// run that never asked and a run that asked at the right moment both leave a
    /// "was it late" flag false, so the null is what keeps an assertion about the
    /// moment from passing on a run where the moment never came.
    ///
    /// An ask made once the wait is under way is an ask made after the decode has
    /// had the processor at the ordinary priority for the whole of the item, which
    /// is the failure this limit exists against rather than a smaller version of
    /// it. This tool prints nothing, so the wait is the only thing there is to be
    /// early or late against.
    /// </remarks>
    public bool? WaitHadBegunWhenPriorityWasAsked { get; private set; }

    public string StandardError => "the tool said something about the stream";

    public IAsyncEnumerable<string> StandardOutputLines => Nothing();

    /// <summary>
    /// Makes this tool one whose priority the platform will not lower.
    /// </summary>
    /// <param name="refusal">What the platform answers with.</param>
    /// <returns>This process, so a test reads as one sentence.</returns>
    public MediaToolProcess RefusingToLowerPriority(Exception refusal)
    {
        _refusesToLowerPriority = refusal;

        return this;
    }

    public void LowerPriority()
    {
        LowerPriorityRequested = true;
        WaitHadBegunWhenPriorityWasAsked = _waitBegun;

        if (_refusesToLowerPriority is not null)
        {
            throw _refusesToLowerPriority;
        }
    }

    public async Task<int> WaitForExitAsync()
    {
        _waitBegun = true;

        if (_runsUntilKilled)
        {
            await _killed.Task.ConfigureAwait(false);
        }

        return _exitCode;
    }

    public void Kill()
    {
        KillRequested = true;
        _killed.TrySetResult();
    }

    public void Dispose() => Disposed = true;

#pragma warning disable CS1998 // async method without await, which is what an empty async sequence is
    private static async IAsyncEnumerable<string> Nothing()
    {
        yield break;
    }
#pragma warning restore CS1998
}
