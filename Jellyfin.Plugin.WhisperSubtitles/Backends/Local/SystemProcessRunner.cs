using System.Diagnostics;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// The one implementation of <see cref="IProcessRunner"/> that starts a real
/// program.
/// </summary>
/// <remarks>
/// Deliberately the whole of it. Everything a caller might want to decide about
/// how the child behaves is decided here rather than passed in, so there is one
/// place to read when asking what this plugin can launch and under what settings.
///
/// No shell. <see cref="ProcessStartInfo.ArgumentList"/> hands each argument to
/// the program as one argument, which is what makes a path with a space, a quote
/// or a semicolon in it ordinary rather than dangerous.
/// </remarks>
public sealed class SystemProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public IStartedProcess Start(ProcessInvocation invocation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new SystemStartedProcess(process);
    }
}
