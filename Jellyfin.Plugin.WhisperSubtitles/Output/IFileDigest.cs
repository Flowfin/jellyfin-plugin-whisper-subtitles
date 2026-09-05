using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The seam the listing reads a file's bytes through, to say whether a file is
/// still the one this plugin wrote.
/// </summary>
/// <remarks>
/// A seam of its own rather than a field on the readiness probe's file facts,
/// because a digest reads the whole file and the probe describes a model that can
/// be gigabytes; a probe that hashed on every page load would be the wrong price
/// for the question it asks. This is paid once per recorded file, when an
/// operator asks what could be removed, which #43 decided is the right price for
/// an action taken on purpose.
/// </remarks>
public interface IFileDigest
{
    /// <summary>
    /// Reads the file and answers with its size and SHA-256, or with nothing when
    /// there is no file at the path.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The file's size and digest, or null when nothing is at the path.</returns>
    Task<FileDigest?> DigestAsync(string path, CancellationToken cancellationToken);
}
