using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Detection;
using Jellyfin.Plugin.WhisperSubtitles.Selection;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// Checks every value the configuration holds, once, before anything reads one.
/// </summary>
/// <remarks>
/// The file is XML on disk that a person can edit, and it is read back on every
/// server start. Checking a value where it is used means checking it in as many
/// places as use it, and the place that forgets is the one that meets the hostile
/// value.
///
/// Nothing here refuses to load. A value that fails its rule falls back to the
/// documented default for that field and produces a complaint naming the field,
/// the reason and what is in force instead, because an operator whose plugin will
/// not start has no page to repair it from. The one thing that never falls back
/// to a working state is the backend: falling back to doing work is not failing
/// closed, so an unusable backend setting reaches selection unchanged and
/// selection answers with the do-nothing backend.
///
/// One file is stood back from rather than read field by field, and it is the one
/// written by a later release than this one. That is not a value failing its rule;
/// it is a document in a vocabulary this release does not have, and #41 is where
/// it is argued.
/// </remarks>
public static class ConfigurationValidation
{
    /// <summary>
    /// The schema version this release writes and knows how to read.
    /// </summary>
    /// <remarks>
    /// One from the first release rather than zero, so that a file carrying no
    /// version and a file carrying a version somebody zeroed by hand are
    /// distinguishable: the absent element leaves the property at its initialiser,
    /// and only an explicit element can produce anything else.
    /// </remarks>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The default for <see cref="PluginConfiguration.TargetLanguage"/> and for a
    /// library row that names nothing: no target at all.
    /// </summary>
    /// <remarks>
    /// Blank is not detection. A run reads it as no target and selects nothing,
    /// which is the only fail-closed answer for a field nobody has filled in.
    /// </remarks>
    public const string NoTargetLanguage = "";

    /// <summary>
    /// The default for <see cref="PluginConfiguration.Backend"/>: nothing chosen.
    /// </summary>
    public const string NoBackendChosen = "";

    private static readonly string[] _validatedFields =
    [
        nameof(PluginConfiguration.SchemaVersion),
        nameof(PluginConfiguration.Backend),
        nameof(PluginConfiguration.TargetLanguage),
        nameof(PluginConfiguration.LibraryTargets),
    ];

    /// <summary>
    /// Gets the name of every setting a rule here decides.
    /// </summary>
    /// <remarks>
    /// Published so a test can compare it against the properties the configuration
    /// declares. "Every field is validated" is otherwise a sentence somebody has to
    /// keep true by hand, and the field that gets missed is the one added last, in a
    /// branch about the feature that needed it.
    /// </remarks>
    public static IReadOnlyList<string> ValidatedFields => _validatedFields;

    /// <summary>
    /// Reads a configuration the server has already deserialised.
    /// </summary>
    /// <param name="configuration">Whatever came off the disk.</param>
    /// <returns>The settings in force and every value that was refused.</returns>
    public static ConfigurationLoad Of(PluginConfiguration? configuration)
    {
        var complaints = new List<SettingComplaint>();

        if (configuration is null)
        {
            complaints.Add(new SettingComplaint(
                "configuration",
                "there is no configuration to read.",
                "Every setting is at its default and nothing is transcribed."));

            return Defaults(complaints);
        }

        if (configuration.SchemaVersion > CurrentSchemaVersion)
        {
            complaints.Add(WrittenByANewerRelease(configuration.SchemaVersion));

            return Defaults(complaints);
        }

        var version = SchemaVersion(configuration.SchemaVersion, complaints);
        var backend = Backend(configuration.Backend, complaints);
        var target = TargetLanguage(configuration.TargetLanguage, complaints);
        var byLibrary = LibraryTargets(configuration.LibraryTargets, complaints);

        return new ConfigurationLoad(
            new SettingsInForce(version, backend, target, byLibrary),
            complaints);
    }

    /// <summary>
    /// The complaint for a file a later release wrote, and the reason nothing else
    /// in it is looked at.
    /// </summary>
    /// <remarks>
    /// Returning before any other field is read is the whole of it. Every value in
    /// such a file may parse and may be a value this release accepts, and that says
    /// nothing: what a later release means by a field is a thing this one cannot
    /// know, so a backend name it recognises is a backend name whose meaning may
    /// have moved underneath it. Reading them anyway is a run configured out of a
    /// document written in a vocabulary nobody here has.
    ///
    /// The direction is the one the field only goes in. A file older than this
    /// release is migrated forward, because this release knows what the earlier
    /// vocabulary meant; a newer one is not migrated backwards, because it does
    /// not.
    ///
    /// Nothing writes the file back, so it survives a downgrade untouched and the
    /// release that wrote it finds its own settings when the operator returns to
    /// it. That is a property of what this plugin does not do rather than of
    /// anything here, and the command behind it is in the pull request that landed
    /// this branch.
    /// </remarks>
    /// <param name="declared">The version the file declared.</param>
    /// <returns>The one complaint such a file produces.</returns>
    private static SettingComplaint WrittenByANewerRelease(int declared) =>
        new(
            nameof(PluginConfiguration.SchemaVersion),
            string.Format(
                CultureInfo.InvariantCulture,
                "the file was written by a newer version of this plugin: it declares schema version {0} and the newest this release knows is {1}.",
                declared,
                CurrentSchemaVersion),
            "Nothing else in the file is read, every setting is at its default, and nothing is transcribed.");

    private static ConfigurationLoad Defaults(List<SettingComplaint> complaints) =>
        new(
            new SettingsInForce(
                CurrentSchemaVersion,
                NoBackendChosen,
                NoTargetLanguage,
                new Dictionary<Guid, string>()),
            complaints);

    /// <remarks>
    /// Only the low side reaches here. A version above the current one is answered
    /// in <see cref="Of"/> before any other field is read, so what is left is a
    /// number no release ever wrote: an absent element cannot produce it, and
    /// nothing below one is a version at all. That is a malformed field rather than
    /// a file from elsewhere, so it falls back like every other field does.
    /// </remarks>
    /// <param name="declared">The version the file declared.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>The version the rest of the file is read under.</returns>
    private static int SchemaVersion(int declared, List<SettingComplaint> complaints)
    {
        if (declared >= 1 && declared <= CurrentSchemaVersion)
        {
            return declared;
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.SchemaVersion),
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is not a version this release writes, and the newest it knows is {1}.",
                declared,
                CurrentSchemaVersion),
            string.Format(
                CultureInfo.InvariantCulture,
                "The file is read under version {0}.",
                CurrentSchemaVersion)));

        return CurrentSchemaVersion;
    }

    private static string Backend(string? declared, List<SettingComplaint> complaints)
    {
        if (string.IsNullOrWhiteSpace(declared) || BackendNames.IsKnown(declared))
        {
            return declared?.Trim() ?? NoBackendChosen;
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.Backend),
            string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\" is not a backend this plugin has, and the ones it has are: {1}.",
                declared.Trim(),
                string.Join(", ", BackendNames.Known)),
            "Nothing is transcribed."));

        // Handed on rather than blanked. Selection refuses a name it does not know
        // and says so naming the name, which is the sentence that repairs a typo;
        // a blank would have it report that nothing is configured, which is a
        // different state with a different repair. Neither transcribes anything.
        return declared.Trim();
    }

    private static string TargetLanguage(string? declared, List<SettingComplaint> complaints)
    {
        if (LanguageTarget.IsAbsent(declared) || LanguageTarget.IsDetection(declared))
        {
            return declared?.Trim() ?? NoTargetLanguage;
        }

        var mapping = SubtitleLanguageCode.For(declared);

        if (mapping.Outcome == LanguageCodeOutcome.Mapped)
        {
            return declared!.Trim();
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.TargetLanguage),
            string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\" is not a language this server can label a subtitle with, and \"{1}\" is the word for letting the backend decide.",
                declared!.Trim(),
                LanguageTarget.Detect),
            "No language is chosen, so no item is selected."));

        return NoTargetLanguage;
    }

    private static Dictionary<Guid, string> LibraryTargets(
        LibraryLanguageTarget[]? rows,
        List<SettingComplaint> complaints)
    {
        var targets = new Dictionary<Guid, string>();

        if (rows is null)
        {
            return targets;
        }

        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];

            if (row is null)
            {
                complaints.Add(Row(index, "is empty.", "That library follows the default."));
                continue;
            }

            if (!Guid.TryParse(row.LibraryId, out var library))
            {
                complaints.Add(Row(
                    index,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "names \"{0}\", which is not a library identifier.",
                        row.LibraryId),
                    "That row is dropped and no library is affected."));
                continue;
            }

            // A row that asks for nothing is a library following the default, which
            // is the state the page stores by removing the row. One left behind by
            // hand means the same thing and is not worth complaining about; keeping
            // it would override a default the operator did choose with nothing.
            if (LanguageTarget.IsAbsent(row.Target))
            {
                continue;
            }

            if (!LanguageTarget.IsDetection(row.Target)
                && SubtitleLanguageCode.For(row.Target).Outcome != LanguageCodeOutcome.Mapped)
            {
                complaints.Add(Row(
                    index,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "asks for \"{0}\", which is not a language this server can label a subtitle with.",
                        row.Target.Trim()),
                    "That library follows the default."));
                continue;
            }

            if (targets.ContainsKey(library))
            {
                complaints.Add(Row(
                    index,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "is the second row for library {0}.",
                        library),
                    "The later row is the one that applies."));
            }

            targets[library] = row.Target.Trim();
        }

        return targets;
    }

    private static SettingComplaint Row(int index, string problem, string inForce) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}[{1}]",
                nameof(PluginConfiguration.LibraryTargets),
                index),
            problem,
            inForce);
}
