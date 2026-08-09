using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// The settings a run actually uses, after every value the file held has been
/// checked.
/// </summary>
/// <remarks>
/// A separate type from <see cref="PluginConfiguration"/> on purpose. That one is
/// the shape on disk, which anybody with a text editor can put anything into, and
/// it has to stay permissive enough to load a file it disagrees with. This one is
/// the shape the code reads, and every value in it has already been through a
/// rule. Handing the disk shape to the rest of the plugin is how "validated at
/// load" becomes "validated wherever somebody remembered to".
/// </remarks>
public sealed class SettingsInForce
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsInForce"/> class.
    /// </summary>
    /// <param name="schemaVersion">The version whose rules the file was read under.</param>
    /// <param name="backend">The backend name selection is given.</param>
    /// <param name="targetLanguage">The language a library gets when it names none.</param>
    /// <param name="targetLanguagesByLibrary">The libraries that ask for something else.</param>
    public SettingsInForce(
        int schemaVersion,
        string backend,
        string targetLanguage,
        IReadOnlyDictionary<Guid, string> targetLanguagesByLibrary)
    {
        SchemaVersion = schemaVersion;
        Backend = backend;
        TargetLanguage = targetLanguage;
        TargetLanguagesByLibrary = targetLanguagesByLibrary;
    }

    /// <summary>
    /// Gets the version whose rules the file was read under.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Gets the backend name selection is given.
    /// </summary>
    /// <remarks>
    /// A name that is not one this plugin has survives validation rather than
    /// being blanked, and the reason is the sentence an operator gets. Blanking it
    /// would make selection report that nothing is configured, which is a
    /// different state with a different repair; left as it is, selection says the
    /// name is not one this plugin has and lists the ones it does. Either way
    /// nothing is transcribed, so the fail-closed answer is not what is being
    /// traded here.
    /// </remarks>
    public string Backend { get; }

    /// <summary>
    /// Gets the language a library gets when it names none.
    /// </summary>
    public string TargetLanguage { get; }

    /// <summary>
    /// Gets the libraries that ask for something other than the default.
    /// </summary>
    public IReadOnlyDictionary<Guid, string> TargetLanguagesByLibrary { get; }
}
