using System;
using System.Collections.Generic;
using System.Text;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The bytes of a finished subtitle: the provenance header, then whatever the
/// format writer produced.
/// </summary>
/// <remarks>
/// A separate type rather than a second method on the writer, because the header
/// is the same three lines whichever format is being written and a per-format
/// copy of it is a place for two formats to disagree. What a writer knows is its
/// format; what this knows is that a file this plugin produced says so.
/// </remarks>
public static class MarkedSubtitleFile
{
    private const string LineEnding = "\r\n";

    private static readonly UTF8Encoding _utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Composes the file a run publishes.
    /// </summary>
    /// <param name="writer">The format writer.</param>
    /// <param name="segments">The timed segments, in the order they occur.</param>
    /// <param name="provenance">What produced them.</param>
    /// <returns>The bytes of the file, ready to be written as they are.</returns>
    public static byte[] Compose(
        ISubtitleFormatWriter writer,
        IReadOnlyList<TimedSegment> segments,
        SubtitleProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(provenance);

        var header = Header(provenance);
        var body = writer.Write(segments);

        var composed = new byte[header.Length + body.Length];
        header.CopyTo(composed, 0);
        body.CopyTo(composed, header.Length);

        return composed;
    }

    /// <summary>
    /// The header on its own.
    /// </summary>
    /// <param name="provenance">What produced the file.</param>
    /// <returns>The bytes that go before the first cue, blank line included.</returns>
    /// <remarks>
    /// It ends with a blank line, which is what separates it from the first cue.
    /// The bytes after it are the writer's own output unchanged, so removing the
    /// header is removing exactly this prefix and never a change to what the body
    /// says.
    /// </remarks>
    public static byte[] Header(SubtitleProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);

        var builder = new StringBuilder();

        foreach (var line in provenance.HeaderLines())
        {
            builder.Append(line).Append(LineEnding);
        }

        builder.Append(LineEnding);

        return _utf8WithoutByteOrderMark.GetBytes(builder.ToString());
    }
}
