using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// The scheduled task an operator runs to generate subtitles.
/// </summary>
/// <remarks>
/// This is the task and not yet what it does. What it holds is the part a server
/// installs: a key, a name, a category, and the promise that nothing starts on its
/// own. An operator who installs this plugin and configures nothing has a server
/// that behaves exactly as it did before and a task they can look at, which is the
/// property this class exists for and the one its tests are about.
///
/// `IScheduledTask` and `IConfigurableScheduledTask` are the same types on both
/// supported server lines, so one implementation serves both. That is not taken on
/// trust here: this project is compiled against each line's own package, so a
/// member that existed on only one of them would fail the build for the other.
///
/// Dependencies come through the constructor rather than off the static plugin
/// instance. A type that reaches a static is a type that cannot be built in a test
/// without a whole server, and the seams every test injects are #71, which is what
/// will hand this task the real ones.
/// </remarks>
public sealed class SubtitleGenerationTask : IScheduledTask, IConfigurableScheduledTask
{
    /// <summary>
    /// The key the server stores this task's schedule and history under.
    /// </summary>
    /// <remarks>
    /// Stable and namespaced by this plugin, because the server keys tasks by this
    /// string: two plugins choosing one key are one task as far as triggers and
    /// history are concerned. Changing it later loses whatever schedule an operator
    /// had set, so it is asserted by a test rather than left to a rename. The scan
    /// that checks it against the other supported plugins is #64.
    /// </remarks>
    public const string TaskKey = "WhisperSubtitlesGenerate";

    private readonly IReadOnlyList<BackendCandidate> _candidates;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleGenerationTask"/> class.
    /// </summary>
    /// <param name="candidates">Every backend this plugin has, with the settings each is missing.</param>
    public SubtitleGenerationTask(IReadOnlyList<BackendCandidate> candidates)
    {
        _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
    }

    /// <inheritdoc />
    public string Name => "Generate subtitles";

    /// <inheritdoc />
    public string Key => TaskKey;

    /// <inheritdoc />
    public string Description =>
        "Transcribes the audio of library items that have no subtitle and writes the result as a subtitle file. "
        + "The transcription runs outside the server, in a tool or an endpoint you configure, and nothing runs until you start it or give it a schedule.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    /// <remarks>
    /// Visible. A task that does work an operator pays for in machine time is a task
    /// they are entitled to find in the list without knowing it exists.
    /// </remarks>
    public bool IsHidden => false;

    /// <inheritdoc />
    /// <remarks>
    /// Enabled means it may be run, not that it will run. What decides whether
    /// anything happens on its own is <see cref="GetDefaultTriggers"/>, which
    /// returns none.
    /// </remarks>
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <summary>
    /// Gets what the last run of this task had to say for itself.
    /// </summary>
    /// <remarks>
    /// A property because there is nowhere else yet. This plugin has no logger, and
    /// the levels each kind of event uses are decided in #73; the surface that shows
    /// an operator what a run did is #39. Until either exists, a run that finished
    /// because nothing is configured has to be able to say so to something, and a
    /// test is the something.
    /// </remarks>
    public string? LastReport { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// No trigger, and this is the whole point of the method rather than an omission.
    /// A plugin that installs a nightly trigger has started transcribing a library on
    /// somebody's server without being asked, on hardware chosen for a media server
    /// rather than for inference. An operator who wants a schedule sets one.
    /// </remarks>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    /// <remarks>
    /// Selection, extraction, transcription and writing are not here. Each is its own
    /// issue with its own tests, and they reach this method once the seams that carry
    /// them exist. What is here is the case an operator meets first: a task that runs
    /// to completion, changes nothing, and says why.
    /// </remarks>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        cancellationToken.ThrowIfCancellationRequested();

        // Null because no setting holds a backend name yet. #40 is the configuration
        // schema and it is where that arrives; until then selection takes the same
        // route it will take when the setting is empty, which is the case a fresh
        // install is in anyway.
        var choice = await BackendSelector
            .SelectAsync(null, _candidates, cancellationToken)
            .ConfigureAwait(false);

        LastReport = choice.Reason;

        // A run that did nothing is finished rather than stalled. A task left at
        // nought looks to an operator like one that hung.
        progress.Report(100);
    }
}
