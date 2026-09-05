using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The record of what this plugin published, appended to as each file takes its
/// name and read back when an operator asks what could be removed.
/// </summary>
/// <remarks>
/// A seam, so a publish can be driven in a test with the record's answer read
/// off a double rather than off a disk, and so a record that refuses to take an
/// entry can be arranged without a full disk. The real one is
/// <see cref="PublishedSubtitleRecordFile"/> and the composition root is where
/// it is named for the container.
/// </remarks>
public interface IPublishedSubtitleRecord
{
    /// <summary>
    /// Adds one entry, before the file it names is visible under its name.
    /// </summary>
    /// <param name="entry">What was published.</param>
    /// <param name="cancellationToken">Stops the append.</param>
    /// <returns>A task that completes once the entry is durable.</returns>
    /// <remarks>
    /// The publisher calls this between writing the bytes and revealing the file,
    /// so a failure here leaves no file under its final name: what the record does
    /// not name was never published.
    /// </remarks>
    Task AppendAsync(PublishedSubtitle entry, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every entry, oldest first.
    /// </summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The entries that could be read, and how many lines could not be.</returns>
    Task<PublishedSubtitleRecordReading> ReadAsync(CancellationToken cancellationToken);
}
