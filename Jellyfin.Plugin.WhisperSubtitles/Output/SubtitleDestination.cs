using System;
using System.Globalization;
using System.IO;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Decides which folder a subtitle belongs in, and refuses a name that would
/// leave it.
/// </summary>
/// <remarks>
/// Pure, and separate from the write. Where a file goes is a decision an operator
/// has already made in their library settings, and it can be read, argued with
/// and tested without a disk. <see cref="AtomicSubtitleFile"/> holds the write
/// itself and says of this decision that it is not its business.
///
/// The two folders are the whole surface. Jellyfin's own subtitle manager saves a
/// downloaded subtitle either next to the media or into the item's metadata
/// folder depending on the library's SaveSubtitlesWithMedia option, and this
/// plugin honours the same option because a plugin that wrote into a media tree
/// the operator had closed would be overruling them silently.
/// </remarks>
public static class SubtitleDestination
{
    /// <summary>
    /// Chooses the folder for one item's subtitle.
    /// </summary>
    /// <param name="item">Where the item's files are, and what its library allows.</param>
    /// <param name="kind">Which of the two places it is.</param>
    /// <returns>The folder the subtitle belongs in.</returns>
    /// <remarks>
    /// The media file's own folder rather than a folder passed in beside it, so a
    /// caller cannot hand in a directory that has nothing to do with the item.
    /// </remarks>
    public static string Choose(ItemLocation item, out SubtitleDestinationKind kind)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.SaveSubtitlesWithMedia)
        {
            kind = SubtitleDestinationKind.BesideTheMedia;

            var beside = Path.GetDirectoryName(item.MediaFilePath);

            if (string.IsNullOrEmpty(beside))
            {
                // A media path with no folder is not a library this plugin can write
                // into. Falling back to the metadata folder here would put the file
                // somewhere the operator did not ask for, quietly, so it refuses.
                throw new SubtitleNotWrittenException(
                    SubtitleWriteFailure.DestinationUnwritable,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The item's media path names no folder to write beside: {0}",
                        item.MediaFilePath));
            }

            return beside;
        }

        kind = SubtitleDestinationKind.InTheMetadataFolder;

        return item.MetadataFolderPath;
    }

    /// <summary>
    /// Puts a file name into a chosen folder, and refuses one that would not stay
    /// there.
    /// </summary>
    /// <param name="destination">The folder chosen for this item.</param>
    /// <param name="fileName">The name of the file, and nothing else.</param>
    /// <returns>The full path to write.</returns>
    /// <remarks>
    /// The check is on the resolved path rather than on the characters in the name.
    /// A name is refused because of where it ends up, not because it matched a
    /// pattern somebody thought of, and the two differ on every platform that has
    /// its own idea of what a separator is.
    ///
    /// What produces the name is #26 and #33, which build it from the media file
    /// name and a language code. Neither is trusted here: a language code that
    /// reached this plugin from a backend is untrusted input, and this is the last
    /// place before a path is opened.
    /// </remarks>
    public static string Resolve(string destination, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var folder = Path.GetFullPath(destination);
        var candidate = Path.GetFullPath(Path.Combine(folder, fileName));

        var inside = string.Equals(
            Trimmed(Path.GetDirectoryName(candidate)),
            Trimmed(folder),
            StringComparison.Ordinal);

        if (!inside)
        {
            throw new SubtitleNotWrittenException(
                SubtitleWriteFailure.NameWouldLeaveTheDestination,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The subtitle name {0} does not stay in the folder chosen for it, so nothing was written.",
                    fileName));
        }

        return candidate;
    }

    private static string Trimmed(string? path) =>
        path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
}
