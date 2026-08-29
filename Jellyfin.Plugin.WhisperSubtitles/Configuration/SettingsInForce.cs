using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;

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
    /// <param name="itemsAtOnce">How many items the run transcribes at once.</param>
    /// <param name="threadsPerItem">How many threads one transcription may use.</param>
    /// <param name="localToolPath">The Whisper program the local backend runs, or none named.</param>
    /// <param name="localModelPath">The model file it is handed, or none named.</param>
    /// <param name="failuresBeforeQuarantine">How many counted failures an item gets before it is quarantined.</param>
    public SettingsInForce(
        int schemaVersion,
        string backend,
        string targetLanguage,
        IReadOnlyDictionary<Guid, string> targetLanguagesByLibrary,
        int itemsAtOnce,
        int threadsPerItem,
        string localToolPath,
        string localModelPath,
        int failuresBeforeQuarantine)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemsAtOnce, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(threadsPerItem, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            failuresBeforeQuarantine,
            RetryPolicy.SmallestFailureLimit);

        SchemaVersion = schemaVersion;
        Backend = backend;
        TargetLanguage = targetLanguage;
        TargetLanguagesByLibrary = targetLanguagesByLibrary;
        ItemsAtOnce = itemsAtOnce;
        ThreadsPerItem = threadsPerItem;
        LocalToolPath = localToolPath;
        LocalModelPath = localModelPath;
        FailuresBeforeQuarantine = failuresBeforeQuarantine;
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

    /// <summary>
    /// Gets how many items the run transcribes at once.
    /// </summary>
    /// <remarks>
    /// A number of items rather than the zero the file may carry. The absence of a
    /// choice is a state of the document and not a state a run can be in, so it is
    /// resolved once, here, against the machine the run is on; a run reading the
    /// disk shape would have to decide what zero means every time it looked, and
    /// the place that forgot would ask for no items at all.
    ///
    /// Never below one, refused at construction rather than left to the caller,
    /// because every value this type can hold is one a run will act on.
    /// </remarks>
    public int ItemsAtOnce { get; }

    /// <summary>
    /// Gets how many threads one transcription may use.
    /// </summary>
    /// <remarks>
    /// Resolved for the reason <see cref="ItemsAtOnce"/> is, and never below one for
    /// the same reason.
    /// </remarks>
    public int ThreadsPerItem { get; }

    /// <summary>
    /// Gets the Whisper program the local backend runs, or none named.
    /// </summary>
    /// <remarks>
    /// Not refused at construction, and that is the difference from the two limits
    /// above. Every number they can hold is one a run will act on, so a value
    /// outside their range has no meaning; a path nobody has typed has one, and it
    /// is the state a fresh install is in. What answers it is selection reporting
    /// that the local backend is not configured, which is a sentence an operator can
    /// repair from, rather than a plugin that fails to construct its own settings.
    /// </remarks>
    public string LocalToolPath { get; }

    /// <summary>
    /// Gets the model file the local backend hands that program, or none named.
    /// </summary>
    /// <remarks>
    /// Empty for the reason <see cref="LocalToolPath"/> may be empty. Named rather
    /// than checked: nothing here has looked at a disk, so a path in this property
    /// is one somebody typed and not one that opens.
    /// </remarks>
    public string LocalModelPath { get; }

    /// <summary>
    /// Gets how many counted failures an item gets before it is quarantined.
    /// </summary>
    /// <remarks>
    /// Resolved rather than the zero the file may carry, for the reason
    /// <see cref="ItemsAtOnce"/> is, and refused below
    /// <see cref="RetryPolicy.SmallestFailureLimit"/> at construction because every
    /// value this type can hold is one <see cref="RetryPolicy.Record"/> will act on,
    /// and that method throws on the same values. Refusing here rather than there is
    /// the difference between a run that cannot start and a page an operator can
    /// repair the file from.
    /// </remarks>
    public int FailuresBeforeQuarantine { get; }
}
