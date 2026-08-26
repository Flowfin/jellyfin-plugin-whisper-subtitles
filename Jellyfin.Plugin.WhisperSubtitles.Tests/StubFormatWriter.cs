using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The format writer every test uses when the format is not what is being
/// asserted.
/// </summary>
/// <remarks>
/// `ISubtitleFormatWriter` was a seam with no stand-in until this landed, which
/// <see cref="SeamDoubleTests"/> found. Every caller of it in this suite handed
/// over the real SubRip writer, so anything asserted about a type that merely
/// HOLDS a writer was asserted against SubRip's bytes as well, and a change that
/// quietly depended on them - a header composer that assumed the body opens with
/// a cue number, or that it ends in a blank line - would have passed.
///
/// So the bytes here are deliberately nothing like a subtitle. A double that
/// produced plausible SubRip would reintroduce the coupling it exists to break:
/// the point is that a caller must not care, and the cheapest way to say so is to
/// hand it something that could not be mistaken for the real format.
///
/// It records what it was asked to write, because the other half of the coupling
/// is a caller that reaches past the seam and formats something itself, and the
/// only evidence against that is what actually arrived here.
/// </remarks>
internal sealed class StubFormatWriter : ISubtitleFormatWriter
{
    private readonly List<IReadOnlyList<TimedSegment>> _asked = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StubFormatWriter"/> class.
    /// </summary>
    /// <param name="body">The bytes it answers with.</param>
    /// <param name="fileExtension">The extension it claims.</param>
    public StubFormatWriter(byte[]? body = null, string fileExtension = "stub")
    {
        Body = body ?? [0x00, 0x01, 0x02, 0x03];
        FileExtension = fileExtension;
    }

    /// <summary>
    /// Gets the extension this format claims.
    /// </summary>
    public string FileExtension { get; }

    /// <summary>
    /// Gets the bytes this writer answers with.
    /// </summary>
    public byte[] Body { get; }

    /// <summary>
    /// Gets the segments each write was asked for, in order.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<TimedSegment>> Asked => _asked;

    /// <inheritdoc />
    public byte[] Write(IReadOnlyList<TimedSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        _asked.Add(segments);

        return Body;
    }
}
