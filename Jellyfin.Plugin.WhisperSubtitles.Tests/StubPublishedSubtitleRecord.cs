using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Output;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A record held in memory, which says what it was handed and when, and can be
/// told to refuse an entry so the publish that depends on it can be watched
/// leaving nothing behind.
/// </summary>
internal sealed class StubPublishedSubtitleRecord : IPublishedSubtitleRecord
{
    private readonly List<PublishedSubtitle> _entries = [];
    private readonly Exception? _refusal;
    private readonly Action<PublishedSubtitle>? _atAppend;

    private StubPublishedSubtitleRecord(Exception? refusal, Action<PublishedSubtitle>? atAppend)
    {
        _refusal = refusal;
        _atAppend = atAppend;
    }

    /// <summary>
    /// Gets what has been appended, oldest first.
    /// </summary>
    public IReadOnlyList<PublishedSubtitle> Entries => _entries;

    /// <summary>
    /// Gets how many lines this record reports as unreadable, which is a number the
    /// test chooses.
    /// </summary>
    public int UnreadableLines { get; set; }

    public static StubPublishedSubtitleRecord Empty() => new(null, null);

    /// <summary>
    /// A record that throws on every append, standing in for a full disk.
    /// </summary>
    public static StubPublishedSubtitleRecord Refusing(Exception refusal) => new(refusal, null);

    /// <summary>
    /// A record that runs the given action at the moment of each append, so a test
    /// can look at the disk while the publisher is between writing and revealing.
    /// </summary>
    public static StubPublishedSubtitleRecord Watching(Action<PublishedSubtitle> atAppend) => new(null, atAppend);

    public StubPublishedSubtitleRecord With(PublishedSubtitle entry)
    {
        _entries.Add(entry);

        return this;
    }

    public Task AppendAsync(PublishedSubtitle entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_refusal is not null)
        {
            throw _refusal;
        }

        _atAppend?.Invoke(entry);
        _entries.Add(entry);

        return Task.CompletedTask;
    }

    public Task<PublishedSubtitleRecordReading> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new PublishedSubtitleRecordReading(_entries.ToArray(), UnreadableLines));
    }
}
