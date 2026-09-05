using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// What the transcription tool said about itself, read off what it printed and
/// labelled as what it was: a version, a description, or nothing.
/// </summary>
/// <remarks>
/// The labelling is the point, and #324 decided it on 2026-09-05: a line printed
/// to <c>--version</c> by a tool that exited cleanly is a version; a line printed
/// to <c>--help</c> is the tool describing itself and is never called a version;
/// and a tool that prints nothing to either is reported as present and silent
/// rather than as anything the probe did not see.
/// </remarks>
public sealed class ToolIdentity
{
    private ToolIdentity(ToolIdentityKind kind, string? text)
    {
        Kind = kind;
        Text = text;
    }

    /// <summary>
    /// Gets what kind of answer the tool gave.
    /// </summary>
    public ToolIdentityKind Kind { get; }

    /// <summary>
    /// Gets the line the tool printed, or null when it printed nothing.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// The tool answered <c>--version</c> with a line and a clean exit.
    /// </summary>
    /// <param name="line">The first non-empty line it printed.</param>
    /// <returns>The identity.</returns>
    public static ToolIdentity Version(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        return new ToolIdentity(ToolIdentityKind.Version, line);
    }

    /// <summary>
    /// The tool printed no version and described itself to <c>--help</c>.
    /// </summary>
    /// <param name="line">The first non-empty line it printed.</param>
    /// <returns>The identity.</returns>
    public static ToolIdentity Description(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        return new ToolIdentity(ToolIdentityKind.Description, line);
    }

    /// <summary>
    /// The tool printed nothing to either flag.
    /// </summary>
    /// <returns>The identity.</returns>
    public static ToolIdentity Silent() => new(ToolIdentityKind.Silent, null);

    /// <summary>
    /// The sentence an operator reads, which says what was printed and to which
    /// flag, and calls a help line a description rather than a version.
    /// </summary>
    /// <returns>One sentence.</returns>
    public string Describe() =>
        Kind switch
        {
            ToolIdentityKind.Version => string.Format(
                CultureInfo.InvariantCulture,
                "The tool reports its version as \"{0}\" to {1}.",
                Text,
                ToolIdentityProbe.VersionFlag),
            ToolIdentityKind.Description => string.Format(
                CultureInfo.InvariantCulture,
                "The tool prints no version to {0}; to {1} it describes itself as \"{2}\".",
                ToolIdentityProbe.VersionFlag,
                ToolIdentityProbe.HelpFlag,
                Text),
            ToolIdentityKind.Silent => string.Format(
                CultureInfo.InvariantCulture,
                "The tool printed nothing to {0} or {1}; it is there and silent.",
                ToolIdentityProbe.VersionFlag,
                ToolIdentityProbe.HelpFlag),
            _ => throw new InvalidOperationException("A tool identity of a kind this type does not declare."),
        };
}
