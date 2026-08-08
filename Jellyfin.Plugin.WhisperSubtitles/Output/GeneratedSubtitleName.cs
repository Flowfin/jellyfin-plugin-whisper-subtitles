using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The file name a generated subtitle takes, which is also how a viewer is told
/// a machine wrote it.
/// </summary>
/// <remarks>
/// The server parses an external subtitle's file name to work out its language
/// and its flags, in <c>ExternalPathParser</c> in Emby.Naming, and whatever is
/// left over after that becomes the track title clients display. So the marker
/// does not need a field anywhere: it needs to be a part of the name that the
/// server's own parser cannot mistake for a language or a flag, and it then
/// arrives in the track list without this plugin inventing a mechanism.
///
/// The server's own subtitle manager cannot produce this name. It builds one out
/// of language, forced and hearing impaired and offers no place for a title,
/// which is why the file is written here instead.
/// </remarks>
public static class GeneratedSubtitleName
{
    /// <summary>
    /// The part of the name that says a machine produced the file.
    /// </summary>
    /// <remarks>
    /// One word, no dot and no space, so no delimiter the server splits on falls
    /// inside it and it survives as a single title rather than being taken apart.
    /// It says what happened to the audio rather than naming the tool, because
    /// the person reading it in a track list is a viewer choosing a subtitle and
    /// not an operator.
    /// </remarks>
    public const string Marker = "Transcribed";

    /// <summary>
    /// The longest language code this accepts.
    /// </summary>
    /// <remarks>
    /// Three, which is ISO 639-2. Two-letter codes are accepted as well because
    /// that is what several backends answer with; anything longer is not a
    /// language code, and the point of the bound is that whatever a backend or a
    /// configuration file says ends up between two dots in a file name.
    /// </remarks>
    private const int LongestLanguageCode = 3;

    /// <summary>
    /// Builds the file name for a generated subtitle.
    /// </summary>
    /// <param name="mediaPath">The media file the subtitle is for.</param>
    /// <param name="languageCode">The language of the subtitle, as an ISO 639 code.</param>
    /// <param name="fileExtension">The subtitle format's extension, without a leading dot.</param>
    /// <returns>A file name with no directory part.</returns>
    /// <exception cref="ArgumentException">The language code is not one, or the media path names no file.</exception>
    /// <remarks>
    /// The base name comes from the media file rather than from the item's title
    /// in the library, and that is a boundary rather than a convenience. A title
    /// is metadata: it can hold a directory separator or a traversal sequence,
    /// and it arrives here from a scraper this repository did not write. A file
    /// name taken off an existing path cannot hold either, because the file it
    /// came from exists. It is also what makes the server associate the subtitle
    /// with the item at all, since it matches external subtitles to media by
    /// exactly that base name.
    /// </remarks>
    public static string For(string mediaPath, string languageCode, string fileExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        var baseName = Path.GetFileNameWithoutExtension(mediaPath);
        if (string.IsNullOrEmpty(baseName))
        {
            throw new ArgumentException(
                FormattableString.Invariant($"{mediaPath} names no file to sit beside."),
                nameof(mediaPath));
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{baseName}.{Language(languageCode)}.{Marker}.{fileExtension}");
    }

    /// <summary>
    /// Checks that a language code is one, and hands it back as it will appear in
    /// a file name.
    /// </summary>
    /// <param name="languageCode">The code to check.</param>
    /// <returns>The code, lower cased.</returns>
    /// <exception cref="ArgumentException">It is not a language code.</exception>
    /// <remarks>
    /// This is the one part of the name that does not come off an existing path.
    /// It arrives from a configuration file a person edited or from a backend's
    /// own answer, so a separator, a dot or a traversal sequence in it would
    /// otherwise reach the file system through a name this plugin built. Letters
    /// only, so none of those shapes can survive the check rather than being
    /// stripped out of it: a code that has to be repaired is a code nobody meant.
    /// </remarks>
    public static string Language(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        if (languageCode.Length > LongestLanguageCode
            || languageCode.Length < 2
            || !languageCode.All(static c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
        {
            throw new ArgumentException(
                FormattableString.Invariant(
                    $"'{languageCode}' is not a two or three letter language code, and this plugin builds a file name out of it."),
                nameof(languageCode));
        }

        return languageCode.ToLowerInvariant();
    }
}
