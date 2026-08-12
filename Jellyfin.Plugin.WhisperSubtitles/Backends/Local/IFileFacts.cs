using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// The seam a probe looks at a path through.
/// </summary>
/// <remarks>
/// Here for the same reason <see cref="Audio.IFileRemoval"/> is: the interesting
/// cases cannot be arranged on a real disk on both of the platforms this suite
/// runs on. A file that exists and may not be executed is one chmod on Linux and
/// has no spelling at all on Windows, and a test that arranged it would assert
/// one thing on the machine it was written on and nothing on the runner.
///
/// What is under test is the probe's reading of an answer, not the file system's
/// ability to produce one. Through this seam every reading is decidable, and no
/// test needs a platform to cooperate or a model file to exist.
///
/// Asynchronous although the framework's own metadata calls are not, because the
/// probe holds a deadline over this and a path can be somewhere that does not
/// answer. A network mount whose server has gone is the case: the call does not
/// fail, it waits.
/// </remarks>
public interface IFileFacts
{
    /// <summary>
    /// Reads what can be known about one path without opening it.
    /// </summary>
    /// <param name="path">The path to look at.</param>
    /// <param name="cancellationToken">Stops waiting for an answer.</param>
    /// <returns>What is there, or an answer saying nothing is.</returns>
    /// <remarks>
    /// A path that is not there is an answer rather than an error. An operator who
    /// has typed a path that does not exist is exactly who this probe is for, and
    /// a thrown exception would make the ordinary case the exceptional one.
    /// </remarks>
    Task<FileFacts> DescribeAsync(string path, CancellationToken cancellationToken);
}
