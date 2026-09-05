using System;
using Jellyfin.Plugin.WhisperSubtitles.Output;

namespace Jellyfin.Plugin.WhisperSubtitles.Api;

/// <summary>
/// One line of what the page is shown when it asks what this plugin wrote: the
/// record's entry and what the listing found at its path, flattened for a page
/// that reads text.
/// </summary>
public sealed class GeneratedSubtitleLine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSubtitleLine"/> class.
    /// </summary>
    /// <param name="finding">What the listing found for one recorded file.</param>
    public GeneratedSubtitleLine(GeneratedSubtitleFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        ItemId = finding.Entry.ItemId;
        Path = finding.Entry.Path;
        SizeInBytes = finding.Entry.SizeInBytes;
        Sha256 = finding.Entry.Sha256;
        WrittenAt = finding.Entry.WrittenAt;
        State = finding.State.ToString();
    }

    /// <summary>
    /// Gets the library item the subtitle belongs to.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets the path the file was published under.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets how many bytes were written.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the SHA-256 the record holds for the file.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Gets when the file took its name.
    /// </summary>
    public DateTimeOffset WrittenAt { get; }

    /// <summary>
    /// Gets what the listing found, as the name of a <see cref="GeneratedSubtitleState"/>.
    /// </summary>
    /// <remarks>
    /// A name rather than a number, so the page compares a word it can read and a
    /// serializer setting on the server cannot turn it into an integer the page
    /// does not expect.
    /// </remarks>
    public string State { get; }
}
