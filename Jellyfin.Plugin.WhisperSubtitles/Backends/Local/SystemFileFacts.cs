using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// The reading that reaches the disk.
/// </summary>
/// <remarks>
/// The work happens away from the caller's thread, and the deadline the probe
/// holds ends the WAIT rather than the call. A metadata read on a mount whose
/// server has gone finishes when the mount decides to, and its answer is dropped
/// on the floor. That is the honest bound: the configuration page is not held,
/// and one thread pool thread is, until the file system lets go.
/// </remarks>
public sealed class SystemFileFacts : IFileFacts
{
    /// <summary>
    /// What is answered about a path that is not there.
    /// </summary>
    public static readonly FileFacts Nothing = new(exists: false, sizeInBytes: 0, isExecutable: null);

    /// <inheritdoc />
    public Task<FileFacts> DescribeAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(() => Describe(path), cancellationToken);
    }

    private static FileFacts Describe(string path)
    {
        FileInfo file;

        try
        {
            file = new FileInfo(path);
        }
        catch (Exception unusable) when (unusable is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // A path the platform will not even parse is a path with nothing at the
            // end of it, which is what the operator needs to be told. Refusing here
            // instead would turn a typo into a failed probe rather than a reason.
            return Nothing;
        }

        try
        {
            if (!file.Exists)
            {
                return Nothing;
            }

            return new FileFacts(exists: true, sizeInBytes: file.Length, isExecutable: MayBeExecuted(file));
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            // A server this plugin runs inside may not be allowed to look, and that
            // is not the same as the file being absent. It is reported as absent
            // anyway, because the probe's question is whether the tool can be used
            // from here, and one it cannot read is one it cannot run.
            return Nothing;
        }
    }

    /// <summary>
    /// Whether the permission bits let this run, where there are permission bits.
    /// </summary>
    /// <remarks>
    /// Null on the platforms that carry no such bit rather than true, because true
    /// is a claim and this has measured nothing. Windows decides what may execute
    /// from the file itself and from policy around it, neither of which is a mode
    /// this can read, so the answer there is that the question was not asked.
    ///
    /// Where the bits exist, any of the three execute bits is enough. Which of them
    /// applies depends on who the server runs as and which group owns the file, and
    /// a probe that answered that from the mode alone would be guessing at the
    /// process identity.
    /// </remarks>
    private static bool? MayBeExecuted(FileInfo file)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        var mode = file.UnixFileMode;

        return mode.HasFlag(UnixFileMode.UserExecute)
            || mode.HasFlag(UnixFileMode.GroupExecute)
            || mode.HasFlag(UnixFileMode.OtherExecute);
    }
}
