using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The record of what this plugin published, as one file in the directory the
/// server hands this plugin for its data: one JSON object per line, appended and
/// never rewritten.
/// </summary>
/// <remarks>
/// Plugin data and not the media tree, which #43 decided on 2026-09-05: the
/// record goes where the server puts a plugin's own files and removes them with
/// the plugin, and a file in the operator's library is never the place a plugin
/// keeps its own bookkeeping.
///
/// One line per entry, appended, so a publish costs one write of one line and
/// two publishes at once cannot lose each other's entry the way two rewrites of
/// one document can. A line that will not parse is counted and skipped, never
/// repaired and never taken as a reason to stop reading the rest, because the
/// file is one a person can open in an editor.
///
/// The directory arrives as a string the way the extractor's working directory
/// does. Which directory a running server hands this plugin is the composition
/// root's question and is not answered here.
/// </remarks>
public sealed class PublishedSubtitleRecordFile : IPublishedSubtitleRecord
{
    /// <summary>
    /// The file's name inside the plugin's data directory.
    /// </summary>
    public const string FileName = "published-subtitles.jsonl";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.General);

    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishedSubtitleRecordFile"/> class.
    /// </summary>
    /// <param name="directory">The directory the server hands this plugin for its data.</param>
    public PublishedSubtitleRecordFile(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        DataDirectory = directory;
        _path = Path.Combine(directory, FileName);
    }

    /// <summary>
    /// Gets the directory the record is kept in.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Gets the path of the record file.
    /// </summary>
    public string FilePath => _path;

    /// <inheritdoc />
    public async Task AppendAsync(PublishedSubtitle entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // The directory is created rather than assumed, because a plugin's data
        // directory exists only once something has put a file in it, and the first
        // publish is that something.
        Directory.CreateDirectory(DataDirectory);

        var line = JsonSerializer.Serialize(new Line(entry), _json) + "\n";

        await File.AppendAllTextAsync(_path, line, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PublishedSubtitleRecordReading> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new PublishedSubtitleRecordReading([], 0);
        }

        var entries = new List<PublishedSubtitle>();
        var unreadable = 0;

        foreach (var raw in await File.ReadAllLinesAsync(_path, Encoding.UTF8, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var entry = Parse(raw);

            if (entry is null)
            {
                unreadable++;
            }
            else
            {
                entries.Add(entry);
            }
        }

        return new PublishedSubtitleRecordReading(entries, unreadable);
    }

    private static PublishedSubtitle? Parse(string raw)
    {
        Line? line;

        try
        {
            line = JsonSerializer.Deserialize<Line>(raw, _json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (line is null
            || line.ItemId is null
            || string.IsNullOrWhiteSpace(line.Path)
            || line.SizeInBytes is null or < 0
            || string.IsNullOrWhiteSpace(line.Sha256)
            || line.WrittenAt is null)
        {
            return null;
        }

        return new PublishedSubtitle(line.ItemId.Value, line.Path, line.SizeInBytes.Value, line.Sha256, line.WrittenAt.Value);
    }

    /// <summary>
    /// The shape one line takes on disk. Every field is optional on the way in so
    /// that a line missing one is refused as a line rather than thrown as a file.
    /// </summary>
    private sealed class Line
    {
        public Line()
        {
        }

        public Line(PublishedSubtitle entry)
        {
            ItemId = entry.ItemId;
            Path = entry.Path;
            SizeInBytes = entry.SizeInBytes;
            Sha256 = entry.Sha256;
            WrittenAt = entry.WrittenAt;
        }

        public Guid? ItemId { get; set; }

        public string? Path { get; set; }

        public long? SizeInBytes { get; set; }

        public string? Sha256 { get; set; }

        public DateTimeOffset? WrittenAt { get; set; }
    }
}
