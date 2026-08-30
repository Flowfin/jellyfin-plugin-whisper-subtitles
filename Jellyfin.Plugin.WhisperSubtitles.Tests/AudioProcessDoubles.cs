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
    /// False on every run today, and it is here so that the day something lowers
    /// the media tool's priority the double can say so. Nothing asks it now:
    /// <c>LocalWhisperBackend</c> lowers the transcription child and the
    /// extractor's child is not covered by #22 yet.
    /// </remarks>
    public bool LowerPriorityRequested { get; private set; }

    public string StandardError => "the tool said something about the stream";

    public IAsyncEnumerable<string> StandardOutputLines => Nothing();

    public void LowerPriority() => LowerPriorityRequested = true;

    public async Task<int> WaitForExitAsync()
    {
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
