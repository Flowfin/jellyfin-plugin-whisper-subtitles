using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Detection;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
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

    /// <summary>
    /// The default for a backend path nobody has typed: none named.
    /// </summary>
    /// <remarks>
    /// Blank rather than a location this plugin would look in. A default naming a
    /// usual place would be a plugin that runs whatever is there, on a server where
    /// nobody chose it, and the two paths this covers are a program that executes
    /// and a file it loads.
    /// </remarks>
    public const string NoPathNamed = "";

    /// <summary>
    /// The default for a remote backend setting nobody has typed: none named.
    /// </summary>
    /// <remarks>
    /// Blank, and for the URL the reason is the sharpest of the three: it is where
    /// the audio of every selected item is sent, and a default naming any host
    /// would be a plugin sending audio somewhere nobody chose. For the key, blank
    /// is also what an endpoint that wants no key is given, so it is a working
    /// state rather than only an absence.
    /// </remarks>
    public const string NoRemoteSettingNamed = "";

    /// <summary>
    /// The value a resource limit carries when nobody has chosen one.
    /// </summary>
    /// <remarks>
    /// Zero, and it is outside the range either limit accepts on purpose: no number
    /// of items and no number of threads is zero, so the sentinel cannot collide
    /// with a value an operator meant. It is also what an absent element
    /// deserialises to, which is what makes a file written before these settings
    /// existed read as nobody having chosen rather than as a zero somebody typed.
    /// </remarks>
    public const int LetTheMachineDecide = 0;

    /// <summary>
    /// The value the quarantine limit carries when nobody has chosen one.
    /// </summary>
    /// <remarks>
    /// Zero, and outside the range <see cref="RetryPolicy"/> accepts on purpose, so
    /// the sentinel cannot collide with a number of failures an operator meant. It
    /// is also what an absent element deserialises to, which is what makes every
    /// configuration this plugin has already written read as nobody having chosen.
    ///
    /// The same numeral as <see cref="LetTheMachineDecide"/> and deliberately not
    /// the same constant. What that one resolves to is a reading of the processors
    /// this server reports; what this one resolves to is a constant, because nothing
    /// about a machine says how many times a broken item is worth trying. Sharing
    /// the name would put a machine's answer and a policy's answer behind one word,
    /// and the day either default moves is the day that costs something.
    /// </remarks>
    public const int LetThePolicyDecide = 0;

    private static readonly string[] _validatedFields =
    [
        nameof(PluginConfiguration.SchemaVersion),
        nameof(PluginConfiguration.Backend),
        nameof(PluginConfiguration.TargetLanguage),
        nameof(PluginConfiguration.LibraryTargets),
        nameof(PluginConfiguration.ItemsAtOnce),
        nameof(PluginConfiguration.ThreadsPerItem),
        nameof(PluginConfiguration.LocalToolPath),
        nameof(PluginConfiguration.LocalModelPath),
        nameof(PluginConfiguration.RemoteBaseUrl),
        nameof(PluginConfiguration.RemoteApiKey),
        nameof(PluginConfiguration.RemoteModel),
        nameof(PluginConfiguration.FailuresBeforeQuarantine),
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
    /// Reads a configuration the server has already deserialised, on this machine.
    /// </summary>
    /// <param name="configuration">Whatever came off the disk.</param>
    /// <returns>The settings in force and every value that was refused.</returns>
    public static ConfigurationLoad Of(PluginConfiguration? configuration) =>
        Of(configuration, Environment.ProcessorCount);

    /// <summary>
    /// Reads a configuration the server has already deserialised, against a stated
    /// number of processors.
    /// </summary>
    /// <param name="configuration">Whatever came off the disk.</param>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>The settings in force and every value that was refused.</returns>
    /// <remarks>
    /// The machine is a parameter rather than something read here, because two of
    /// the values this decides are bounded BY the machine, and a rule that reads its
    /// own bound is one no test can put a number to: the assertion would have to
    /// compute the expected value the same way the code did, which is the code
    /// agreeing with itself. The overload above is the one the server takes and is
    /// the only place the machine is asked.
    /// </remarks>
    public static ConfigurationLoad Of(PluginConfiguration? configuration, int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        var complaints = new List<SettingComplaint>();

        if (configuration is null)
        {
            complaints.Add(new SettingComplaint(
                "configuration",
                "there is no configuration to read.",
                "Every setting is at its default and nothing is transcribed."));

            return Defaults(complaints, processorCount);
        }

        if (configuration.SchemaVersion > CurrentSchemaVersion)
        {
            complaints.Add(WrittenByANewerRelease(configuration.SchemaVersion));

            return Defaults(complaints, processorCount);
        }

        var version = SchemaVersion(configuration.SchemaVersion, complaints);
        var backend = Backend(configuration.Backend, complaints);
        var target = TargetLanguage(configuration.TargetLanguage, complaints);
        var byLibrary = LibraryTargets(configuration.LibraryTargets, complaints);
        var items = ItemsAtOnce(configuration.ItemsAtOnce, processorCount, complaints);
        var threads = ThreadsPerItem(configuration.ThreadsPerItem, processorCount, complaints);
        var tool = BackendPath(nameof(PluginConfiguration.LocalToolPath), configuration.LocalToolPath, complaints);
        var model = BackendPath(nameof(PluginConfiguration.LocalModelPath), configuration.LocalModelPath, complaints);
        var remoteUrl = RemoteBaseUrl(configuration.RemoteBaseUrl, complaints);
        var remoteKey = RemoteSetting(nameof(PluginConfiguration.RemoteApiKey), configuration.RemoteApiKey, complaints);
        var remoteModel = RemoteSetting(nameof(PluginConfiguration.RemoteModel), configuration.RemoteModel, complaints);
        var failures = FailuresBeforeQuarantine(configuration.FailuresBeforeQuarantine, complaints);

        return new ConfigurationLoad(
            new SettingsInForce(
                version,
                backend,
                target,
                byLibrary,
                items,
                threads,
                tool,
                model,
                remoteUrl,
                remoteKey,
                remoteModel,
                failures),
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

    /// <summary>
    /// The settings a run uses when nothing in the file could be honoured.
    /// </summary>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <returns>Every setting at its documented default.</returns>
    /// <remarks>
    /// Published because this is reached from two directions that must not drift
    /// apart: a file this release stands back from, decided here, and a file that
    /// would not parse at all, decided in <see cref="ConfigurationFile"/>. Those are
    /// different complaints about the same outcome, and a second copy of the
    /// defaults is how the two stop being the same outcome.
    /// </remarks>
    public static SettingsInForce DefaultSettings(int processorCount) =>
        new(
            CurrentSchemaVersion,
            NoBackendChosen,
            NoTargetLanguage,
            new Dictionary<Guid, string>(),
            ConcurrencyCap.Default,
            ThreadCount.DefaultFor(processorCount),
            NoPathNamed,
            NoPathNamed,
            NoRemoteSettingNamed,
            NoRemoteSettingNamed,
            NoRemoteSettingNamed,
            RetryPolicy.DefaultFailureLimit);

    private static ConfigurationLoad Defaults(List<SettingComplaint> complaints, int processorCount) =>
        new(DefaultSettings(processorCount), complaints);

    /// <remarks>
    /// Only the low side reaches here. A version above the current one is answered
    /// in <see cref="Of(PluginConfiguration, int)"/> before any other field is read, so what is left is a
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

    /// <remarks>
    /// The one rule a path can be held to without touching a disk, and the whole of
    /// what is decided here. Surrounding whitespace is taken off, because a path
    /// pasted out of a file manager or a terminal carries it and the failure it
    /// causes names a path that reads correctly; that is the same trim
    /// <see cref="Backend"/> and <see cref="TargetLanguage"/> already do, rather
    /// than a rule of this field's own.
    ///
    /// What is refused is a control character left inside the value after that trim.
    /// No path an operator typed into a page contains one; what does is a value that
    /// arrived with a line break in the middle of it, from a paste that wrapped or
    /// from the file being edited by hand. It fails inside a process launch or a
    /// file open, and the message it fails with prints the path on two lines or
    /// stops at the break, so the operator is shown something that looks like what
    /// they meant.
    ///
    /// Nothing else is decided. Whether a file is at either path, whether it
    /// executes, and whether it is a model are questions about a disk, they are the
    /// readiness probe in #15, and answering any of them here would put the same
    /// question in two places and let the two disagree. A path this accepts is a
    /// path somebody named, which is all
    /// <see cref="Backends.Local.LocalBackendOptions.IsComplete"/> claims about the
    /// same value.
    /// </remarks>
    /// <param name="field">The setting being read, for the complaint.</param>
    /// <param name="declared">The path the file carried.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>The path the local backend is given, or none named.</returns>
    private static string BackendPath(string field, string? declared, List<SettingComplaint> complaints)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return NoPathNamed;
        }

        var trimmed = declared.Trim();

        if (!trimmed.Any(char.IsControl))
        {
            return trimmed;
        }

        complaints.Add(new SettingComplaint(
            field,
            "the path holds a line break or another control character, which no path typed on the page does.",
            "No path is named, so the local backend reports that it is not configured."));

        return NoPathNamed;
    }

    /// <remarks>
    /// The same trim and the same refusal of a control character as
    /// <see cref="BackendPath"/>, for the same reasons, and one rule more, which is
    /// not this file's own: whether the value is a URL the remote backend could post
    /// to is decided by <see cref="RemoteBackendOptions.TryParseEndpoint"/>, which
    /// is the rule the backend applies before it posts, asked here on the value
    /// alone and without building a backend's settings to ask it, so the one place
    /// this plugin builds those stays the composition root. A URL that is relative or that is not http or https
    /// is refused with that type's own sentence, so the value an operator may save
    /// and the value the backend would send audio to cannot come apart, and the
    /// fallback is no endpoint at all rather than a repaired one, because guessing
    /// at a host is guessing at where the audio goes.
    ///
    /// Nothing here reaches the network. Whether the host resolves or answers is the
    /// readiness probe's question and is #15.
    /// </remarks>
    /// <param name="declared">The URL the file carried.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>The base URL the remote backend is given, or none named.</returns>
    private static string RemoteBaseUrl(string? declared, List<SettingComplaint> complaints)
    {
        var typed = RemoteSetting(nameof(PluginConfiguration.RemoteBaseUrl), declared, complaints);

        if (typed.Length == 0)
        {
            return NoRemoteSettingNamed;
        }

        if (RemoteBackendOptions.TryParseEndpoint(typed, out _, out var problem))
        {
            return typed;
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.RemoteBaseUrl),
            problem ?? "the URL is not one the remote backend can post to.",
            "No endpoint is named, so the remote backend reports that it is not configured."));

        return NoRemoteSettingNamed;
    }

    /// <remarks>
    /// The trim and the control-character refusal the paths get, applied to the
    /// three text settings of the remote backend. For the key the refusal is what
    /// stands between a pasted line break and a request the HTTP client refuses to
    /// send, with the key printed in the failure. Nothing else is decided: whether a
    /// key is accepted or a model is served is the endpoint's answer, which is the
    /// probe's to ask.
    /// </remarks>
    /// <param name="field">The setting being read, for the complaint.</param>
    /// <param name="declared">The value the file carried.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>The value the remote backend is given, or none named.</returns>
    private static string RemoteSetting(string field, string? declared, List<SettingComplaint> complaints)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return NoRemoteSettingNamed;
        }

        var trimmed = declared.Trim();

        if (!trimmed.Any(char.IsControl))
        {
            return trimmed;
        }

        complaints.Add(new SettingComplaint(
            field,
            "the value holds a line break or another control character, which nothing typed on the page does.",
            "Nothing is named for it, so the remote backend reports what it is missing."));

        return NoRemoteSettingNamed;
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

    /// <remarks>
    /// The rule is <see cref="ConcurrencyCap.Choose"/>'s and is not restated here.
    /// That type refuses a number rather than reducing it, and this is where the
    /// refusal meets a file: an operator who typed something the machine cannot
    /// carry is told the number, the ceiling and the reason, and the run uses the
    /// default instead of the number they typed. Falling back rather than refusing
    /// to load is what every field here does, and the reason is the same one:
    /// a plugin that will not start leaves nobody a page to repair it from.
    /// </remarks>
    /// <param name="declared">The number the file carried.</param>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>The number of items a run transcribes at once.</returns>
    private static int ItemsAtOnce(int declared, int processorCount, List<SettingComplaint> complaints)
    {
        if (declared == LetTheMachineDecide)
        {
            return ConcurrencyCap.Default;
        }

        var choice = ConcurrencyCap.Choose(declared, processorCount);
        var refusal = choice.Refusal;

        if (refusal is null)
        {
            return choice.Workers;
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.ItemsAtOnce),
            refusal,
            string.Format(
                CultureInfo.InvariantCulture,
                "The run transcribes {0} item at a time.",
                ConcurrencyCap.Default)));

        return ConcurrencyCap.Default;
    }

    /// <remarks>
    /// <see cref="ThreadCount.Choose"/>'s rule, met the same way, with one
    /// difference worth seeing: what nobody choosing falls back to is a reading of
    /// the machine rather than a constant, so the sentence an operator gets names
    /// the number this server arrived at rather than a number written in the source.
    /// </remarks>
    /// <param name="declared">The number the file carried.</param>
    /// <param name="processorCount">Processors the server can see.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>The number of threads one transcription may use.</returns>
    private static int ThreadsPerItem(int declared, int processorCount, List<SettingComplaint> complaints)
    {
        var whenNobodyChose = ThreadCount.DefaultFor(processorCount);

        if (declared == LetTheMachineDecide)
        {
            return whenNobodyChose;
        }

        var choice = ThreadCount.Choose(declared, processorCount);
        var refusal = choice.Refusal;

        if (refusal is null)
        {
            return choice.Threads;
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.ThreadsPerItem),
            refusal,
            string.Format(
                CultureInfo.InvariantCulture,
                "One transcription runs on {0} threads, which is what this machine decides.",
                whenNobodyChose)));

        return whenNobodyChose;
    }

    /// <remarks>
    /// The same shape as the two resource limits above, with the machine left out of
    /// it: nobody choosing falls back to a constant rather than to a reading of the
    /// processors, so the sentence an operator gets names a number that is the same
    /// on every server.
    ///
    /// The rule is asked of <see cref="RetryPolicy"/> rather than restated here, so
    /// the values a file may carry and the values <see cref="RetryPolicy.Record"/>
    /// will act on are one rule. Refusing rather than raising is the same trade the
    /// other limits make: an operator who typed zero and got three would go on
    /// believing they had switched quarantine off.
    /// </remarks>
    /// <param name="declared">The number the file carried.</param>
    /// <param name="complaints">Where a refused value is recorded.</param>
    /// <returns>How many counted failures an item gets before it is quarantined.</returns>
    private static int FailuresBeforeQuarantine(int declared, List<SettingComplaint> complaints)
    {
        if (declared == LetThePolicyDecide)
        {
            return RetryPolicy.DefaultFailureLimit;
        }

        var refusal = RetryPolicy.RefuseAsAFailureLimit(declared);

        if (refusal is null)
        {
            return declared;
        }

        complaints.Add(new SettingComplaint(
            nameof(PluginConfiguration.FailuresBeforeQuarantine),
            refusal,
            string.Format(
                CultureInfo.InvariantCulture,
                "An item is quarantined after {0} counted failures.",
                RetryPolicy.DefaultFailureLimit)));

        return RetryPolicy.DefaultFailureLimit;
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
