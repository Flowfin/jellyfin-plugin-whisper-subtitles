using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A disk of digests: what each path holds now, arranged by the test and
/// changeable between the two steps of a removal, which is the case the listing
/// exists to get right and one a real disk cannot be asked to produce on cue.
/// </summary>
internal sealed class StubFileDigest : IFileDigest
{
    private readonly Dictionary<string, FileDigest> _byPath = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the paths that were read, in the order they were read.
    /// </summary>
    public List<string> Read { get; } = [];

    public static StubFileDigest Empty() => new();

    /// <summary>
    /// A file holding exactly the bytes an entry records.
    /// </summary>
    public StubFileDigest Holding(PublishedSubtitle entry)
    {
        _byPath[entry.Path] = new FileDigest(entry.SizeInBytes, entry.Sha256);

        return this;
    }

    /// <summary>
    /// A file holding other bytes than an entry records.
    /// </summary>
    public StubFileDigest HoldingSomethingElseAt(PublishedSubtitle entry)
    {
        _byPath[entry.Path] = new FileDigest(entry.SizeInBytes + 7, new string('f', 64));

        return this;
    }

    /// <summary>
    /// Nothing at the path any more.
    /// </summary>
    public StubFileDigest Without(string path)
    {
        _byPath.Remove(path);

        return this;
    }

    public Task<FileDigest?> DigestAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Read.Add(path);

        return Task.FromResult(_byPath.TryGetValue(path, out var digest) ? digest : null);
    }
}
