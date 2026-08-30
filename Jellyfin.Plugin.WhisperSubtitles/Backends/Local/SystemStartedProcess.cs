using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// A real child process, behind the seam the backend talks to.
/// </summary>
/// <remarks>
/// Standard error is drained into a bounded buffer while the transcript is being
/// read. Draining it is not optional: a program whose error pipe fills up blocks
/// writing to it, and a program blocked on that stops printing the transcript,
/// which looks exactly like a transcription that has hung. Bounding it is not
/// optional either, because the amount a chosen program prints there is not this
/// plugin's to decide.
/// </remarks>
internal sealed class SystemStartedProcess : IStartedProcess
{
    /// <summary>
    /// How much of the program's diagnostic output is kept for a failure message.
    /// </summary>
    /// <remarks>
    /// Enough for a stack of messages and a usage note, which is what a person
    /// reading a failed item wants, and far short of anything worth holding in a
    /// server's memory per item.
    /// </remarks>
    private const int StandardErrorCeiling = 16384;

    private readonly Process _process;
    private readonly StringBuilder _standardError = new();
    private bool _disposed;

    public SystemStartedProcess(Process process)
    {
        _process = process;

        _process.ErrorDataReceived += OnErrorData;
        _process.BeginErrorReadLine();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StandardOutputLines => ReadStandardOutputAsync();

    /// <inheritdoc />
    public string StandardError
    {
        get
        {
            lock (_standardError)
            {
                return _standardError.ToString();
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);

        return _process.ExitCode;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Below normal rather than idle. Idle is the class that is scheduled only
    /// when nothing else wants the machine at all, and a transcription that never
    /// finishes is a limit that turned into a refusal to work.
    ///
    /// Nothing is caught here. The platforms that refuse this, and the accounts
    /// that may not do it, are real, and what a refusal costs is the caller's to
    /// decide rather than this class's to hide.
    /// </remarks>
    public void LowerPriority() => _process.PriorityClass = ProcessPriorityClass.BelowNormal;

    /// <inheritdoc />
    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The program ended between the check and the kill. That is the race
            // this method exists to swallow rather than to hand to every caller.
        }
        catch (NotSupportedException)
        {
            // A platform that cannot end this process. Nothing here can repair it,
            // and turning it into a failed item would hide the cancellation that
            // asked for it.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _process.ErrorDataReceived -= OnErrorData;
        _process.Dispose();
    }

    private async IAsyncEnumerable<string> ReadStandardOutputAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                yield break;
            }

            yield return line;
        }
    }

    private void OnErrorData(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        lock (_standardError)
        {
            if (_standardError.Length >= StandardErrorCeiling)
            {
                return;
            }

            _standardError.Append(eventArgs.Data).Append('\n');
        }
    }
}
