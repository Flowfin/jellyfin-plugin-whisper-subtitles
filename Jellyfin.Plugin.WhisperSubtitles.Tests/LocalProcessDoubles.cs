using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

// The runner and the process it starts are one arrangement: a test writes the
// script on one and reads what happened off the other, and reading either alone
// says nothing.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// A process runner that starts nothing and hands back the process a test wrote.
/// </summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    private readonly ScriptedProcess? _process;
    private readonly Exception? _refusal;

    private ScriptedProcessRunner(ScriptedProcess? process, Exception? refusal)
    {
        _process = process;
        _refusal = refusal;
    }

    /// <summary>
    /// Gets what the backend asked to be run, or null when it asked for nothing.
    /// </summary>
    public ProcessInvocation? Invocation { get; private set; }

    public static ScriptedProcessRunner Starting(ScriptedProcess process) => new(process, null);

    /// <summary>
    /// A runner that cannot start the program at all, which is the tool being
    /// absent or not executable rather than the tool failing.
    /// </summary>
    public static ScriptedProcessRunner Refusing(Exception refusal) => new(null, refusal);

    public IStartedProcess Start(ProcessInvocation invocation)
    {
        Invocation = invocation;

        if (_refusal is not null)
        {
            throw _refusal;
        }

        return _process!;
    }
}

/// <summary>
/// A child process whose output, exit code and diagnostic text a test writes in
/// advance.
/// </summary>
/// <remarks>
/// Nothing here sleeps and nothing reads a clock. Where the script says the tool
/// keeps running after its last line, the output pends on a task that a kill
/// completes, which is how a test drives cancellation without racing a timer.
/// </remarks>
internal sealed class ScriptedProcess : IStartedProcess
{
    private readonly IReadOnlyList<string> _lines;
    private readonly int _exitCode;
    private readonly bool _keepsRunning;
    private readonly TaskCompletionSource _killed = new();
    private readonly TaskCompletionSource _reachedTheEndOfItsOutput = new();

    private ScriptedProcess(IReadOnlyList<string> lines, int exitCode, bool keepsRunning, string standardError)
    {
        _lines = lines;
        _exitCode = exitCode;
        _keepsRunning = keepsRunning;
        StandardError = standardError;
    }

    /// <summary>
    /// Gets a value indicating whether the caller asked for the program to be ended.
    /// </summary>
    /// <remarks>
    /// The whole reason <see cref="IStartedProcess.Kill"/> is on the seam. A caller
    /// that only watched the token would leave this false.
    /// </remarks>
    public bool KillRequested { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the caller disposed the process.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Gets a task that completes once the caller has read every scripted line.
    /// </summary>
    /// <remarks>
    /// A test cancelling mid item waits on this first, so the cancellation lands
    /// while the caller is inside the read rather than before it started.
    /// </remarks>
    public Task ReachedTheEndOfItsOutput => _reachedTheEndOfItsOutput.Task;

    public string StandardError { get; }

    public IAsyncEnumerable<string> StandardOutputLines => ReadAsync();

    /// <summary>
    /// A tool that prints its lines and ends.
    /// </summary>
    public static ScriptedProcess Printing(IReadOnlyList<string> lines, int exitCode = 0, string standardError = "") =>
        new(lines, exitCode, keepsRunning: false, standardError);

    /// <summary>
    /// A tool that prints its lines and then goes on working, which is every
    /// transcription for most of its life.
    /// </summary>
    public static ScriptedProcess StillRunningAfter(IReadOnlyList<string> lines, int exitCode) =>
        new(lines, exitCode, keepsRunning: true, standardError: string.Empty);

    public Task<int> WaitForExitAsync() => Task.FromResult(_exitCode);

    public void Kill()
    {
        KillRequested = true;
        _killed.TrySetResult();
    }

    public void Dispose() => Disposed = true;

    private async IAsyncEnumerable<string> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var line in _lines)
        {
            yield return line;
        }

        _reachedTheEndOfItsOutput.TrySetResult();

        if (!_keepsRunning)
        {
            yield break;
        }

        // Ends on either signal on purpose. A caller that watched the token and
        // never asked for a kill would otherwise hang the suite instead of failing
        // an assertion, and a hung test says nothing to whoever finds it.
        var cancelled = new TaskCompletionSource();
        await using var registration = cancellationToken.Register(() => cancelled.TrySetResult()).ConfigureAwait(false);

        await Task.WhenAny(_killed.Task, cancelled.Task).ConfigureAwait(false);
    }
}

/// <summary>
/// Records what a backend reported, so a test can say the report never went
/// backwards rather than only that it happened.
/// </summary>
internal sealed class RecordingProgress : IProgress<double>
{
    private readonly List<double> _reported = new();

    public IReadOnlyList<double> Reported => _reported;

    public void Report(double value) => _reported.Add(value);
}
