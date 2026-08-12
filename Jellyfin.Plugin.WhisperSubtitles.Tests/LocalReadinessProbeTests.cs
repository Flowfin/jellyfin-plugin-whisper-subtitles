using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What the local backend answers when it is asked whether it can be used, and
/// what it looked at to answer.
/// </summary>
/// <remarks>
/// The failure this suite is about is not a wrong answer, it is a right answer
/// arriving three hours late. An operator who has typed a path with a typo in it
/// learns that from a run that failed on its first item, at whatever hour they
/// scheduled it for, and the only thing standing between them and that is a probe
/// that looks before the run.
///
/// Every case here is arranged through the file seam rather than on a disk. That
/// is not convenience: a file that exists and may not be executed cannot be made
/// on both of the platforms this suite runs on, and a model of a plausible size is
/// a megabyte written and deleted on every run.
/// </remarks>
public sealed class LocalReadinessProbeTests
{
    private const string Tool = "/opt/whisper/whisper-cli";

    private const string Model = "/var/lib/models/ggml-base.bin";

    [Fact]
    public async Task A_backend_with_no_paths_names_the_settings_and_looks_at_nothing()
    {
        // Nothing is asked of the file system, because there is nothing to ask
        // about. A probe that reported "there is no file at " with an empty path
        // would be answering a question the operator has not got to yet.
        var files = StubFileFacts.Empty();

        var readiness = await Probe(files, new LocalBackendOptions(null, null));

        Assert.False(readiness.IsReady);
        Assert.Contains("configuration page", readiness.Reason, StringComparison.Ordinal);
        Assert.Empty(files.Asked);
    }

    [Fact]
    public async Task A_tool_path_with_nothing_at_it_is_named_in_the_reason()
    {
        var readiness = await Probe(StubFileFacts.Empty().WithModel(Model), Configured());

        Assert.False(readiness.IsReady);
        Assert.Contains("no file at the transcription tool path", readiness.Reason, StringComparison.Ordinal);
        Assert.Contains(Tool, readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_the_server_may_not_execute_is_refused_before_a_run_finds_out()
    {
        // The mistake this catches is a tool copied out of an archive that carried
        // no permission bits. It is there, it is the right size, and it will not
        // start.
        var files = StubFileFacts.Empty().WithTool(Tool, isExecutable: false).WithModel(Model);

        var readiness = await Probe(files, Configured());

        Assert.False(readiness.IsReady);
        Assert.Contains("may not be executed", readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_platform_that_carries_no_permission_bit_is_not_read_as_a_refusal()
    {
        // The near-miss this suite exists against, and it is one character: treating
        // the unanswered question as false refuses every tool on Windows, where
        // there is no bit to read. Nothing about the answer below is Windows
        // specific, which is the point of asserting it through the seam.
        var files = StubFileFacts.Empty().WithTool(Tool, isExecutable: null).WithModel(Model);

        var readiness = await Probe(files, Configured());

        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Reason);
    }

    [Fact]
    public async Task A_model_path_with_nothing_at_it_is_named_in_the_reason()
    {
        var readiness = await Probe(StubFileFacts.Empty().WithTool(Tool), Configured());

        Assert.False(readiness.IsReady);
        Assert.Contains("no file at the model path", readiness.Reason, StringComparison.Ordinal);
        Assert.Contains(Model, readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_far_too_small_to_be_a_model_is_refused_and_its_size_is_quoted()
    {
        // A refused download saved anyway, a page of HTML from a proxy, a pointer
        // file standing in for a large object. Each is a file at the model path, and
        // each reaches the operator as a tool that starts and fails on every item.
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model, sizeInBytes: 402);

        var readiness = await Probe(files, Configured());

        Assert.False(readiness.IsReady);
        Assert.Contains("too small to be a whisper model", readiness.Reason, StringComparison.Ordinal);
        Assert.Contains("402", readiness.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LocalBackendOptions.SmallestPlausibleModelBytes - 1, false)]
    [InlineData(LocalBackendOptions.SmallestPlausibleModelBytes, true)]
    public async Task The_floor_refuses_the_byte_below_it_and_accepts_the_byte_on_it(long size, bool expected)
    {
        // One byte either side, because a floor is only worth anything at its edge
        // and the mistake somebody will make is the comparison, not the number.
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model, sizeInBytes: size);

        var readiness = await Probe(files, Configured());

        Assert.Equal(expected, readiness.IsReady);
    }

    [Fact]
    public async Task A_tool_and_a_model_that_are_both_there_are_ready()
    {
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing([]));

        var readiness = await new LocalWhisperBackend(runner, files, Configured())
            .CheckReadinessAsync(CancellationToken.None);

        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Reason);

        // The clause the contract suite states in general, asserted here against the
        // one backend that could break it: nothing was started. A probe that ran the
        // tool to find out whether it runs would be a transcription an operator did
        // not ask for, on a page they were only looking at.
        Assert.Null(runner.Invocation);
    }

    [Fact]
    public async Task The_tool_is_looked_at_first_and_a_missing_one_ends_the_probe()
    {
        // The order an operator fixes them in, and it is asserted rather than left
        // to the reading order of the method: with neither path holding anything,
        // only the tool is asked about.
        var files = StubFileFacts.Empty();

        var readiness = await Probe(files, Configured());

        Assert.False(readiness.IsReady);
        Assert.Equal(new[] { Tool }, files.Asked);
    }

    [Fact]
    public async Task A_file_system_that_does_not_answer_ends_at_the_probe_deadline()
    {
        // The wait is the probe's own deadline rather than a sleep in this test, and
        // the setting bounds it: twenty milliseconds is how long this test can take,
        // whatever the machine. The case it stands for is a path on a mount whose
        // server has gone, which does not fail, it waits.
        var options = new LocalBackendOptions(Tool, Model, TimeSpan.FromMilliseconds(20));

        var readiness = await Probe(StubFileFacts.NeverAnswering(), options);

        Assert.False(readiness.IsReady);
        Assert.Contains("did not answer", readiness.Reason, StringComparison.Ordinal);
        Assert.Contains(
            options.ProbeTimeout.ToString("g", CultureInfo.InvariantCulture),
            readiness.Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_operator_who_stops_the_probe_is_not_told_the_file_system_is_slow()
    {
        // The two arrive at one catch and mean opposite things. Somebody who
        // navigated away from the page has discovered nothing about their disk, and
        // reporting a deadline they never reached would send them looking for one.
        //
        // The stop lands while the probe is inside the seam rather than before it is
        // called. Cancelling first is refused at the probe's first line, which is a
        // green test that never reaches the catch this is about.
        using var stopping = new CancellationTokenSource();

        var backend = new LocalWhisperBackend(
            ScriptedProcessRunner.Starting(ScriptedProcess.Printing([])),
            StubFileFacts.StoppingTheCaller(stopping),
            Configured());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backend.CheckReadinessAsync(stopping.Token));
    }

    [Fact]
    public void A_probe_deadline_of_nothing_is_refused_where_it_is_configured()
    {
        // A zero deadline is a probe that answers "the file system did not answer"
        // about every path there is, which is a setting that looks like a bound and
        // behaves like a fault.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LocalBackendOptions(Tool, Model, TimeSpan.Zero));
    }

    private static LocalBackendOptions Configured() => new(Tool, Model);

    private static async Task<BackendReadiness> Probe(StubFileFacts files, LocalBackendOptions options) =>
        await new LocalWhisperBackend(
            ScriptedProcessRunner.Starting(ScriptedProcess.Printing([])),
            files,
            options).CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);
}
