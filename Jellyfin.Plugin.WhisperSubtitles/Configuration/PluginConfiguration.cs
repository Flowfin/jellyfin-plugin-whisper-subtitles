using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// The server writes this type to disk and reads it back on every start, so
/// anything declared here has to be carried by every later version or migrated
/// away from. Each setting arrives with the feature that reads it, and these two
/// arrive with the target language, which is the first decision no default can
/// make for an operator.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the language a library gets when it names none: a language
    /// code, or the reserved word for detection.
    /// </summary>
    /// <remarks>
    /// Empty out of the box, and empty means nothing has been chosen rather than
    /// meaning detect. A run reads it as no target and selects nothing, which is
    /// the only fail-closed answer: the alternative is a fresh install
    /// transcribing a library into whatever a backend guessed, off a field nobody
    /// filled in.
    /// </remarks>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the libraries that ask for something other than the default.
    /// </summary>
    /// <remarks>
    /// An array with a setter, and the analyser is right that a read-only
    /// collection is the better shape for almost any other property. It is wrong
    /// here, and the reason is which serializers this type travels through. The
    /// disk half is XmlSerializer, which fills a read-only collection happily. The
    /// configuration page half is the server's JSON API, and System.Text.Json does
    /// not populate a read-only collection property unless it is told to, so a page
    /// that posted these would have them silently dropped and an operator would
    /// watch a saved setting come back empty with nothing in any log. Both round
    /// trips are asserted rather than assumed.
    ///
    /// Only the libraries that differ appear here, so a server whose libraries all
    /// want one language stores nothing.
    /// </remarks>
#pragma warning disable CA1819 // Properties should not return arrays
    public LibraryLanguageTarget[] LibraryTargets { get; set; } = [];
#pragma warning restore CA1819

    /// <summary>
    /// The per-library targets in the shape selection reads.
    /// </summary>
    /// <returns>A target per library that names one.</returns>
    /// <remarks>
    /// A row whose library will not parse as an identifier is dropped rather than
    /// throwing. This is read on every run from a file an operator may have edited,
    /// and a single bad line taking the whole configuration down would turn a typo
    /// into a plugin that fails to load with nothing to look at. A dropped row
    /// leaves that library on the default, which is the same state it was in before
    /// the row was written.
    ///
    /// The last row for a library wins, because a file holding the same library
    /// twice has no other reading that is not a guess, and refusing the whole
    /// configuration for it would be the failure above.
    /// </remarks>
    public IReadOnlyDictionary<Guid, string> TargetLanguagesByLibrary()
    {
        var targets = new Dictionary<Guid, string>();

        foreach (var row in LibraryTargets)
        {
            if (row is null || !Guid.TryParse(row.LibraryId, out var library))
            {
                continue;
            }

            targets[library] = row.Target ?? string.Empty;
        }

        return targets;
    }
}
