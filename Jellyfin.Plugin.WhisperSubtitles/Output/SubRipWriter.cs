using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jellyfin.Plugin.WhisperSubtitles.Backends;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Writes SubRip, which is the one format this plugin produces for the first
/// release.
/// </summary>
/// <remarks>
/// Two choices here differ from the server's own SrtWriter and are deliberate
/// rather than accidental. That writer constructs its StreamWriter with
/// Encoding.UTF8, which emits a byte order mark, and its WriteLine calls take
/// Environment.NewLine, so the same subtitle comes out differently on a Linux
/// server and a Windows one. This writer emits no byte order mark and always
/// ends a line with a carriage return and a line feed, which is what SubRip has
/// used since it was written and what makes the bytes a test can assert on
/// identical everywhere the plugin runs.
/// </remarks>
public sealed class SubRipWriter : ISubtitleFormatWriter
{
    private const string LineEnding = "\r\n";

    private static readonly UTF8Encoding _utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public string FileExtension => "srt";

    /// <inheritdoc />
    public byte[] Write(IReadOnlyList<TimedSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var builder = new StringBuilder();

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];

            builder.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(LineEnding);

            builder.Append(Timestamp(segment.Start))
                .Append(" --> ")
                .Append(Timestamp(segment.End))
                .Append(LineEnding);

            builder.Append(SingleLine(segment.Text)).Append(LineEnding);

            builder.Append(LineEnding);
        }

        return _utf8WithoutByteOrderMark.GetBytes(builder.ToString());
    }

    private static string Timestamp(TimeSpan at) =>
        at.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);

    /// <summary>
    /// Flattens a segment's text onto one line.
    /// </summary>
    /// <remarks>
    /// A blank line is what separates one cue from the next, so a segment whose
    /// text carries a line break would end the cue early and make every following
    /// cue unreadable. A backend returning text with a newline in it is not a
    /// defect on its side, so this is where it is dealt with.
    /// </remarks>
    private static string SingleLine(string text) =>
        text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}
