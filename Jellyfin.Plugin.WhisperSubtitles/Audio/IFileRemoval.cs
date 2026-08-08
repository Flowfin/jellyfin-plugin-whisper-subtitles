namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// The seam a sweep removes a file through.
/// </summary>
/// <remarks>
/// Here because the interesting case cannot be arranged on a real disk on both
/// of the platforms this suite runs on. A file held open refuses to be deleted
/// on Windows and is deleted regardless on Linux, where an open handle keeps
/// working against a name that has gone, so a test that arranged the refusal by
/// locking a file would assert one thing on the machine it was written on and
/// the opposite on the runner.
///
/// The behaviour under test is the sweep's, not the file system's: a removal
/// that fails is counted rather than thrown, and the run starts anyway. Through
/// this seam that is decidable, and no test needs a platform to cooperate.
/// </remarks>
public interface IFileRemoval
{
    /// <summary>
    /// Removes one file.
    /// </summary>
    /// <param name="path">The file to remove.</param>
    /// <remarks>
    /// A file that is already gone is not an error, which is what
    /// <c>File.Delete</c> does and what a caller sweeping leftovers needs: two
    /// things racing for the same stale file is the ordinary case, not a fault.
    /// </remarks>
    void Delete(string path);
}
