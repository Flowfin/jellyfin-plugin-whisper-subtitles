using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What the readiness probe reports about the tool itself, and what it asked to
/// find out: the two flags #324 decided, one line read out of each, labelled as
/// what it was and never as a version the probe did not see.
/// </summary>
/// <remarks>
/// The runner double here answers per flag, because the probe asks twice and the
/// suite's scripted runner hands one process to every start. What is read off it
/// is every invocation the probe made, so the legs can hold that the tool was
/// started with one flag and nothing else: no model, no audio, no third flag.
///
/// The hostile case <c>docs/untrusted-input.md</c> names this class for is the
/// tool printing what a version line is not: a paragraph, control characters,
/// nothing, or output that never ends.
/// </remarks>
public sealed class LocalVersionProbeTests
{
    private const string Tool = "/opt/whisper/whisper-cli";

    private const string Model = "/var/lib/models/ggml-base.bin";

    [Fact]
    public async Task A_tool_that_answers_the_version_flag_cleanly_has_reported_its_version()
    {
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.Printing(["whisper.cpp 1.7.4", "built with cuda"]));

        var readiness = await Probe(runner).ConfigureAwait(true);

        Assert.True(readiness.IsReady);
        Assert.NotNull(readiness.Tool);
        Assert.Equal(ToolIdentityKind.Version, readiness.Tool.Kind);
        Assert.Equal("whisper.cpp 1.7.4", readiness.Tool.Text);
        Assert.Contains("--version", readiness.Tool.Describe(), StringComparison.Ordinal);
        Assert.Contains("whisper.cpp 1.7.4", readiness.Tool.Describe(), StringComparison.Ordinal);

        // One start, one flag, and none of what a transcription carries.
        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal(Tool, invocation.ExecutablePath);
        Assert.Equal([ToolIdentityProbe.VersionFlag], invocation.Arguments);
    }

    [Fact]
    public async Task A_tool_that_refuses_the_version_flag_describes_itself_from_its_help_and_is_not_called_a_version()
    {
        // whisper.cpp builds that know no --version print usage and exit 1. The
        // first line of --help is then the tool's own description, labelled as one.
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.Printing(["error: unknown argument: --version"], exitCode: 1))
            .To(ToolIdentityProbe.HelpFlag, ScriptedProcess.Printing([string.Empty, "usage: whisper-cli [options] file0 file1 ...", "options:"]));

        var readiness = await Probe(runner).ConfigureAwait(true);

        Assert.True(readiness.IsReady);
        Assert.Equal(ToolIdentityKind.Description, readiness.Tool!.Kind);
        Assert.Equal("usage: whisper-cli [options] file0 file1 ...", readiness.Tool.Text);
        Assert.Contains("describes itself", readiness.Tool.Describe(), StringComparison.Ordinal);
        Assert.DoesNotContain("reports its version", readiness.Tool.Describe(), StringComparison.Ordinal);
        Assert.Equal(
            [ToolIdentityProbe.VersionFlag, ToolIdentityProbe.HelpFlag],
            runner.Invocations.Select(invocation => invocation.Arguments.Single()));
    }

    [Fact]
    public async Task A_clean_exit_that_printed_nothing_is_not_a_version_either()
    {
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.Printing([string.Empty, "   "]))
            .To(ToolIdentityProbe.HelpFlag, ScriptedProcess.Printing(["whisper-cli: transcribe audio with whisper.cpp"]));

        var readiness = await Probe(runner).ConfigureAwait(true);

        Assert.Equal(ToolIdentityKind.Description, readiness.Tool!.Kind);
        Assert.Equal("whisper-cli: transcribe audio with whisper.cpp", readiness.Tool.Text);
    }

    [Fact]
    public async Task A_tool_that_prints_nothing_to_either_flag_is_there_and_silent()
    {
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.Printing([]))
            .To(ToolIdentityProbe.HelpFlag, ScriptedProcess.Printing([], exitCode: 2));

        var readiness = await Probe(runner).ConfigureAwait(true);

        Assert.True(readiness.IsReady);
        Assert.Equal(ToolIdentityKind.Silent, readiness.Tool!.Kind);
        Assert.Null(readiness.Tool.Text);
        Assert.Contains("silent", readiness.Tool.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_that_never_stops_is_stopped_at_the_deadline_and_read_as_silent()
    {
        // The case the deadline exists for: a tool that treats an unknown flag as a
        // reason to start doing something else, on a page somebody is waiting in
        // front of.
        var version = ScriptedProcess.StillRunningAfter([], 0);
        var help = ScriptedProcess.StillRunningAfter([], 0);
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, version)
            .To(ToolIdentityProbe.HelpFlag, help);

        var readiness = await Probe(runner, toolAnswerTimeout: TimeSpan.FromMilliseconds(20)).ConfigureAwait(true);

        Assert.True(readiness.IsReady);
        Assert.Equal(ToolIdentityKind.Silent, readiness.Tool!.Kind);
        Assert.True(version.KillRequested, "the tool that never answered --version was left running");
        Assert.True(help.KillRequested, "the tool that never answered --help was left running");
    }

    [Fact]
    public async Task A_line_longer_than_a_screen_is_cut_and_control_characters_do_not_reach_the_page()
    {
        var hostile = "\u001b" + new string('v', 500) + "\r\u0007";
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.Printing([hostile]));

        var readiness = await Probe(runner).ConfigureAwait(true);

        Assert.Equal(ToolIdentityKind.Version, readiness.Tool!.Kind);
        Assert.Equal(ToolIdentityProbe.LongestLine, readiness.Tool.Text!.Length);
        Assert.DoesNotContain(readiness.Tool.Text, character => char.IsControl(character));
        Assert.Equal(new string('v', ToolIdentityProbe.LongestLine), readiness.Tool.Text);
    }

    [Fact]
    public async Task A_tool_that_cannot_be_started_is_not_ready_and_the_reason_names_it()
    {
        var runner = ScriptedProcessRunner.Refusing(new InvalidOperationException("exec format error"));
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);

        var readiness = await new LocalWhisperBackend(runner, files, Options())
            .CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.False(readiness.IsReady);
        Assert.Contains(Tool, readiness.Reason, StringComparison.Ordinal);
        Assert.Contains("exec format error", readiness.Reason, StringComparison.Ordinal);
        Assert.Null(readiness.Tool);
    }

    [Fact]
    public async Task Nothing_is_started_while_a_path_is_missing_or_holds_nothing()
    {
        // The tool is asked what it is only once both paths hold files. A probe that
        // ran a path nothing is at, or ran the tool with no model to hand it, would be
        // answering a question the operator has not got to yet.
        var runner = new FlagAnsweringRunner();

        var noModel = await new LocalWhisperBackend(runner, StubFileFacts.Empty().WithTool(Tool), Options())
            .CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);
        var noPaths = await new LocalWhisperBackend(runner, StubFileFacts.Empty(), new LocalBackendOptions(null, null))
            .CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.False(noModel.IsReady);
        Assert.False(noPaths.IsReady);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task An_operator_who_leaves_the_page_stops_the_question_rather_than_being_answered()
    {
        using var leaves = new CancellationTokenSource();
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.StillRunningAfter([], 0));
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        var backend = new LocalWhisperBackend(runner, files, Options());

        var asking = backend.CheckReadinessAsync(leaves.Token);
        await leaves.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asking).ConfigureAwait(true);
    }

    [Fact]
    public async Task What_the_tool_said_travels_into_the_answer_the_page_is_given()
    {
        var runner = new FlagAnsweringRunner()
            .To(ToolIdentityProbe.VersionFlag, ScriptedProcess.Printing(["whisper.cpp 1.7.4"]));
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var report = await ReadinessQuestion.AskAsync(
            new PluginConfiguration { Backend = LocalWhisperBackend.BackendName, LocalToolPath = Tool, LocalModelPath = Model },
            runner,
            files,
            endpoint,
            CancellationToken.None).ConfigureAwait(true);

        Assert.True(report.IsReady);
        Assert.NotNull(report.Tool);
        Assert.Contains("whisper.cpp 1.7.4", report.Tool, StringComparison.Ordinal);
        Assert.Contains("--version", report.Tool, StringComparison.Ordinal);
    }

    [Fact]
    public void A_deadline_of_nothing_for_the_tool_is_refused_where_it_is_configured()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LocalBackendOptions(Tool, Model, LocalBackendOptions.DefaultProbeTimeout, 2, TimeSpan.Zero));
    }

    private static async Task<BackendReadiness> Probe(IProcessRunner runner, TimeSpan? toolAnswerTimeout = null) =>
        await new LocalWhisperBackend(
            runner,
            StubFileFacts.Empty().WithTool(Tool).WithModel(Model),
            Options(toolAnswerTimeout)).CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);

    private static LocalBackendOptions Options(TimeSpan? toolAnswerTimeout = null) =>
        new(Tool, Model, LocalBackendOptions.DefaultProbeTimeout, 2, toolAnswerTimeout ?? LocalBackendOptions.DefaultToolAnswerTimeout);

    /// <summary>
    /// A runner that answers each flag with the process the test scripted for it,
    /// and records every invocation. A flag nothing was scripted for is answered
    /// by a process that prints nothing and exits cleanly, which is the answer
    /// least likely to hide a probe asking a flag it should not.
    /// </summary>
    private sealed class FlagAnsweringRunner : IProcessRunner
    {
        private readonly Dictionary<string, ScriptedProcess> _byFlag = new(StringComparer.Ordinal);

        public List<ProcessInvocation> Invocations { get; } = [];

        public FlagAnsweringRunner To(string flag, ScriptedProcess process)
        {
            _byFlag[flag] = process;

            return this;
        }

        public IStartedProcess Start(ProcessInvocation invocation)
        {
            Invocations.Add(invocation);

            var flag = invocation.Arguments.Count == 1 ? invocation.Arguments[0] : string.Empty;

            return _byFlag.TryGetValue(flag, out var scripted) ? scripted : ScriptedProcess.Printing([]);
        }
    }
}
