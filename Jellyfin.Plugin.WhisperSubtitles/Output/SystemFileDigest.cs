using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The real digest: reads the file on this machine and hashes it.
/// </summary>
/// <remarks>
/// Named for the container in the composition root and constructed nowhere
/// else, which <c>CompositionRootTests</c> holds. A file that is not there, or
/// that this server may not read, is answered as nothing at the path: for the
/// listing's question, a file it cannot read is one it cannot say is unchanged,
/// and one it cannot say is unchanged is one it must not remove.
/// </remarks>
public sealed class SystemFileDigest : IFileDigest
{
    /// <inheritdoc />
    public async Task<FileDigest?> DigestAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var stream = File.OpenRead(path);

            await using (stream.ConfigureAwait(false))
            {
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

                return new FileDigest(stream.Length, DigestingStream.Hex(hash));
            }
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
