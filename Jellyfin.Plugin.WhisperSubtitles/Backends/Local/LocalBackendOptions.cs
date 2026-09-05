using System;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// The two paths the local backend needs, and how much of the machine it may use
/// while it runs.
/// </summary>
/// <remarks>
/// A type of its own rather than the plugin configuration, so the backend can be
/// built and driven in a test without a server writing a file. Where these values
/// come from, how they are validated and what happens to an invalid one is #40,
/// and the page an operator types them into is #36.
///
/// Neither path is ever downloaded. That is a fixed property of this plugin
/// rather than a default: the operator supplies the tool and the model, and a
/// plugin that fetched several gigabytes it was not asked for would be making a
/// trust decision on somebody else's server.
/// </remarks>
public sealed class LocalBackendOptions
{
    /// <summary>
    /// The smallest file this plugin will believe is a model.
    /// </summary>
    /// <remarks>
    /// One mebibyte, and it is a floor against a file that is plainly not a model
    /// rather than a measurement of any model. The smallest whisper.cpp publishes
    /// is tens of megabytes even quantised, and what this catches sits three orders
    /// of magnitude below that: a download that was refused and saved anyway, a
    /// page of HTML from a proxy, a pointer file from a repository that stores
    /// large objects elsewhere, an empty file made by a shell redirect. Each of
    /// those otherwise reaches the operator as a tool that starts and fails on the
    /// first item.
    ///
    /// Deliberately not a ceiling and deliberately not per model name. A number
    /// that tracked what each published model weighs would refuse a model somebody
    /// quantised themselves, and being wrong in that direction costs more than
    /// letting a small but real model through.
    /// </remarks>
    public const long SmallestPlausibleModelBytes = 1024L * 1024;

    /// <summary>
    /// How long the readiness probe may spend looking before it gives up.
    /// </summary>
    /// <remarks>
    /// Five seconds, and it covers looking at both paths rather than each one. It
    /// is short because everything under it is a metadata read on a local disk,
    /// which takes microseconds, and because what sits on the other end of this is
    /// a configuration page somebody is waiting in front of. The case it exists for
    /// is the path that is not on a local disk: a mount whose server has gone
    /// answers no faster than that server comes back.
    /// </remarks>
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the readiness probe waits for the tool to answer one of the two
    /// flags it is asked before the tool is stopped and read as silent for it.
    /// </summary>
    /// <remarks>
    /// Three seconds per flag, and it is the first time this plugin runs the
    /// operator's tool on a page load rather than in a run. Printing a version is
    /// instant for any tool that prints one; what this bounds is a tool that
    /// treats an unknown flag as a reason to start doing something else, on a page
    /// somebody is waiting in front of. #324 decided the two flags; the bound is
    /// this type's because every other deadline the probe carries is.
    /// </remarks>
    public static readonly TimeSpan DefaultToolAnswerTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalBackendOptions"/> class.
    /// </summary>
    /// <param name="executablePath">The whisper.cpp compatible command line tool.</param>
    /// <param name="modelPath">The model file to hand it.</param>
    public LocalBackendOptions(string? executablePath, string? modelPath)
        : this(executablePath, modelPath, DefaultProbeTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalBackendOptions"/> class
    /// with a probe deadline of its own.
    /// </summary>
    /// <param name="executablePath">The whisper.cpp compatible command line tool.</param>
    /// <param name="modelPath">The model file to hand it.</param>
    /// <param name="probeTimeout">How long the readiness probe may spend looking.</param>
    public LocalBackendOptions(string? executablePath, string? modelPath, TimeSpan probeTimeout)
        : this(executablePath, modelPath, probeTimeout, Scheduling.ThreadCount.DefaultFor(Environment.ProcessorCount))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalBackendOptions"/> class
    /// with a thread count somebody chose.
    /// </summary>
    /// <param name="executablePath">The whisper.cpp compatible command line tool.</param>
    /// <param name="modelPath">The model file to hand it.</param>
    /// <param name="probeTimeout">How long the readiness probe may spend looking.</param>
    /// <param name="threadCount">How many threads the tool may use on one item.</param>
    /// <remarks>
    /// The thread count arrives already decided. Whether the number an operator
    /// typed is one this machine may be asked for is
    /// <see cref="Scheduling.ThreadCount.Choose"/>'s question, and answering it here would put
    /// the same rule in two places and let the two disagree.
    /// </remarks>
    public LocalBackendOptions(string? executablePath, string? modelPath, TimeSpan probeTimeout, int threadCount)
        : this(executablePath, modelPath, probeTimeout, threadCount, DefaultToolAnswerTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalBackendOptions"/> class
    /// with a deadline of its own for the tool answering the probe.
    /// </summary>
    /// <param name="executablePath">The whisper.cpp compatible command line tool.</param>
    /// <param name="modelPath">The model file to hand it.</param>
    /// <param name="probeTimeout">How long the readiness probe may spend looking at the two paths.</param>
    /// <param name="threadCount">How many threads the tool may use on one item.</param>
    /// <param name="toolAnswerTimeout">How long the tool may take to answer one flag before it is stopped.</param>
    public LocalBackendOptions(string? executablePath, string? modelPath, TimeSpan probeTimeout, int threadCount, TimeSpan toolAnswerTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(probeTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(toolAnswerTimeout, TimeSpan.Zero);

        ExecutablePath = executablePath;
        ModelPath = modelPath;
        ProbeTimeout = probeTimeout;
        ThreadCount = threadCount;
        ToolAnswerTimeout = toolAnswerTimeout;
    }

    /// <summary>
    /// Gets how long the tool may take to answer one flag of the readiness probe
    /// before it is stopped and read as silent for it.
    /// </summary>
    public TimeSpan ToolAnswerTimeout { get; }

    /// <summary>
    /// Gets how many threads the tool may use on one item.
    /// </summary>
    /// <remarks>
    /// Always a decided number rather than an absence meaning "let the tool
    /// choose". Saying nothing is not the neutral option: it selects the tool's
    /// own default, which is a budget somebody else chose without seeing this
    /// machine. What that default is and the command behind it are on
    /// docs/choosing-a-backend.md rather than asserted here. Where nobody has
    /// chosen, the number is
    /// <see cref="Scheduling.ThreadCount.DefaultFor"/> of the processors this server reports.
    ///
    /// Where an operator types one is #36, and the configuration property it
    /// would be read from is #22 along with the four limits beside it. What this
    /// property fixes is that a number reaching the tool is one somebody can name.
    /// </remarks>
    public int ThreadCount { get; }

    /// <summary>
    /// Gets how long the readiness probe may spend looking.
    /// </summary>
    public TimeSpan ProbeTimeout { get; }

    /// <summary>
    /// Gets the whisper.cpp compatible command line tool, or null when the operator has named none.
    /// </summary>
    public string? ExecutablePath { get; }

    /// <summary>
    /// Gets the model file to hand it, or null when the operator has named none.
    /// </summary>
    public string? ModelPath { get; }

    /// <summary>
    /// Gets a value indicating whether both paths have been named.
    /// </summary>
    /// <remarks>
    /// Named, not checked. Whether the file at either path exists, runs, or is a
    /// model at all is the readiness probe in #15, and this property says nothing
    /// about it.
    /// </remarks>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ExecutablePath) && !string.IsNullOrWhiteSpace(ModelPath);
}
