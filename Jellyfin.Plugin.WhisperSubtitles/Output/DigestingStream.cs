using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Hashes subtitle bytes on their way into a file, so the record can carry the
/// digest of exactly what was written without reading the file back.
/// </summary>
/// <remarks>
/// A stream that forwards every write to the file's stream and folds the same
/// bytes into a running SHA-256. The publisher wraps the content writer's stream
/// in one of these, and asks it for the digest once the writer is done, which is
/// before the file takes its name. Reading the file back instead would cost a
/// second pass over every subtitle and would hash what the disk holds rather than
/// what was handed in, and the two are meant to be the same thing.
/// </remarks>
public sealed class DigestingStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="DigestingStream"/> class.
    /// </summary>
    /// <param name="inner">The stream the bytes are really written to.</param>
    public DigestingStream(Stream inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => _length;

    /// <summary>
    /// Gets how many bytes have been written through this stream.
    /// </summary>
    public long BytesWritten => _length;

    /// <inheritdoc />
    public override long Position
    {
        get => _length;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// The SHA-256 of a whole byte array, as lower case hexadecimal.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <returns>The digest.</returns>
    public static string Sha256Hex(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return Hex(SHA256.HashData(content));
    }

    /// <summary>
    /// A hash as lower case hexadecimal, which is the spelling the record keeps.
    /// </summary>
    /// <param name="hash">The hash bytes.</param>
    /// <returns>The hexadecimal string.</returns>
    public static string Hex(byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// The SHA-256 of everything written so far, as lower case hexadecimal.
    /// </summary>
    /// <returns>The digest.</returns>
    /// <remarks>
    /// Asked once, after the writer is done: the running hash is finished by the
    /// call, and the stream does not accept bytes after it.
    /// </remarks>
    public string Sha256Hex() => Hex(_hash.GetHashAndReset());

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        _hash.AppendData(buffer, offset, count);
        _length += count;
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        _hash.AppendData(buffer);
        _length += buffer.Length;
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        _hash.AppendData(buffer, offset, count);
        _length += count;
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _hash.AppendData(buffer.Span);
        _length += buffer.Length;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The inner stream is the caller's and is not disposed here; what is
    /// released is the running hash.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
