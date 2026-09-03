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
    /// Gets or sets the version of this shape the file was written under.
    /// </summary>
    /// <remarks>
    /// Present from the first release, because a migration can only read a version
    /// the file already carries: added later it would be absent from exactly the
    /// files that need migrating, and every one of them would have to be guessed
    /// at from which properties happen to be there. Defaults to
    /// <see cref="ConfigurationValidation.CurrentSchemaVersion"/>, which is also
    /// what an older file with no such element deserialises to, since the
    /// initialiser runs and no element overwrites it. What #41 does with a version
    /// this release does not know is that issue's; what happens here is that the
    /// file is read under this release's rules and the operator is told.
    /// </remarks>
    public int SchemaVersion { get; set; } = ConfigurationValidation.CurrentSchemaVersion;

    /// <summary>
    /// Gets or sets the backend that runs: one of the names this plugin answers
    /// to, or empty for none.
    /// </summary>
    /// <remarks>
    /// Empty out of the box, and empty means nothing is transcribed. A default
    /// naming a real backend would be a fresh install that starts launching a tool
    /// or talking to an endpoint on settings nobody filled in, and the backends
    /// that do work need settings this cannot supply anyway.
    /// </remarks>
    public string Backend { get; set; } = ConfigurationValidation.NoBackendChosen;

    /// <summary>
    /// Gets or sets the path to the Whisper program the local backend runs, or
    /// empty when the operator has named none.
    /// </summary>
    /// <remarks>
    /// Empty out of the box, and empty means the local backend cannot run. There is
    /// no default worth writing: the program is one the operator installed, this
    /// plugin never downloads it, and a guess at a location would be a plugin
    /// launching whatever happens to sit at a path nobody typed.
    ///
    /// Trimmed and otherwise carried through untouched. Whether a file is there,
    /// whether it runs, and whether it speaks the command line this backend uses are
    /// the readiness probe's questions and are #15; the local backend's own options
    /// type says the same thing about the same value, and this property does not
    /// make a second rule about it.
    /// </remarks>
    public string LocalToolPath { get; set; } = ConfigurationValidation.NoPathNamed;

    /// <summary>
    /// Gets or sets the path to the model file the local backend hands that program,
    /// or empty when the operator has named none.
    /// </summary>
    /// <remarks>
    /// Empty for the reason <see cref="LocalToolPath"/> is empty, and here the
    /// reason is a fixed property of this plugin rather than a default somebody
    /// chose: the model is a file the operator supplies, nothing here downloads
    /// several gigabytes it was not asked for, and a path pointing at a file that
    /// was never fetched is the same state as no path at all.
    /// </remarks>
    public string LocalModelPath { get; set; } = ConfigurationValidation.NoPathNamed;

    /// <summary>
    /// Gets or sets the base URL of the endpoint the remote backend posts audio to,
    /// or empty when the operator has named none.
    /// </summary>
    /// <remarks>
    /// Empty out of the box, and empty means the remote backend cannot run. There
    /// is no default worth writing: a URL is where the audio of every selected item
    /// goes, and a guess at one would be a plugin sending audio to a host nobody
    /// typed. The page states beside this field what leaves the machine and where,
    /// naming the host out of whatever is typed here, so the disclosure and the
    /// value cannot describe two different endpoints.
    ///
    /// Trimmed, refused for a control character, and refused when it is not an
    /// absolute http or https URL, by the rule
    /// <see cref="Backends.Remote.RemoteBackendOptions.TryGetEndpoint"/> already
    /// applies to the same value, so what an operator may save and what the backend
    /// would post to cannot come apart. Whether the host answers is the readiness
    /// probe's question and is #15.
    /// </remarks>
    public string RemoteBaseUrl { get; set; } = ConfigurationValidation.NoRemoteSettingNamed;

    /// <summary>
    /// Gets or sets the key the remote backend sends, or empty when the endpoint
    /// needs none.
    /// </summary>
    /// <remarks>
    /// Empty out of the box, and empty is a working state rather than a missing
    /// one: an endpoint an operator runs themselves commonly wants no key, which is
    /// the reason <see cref="Backends.Remote.RemoteBackendOptions.ApiKey"/> is
    /// optional. It is a secret, stored where the server stores every plugin's
    /// settings, and this plugin puts it into one request header and nowhere else;
    /// the page never writes it into the disclosure beside it, and
    /// <c>RemoteBackendSettingsTests</c> refuses a page that does.
    ///
    /// Trimmed and refused for a control character, because a key holding a line
    /// break is a request header the client refuses to send.
    /// </remarks>
    public string RemoteApiKey { get; set; } = ConfigurationValidation.NoRemoteSettingNamed;

    /// <summary>
    /// Gets or sets the model name the remote backend asks the endpoint for, or
    /// empty when the operator has named none.
    /// </summary>
    /// <remarks>
    /// A name and not a choice from a list, for the reason
    /// <see cref="Backends.Remote.RemoteBackendOptions.Model"/> gives: which models
    /// an endpoint serves is the endpoint's business. Empty means the remote
    /// backend cannot run, which is what that type's own completeness rule says
    /// about the same value. Trimmed and refused for a control character.
    /// </remarks>
    public string RemoteModel { get; set; } = ConfigurationValidation.NoRemoteSettingNamed;

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
    /// Gets or sets how many items a run transcribes at once, or zero for the
    /// number this machine decides.
    /// </summary>
    /// <remarks>
    /// Zero out of the box, and zero is not a number of items. It is the absence of
    /// a choice, which is a different fact from any number an operator could type
    /// and is the only one a file can carry across machines: a configuration copied
    /// to a server with a different processor count still means "whatever this
    /// machine can afford" rather than a count somebody else's machine could
    /// afford. An absent element deserialises to it as well, so a file written
    /// before this setting existed reads as nobody having chosen rather than as a
    /// zero somebody typed.
    ///
    /// What zero resolves to is <see cref="Scheduling.ConcurrencyCap.Default"/>, and
    /// a value outside what <see cref="Scheduling.ConcurrencyCap.Choose"/> accepts is
    /// refused rather than quietly reduced, which is that type's own rule reaching
    /// the file an operator edits.
    /// </remarks>
    public int ItemsAtOnce { get; set; } = ConfigurationValidation.LetTheMachineDecide;

    /// <summary>
    /// Gets or sets how many threads one transcription may use, or zero for the
    /// number this machine decides.
    /// </summary>
    /// <remarks>
    /// Zero for the reason <see cref="ItemsAtOnce"/> is zero, and here the reason is
    /// sharper: what nobody choosing resolves to is
    /// <see cref="Scheduling.ThreadCount.DefaultFor"/> of the processors this server
    /// reports, so a number written into the file would be this machine's answer
    /// frozen at the moment somebody saved the page.
    ///
    /// The pair is not checked against each other. An operator who raises both to
    /// their ceilings has asked for the whole machine several times over, and what
    /// would refuse that is a rule neither limit carries, because each is a bound on
    /// its own subject. It is written at the ceilings rather than built.
    /// </remarks>
    public int ThreadsPerItem { get; set; } = ConfigurationValidation.LetTheMachineDecide;

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
    /// Gets or sets how many counted failures an item gets before it is
    /// quarantined, or zero for the number this plugin decides.
    /// </summary>
    /// <remarks>
    /// Zero out of the box, and zero is not a number of failures. It is the absence
    /// of a choice, which is what an absent element deserialises to as well, so a
    /// file written before this setting existed reads as nobody having chosen rather
    /// than as an item quarantined before it has been tried once.
    ///
    /// What zero resolves to is
    /// <see cref="Attempts.RetryPolicy.DefaultFailureLimit"/>. That is where the
    /// sentinel differs from the one the two resource limits carry, and the
    /// difference is worth seeing rather than reading this as the same field again:
    /// those two resolve against the processors this server reports, and this one
    /// resolves to a constant, because nothing about a machine says how many times a
    /// broken item is worth trying.
    ///
    /// A value below one is refused rather than quietly raised, and the rule doing
    /// the refusing is the retry policy's own, so the number a run acts on and the
    /// number an operator is allowed to type cannot come apart. There is no ceiling,
    /// and <see cref="Attempts.RetryPolicy.SmallestFailureLimit"/> is where that
    /// absence is argued.
    /// </remarks>
    public int FailuresBeforeQuarantine { get; set; } = ConfigurationValidation.LetThePolicyDecide;

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
    ///
    /// Which rows survive is decided in <see cref="ConfigurationValidation"/> and
    /// not here, so that the rows a run uses and the rows an operator is told
    /// about cannot come from two readings of the same array. What this adds is
    /// the shape selection wants; the complaints go with the load.
    /// </remarks>
    public IReadOnlyDictionary<Guid, string> TargetLanguagesByLibrary() =>
        ConfigurationValidation.Of(this).InForce.TargetLanguagesByLibrary;
}
