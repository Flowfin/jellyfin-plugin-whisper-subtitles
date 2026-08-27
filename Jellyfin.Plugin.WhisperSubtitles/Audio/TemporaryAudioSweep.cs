using System;
using System.IO;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// Collects extracted audio that no run is going to come back for.
/// </summary>
/// <remarks>
/// A server that dies between writing a file and deleting it runs no handler,
/// disposes nothing, and leaves an hour of audio on the disk per item it was
/// holding. Nothing that runs at the end of a process can cover that case, which
/// is why the property is held here instead: whatever was left behind is
/// collected at the start of the next run, by something that was not there when
/// the process died.
///
/// This is only safe because the directory belongs to this plugin.
/// <see cref="AudioExtractor"/> writes into a directory it is handed rather than
/// into the system temporary directory, so a sweep of what is there cannot
/// mistake somebody else's file for its own leftovers. A sweep of a shared
/// directory is a deletion of files belonging to whoever else happens to use it.
///
/// The bound is deliberately narrow in three directions at once, because each
/// one is a plausible one-character mistake with a bad consequence. It reads the
/// top level only, so a directory an operator pointed at by accident is not
/// walked. It matches only the name shape the extractor produces, so nothing
/// else in the directory is touched. And it removes files only, so a directory
/// sitting there is left alone rather than recursed into.
/// </remarks>
public static class TemporaryAudioSweep
{
    /// <summary>
    /// The name shape <see cref="AudioExtractor"/> gives an extracted file.
    /// </summary>
    /// <remarks>
    /// Stated here and used by the sweep so the two cannot drift into a pattern
    /// that matches nothing, which is the failure that leaves a sweep green and
    /// the disk full.
    /// </remarks>
    public const string ExtractedAudioPattern = "*.wav";

    /// <summary>
    /// Removes what a previous run left in the directory this plugin owns.
    /// </summary>
    /// <param name="workingDirectory">The directory this plugin writes its temporary audio into.</param>
    /// <param name="removal">How a file is removed.</param>
    /// <returns>How much was collected and how much would not go.</returns>
    /// <remarks>
    /// A directory that is not there is not an error. On a first run there is
    /// nothing to sweep, and refusing to start over that would turn an empty disk
    /// into a failed run.
    ///
    /// A file that will not go is counted rather than thrown. The likely cause is
    /// that something still holds it open, which the next sweep will find
    /// released, and a run that refused to start because one leftover was locked
    /// would be a worse outcome than the leftover.
    ///
    /// The removal is a parameter and has no default. There was a one-argument
    /// overload closing over a static <see cref="SystemFileRemoval"/> held here,
    /// and it meant a caller reached the real disk without asking any container
    /// for it, which is the one thing <see cref="PluginServiceRegistrator"/> is
    /// for. A caller that wants the real one resolves <see cref="IFileRemoval"/>.
    /// </remarks>
    public static SweepOutcome Run(string workingDirectory, IFileRemoval removal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(removal);

        if (!Directory.Exists(workingDirectory))
        {
            return new SweepOutcome(0, 0);
        }

        var collected = 0;
        var left = 0;

        foreach (var path in Directory.EnumerateFiles(
            workingDirectory,
            ExtractedAudioPattern,
            SearchOption.TopDirectoryOnly))
        {
            try
            {
                removal.Delete(path);
                collected++;
            }
            catch (IOException)
            {
                left++;
            }
            catch (UnauthorizedAccessException)
            {
                left++;
            }
        }

        return new SweepOutcome(collected, left);
    }
}
