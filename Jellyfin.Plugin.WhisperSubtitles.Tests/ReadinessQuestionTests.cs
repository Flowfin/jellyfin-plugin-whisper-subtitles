using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What the configuration page is told when it asks whether the backend it has
/// chosen is ready, and what was looked at to answer.
/// </summary>
/// <remarks>
/// The question is asked about the settings as they stand on the page rather than
/// about what the file holds, so every case here posts a configuration and reads
/// which paths and which host were looked at off the seams. A page that showed an
/// answer about the last save would be telling an operator that the path they
/// have just corrected is still wrong.
///
/// The failure this exists against is the one the probes exist against: a right
/// answer arriving three hours late, from a run that failed on its first item.
/// What is added here is the reading of the posted shape, so the hostile case is
/// a value the load would refuse arriving by the route instead of by the file,
/// which is the entry <c>docs/untrusted-input.md</c> names this class for.
/// </remarks>
public sealed class ReadinessQuestionTests
{
    private const string Tool = "/opt/whisper/whisper-cli";

    private const string Model = "/var/lib/models/ggml-base.bin";

    private const string Endpoint = "https://transcription.example/v1";

    private const string RemoteModel = "whisper-1";

    private const string Key = "sk-a-key-nobody-may-see";

    [Fact]
    public async Task Nothing_chosen_is_answered_without_a_disk_or_a_host_being_asked()
    {
        var files = StubFileFacts.Empty();
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var report = await Ask(new PluginConfiguration(), files, endpoint).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Equal(NotConfiguredBackend.BackendName, report.Backend);
        Assert.Equal(NotConfiguredBackend.Explanation, report.Reason);
        Assert.Empty(files.Asked);
        Assert.Equal(0, endpoint.Requests);
    }

    [Fact]
    public async Task Nothing_readable_from_the_page_is_answered_as_nothing_chosen()
    {
        // A body the server could not read arrives as null. The load already answers
        // that with the defaults and a complaint, and here it is the do-nothing
        // backend rather than an exception on the route.
        var files = StubFileFacts.Empty();

        var report = await Ask(null, files, StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Equal(NotConfiguredBackend.BackendName, report.Backend);
        Assert.Empty(files.Asked);
    }

    [Fact]
    public async Task A_name_this_plugin_has_no_backend_for_is_answered_with_the_names_it_has()
    {
        var report = await Ask(
            new PluginConfiguration { Backend = "Cloud" },
            StubFileFacts.Empty(),
            StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Equal("Cloud", report.Backend);
        Assert.Contains("not one this plugin has", report.Reason, StringComparison.Ordinal);
        Assert.Contains(LocalWhisperBackend.BackendName, report.Reason, StringComparison.Ordinal);
        Assert.Contains(RemoteWhisperBackend.BackendName, report.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_local_backend_is_asked_about_the_paths_that_were_posted()
    {
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var report = await Ask(Local(Tool, Model), files, endpoint).ConfigureAwait(true);

        Assert.True(report.IsReady);
        Assert.Null(report.Reason);
        Assert.Equal(LocalWhisperBackend.BackendName, report.Backend);
        Assert.Equal(new[] { Tool, Model }, files.Asked);
        Assert.Equal(0, endpoint.Requests);
    }

    [Fact]
    public async Task A_model_file_that_is_not_there_is_named_in_the_answer()
    {
        var files = StubFileFacts.Empty().WithTool(Tool);

        var report = await Ask(Local(Tool, Model), files, StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Contains("no file at the model path", report.Reason, StringComparison.Ordinal);
        Assert.Contains(Model, report.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_local_setting_left_out_is_named_before_any_disk_is_asked()
    {
        var files = StubFileFacts.Empty().WithTool(Tool);

        var report = await Ask(Local(Tool, null), files, StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Contains(nameof(LocalBackendOptions.ModelPath), report.Reason, StringComparison.Ordinal);
        Assert.Empty(files.Asked);
    }

    [Fact]
    public async Task A_path_carrying_a_line_break_is_refused_before_any_disk_is_asked()
    {
        // The hostile case docs/untrusted-input.md names this class for: a value the
        // load would refuse, arriving by the route instead of by the file. The load
        // drops it and names the field, the probe then has no path to look at, and
        // nothing is asked about a path with a line break in the middle of it.
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);

        var report = await Ask(Local(Tool + "\n" + Model, Model), files, StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Contains(nameof(LocalBackendOptions.ExecutablePath), report.Reason, StringComparison.Ordinal);
        Assert.Empty(files.Asked);
    }

    [Fact]
    public async Task The_remote_backend_sends_one_request_with_no_audio_and_the_key_in_a_header()
    {
        var files = StubFileFacts.Empty();
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var report = await Ask(Remote(Endpoint, Key, RemoteModel), files, endpoint).ConfigureAwait(true);

        Assert.True(report.IsReady);
        Assert.Null(report.Reason);
        Assert.Equal(RemoteWhisperBackend.BackendName, report.Backend);
        Assert.Equal(1, endpoint.Requests);
        Assert.Empty(endpoint.Body);
        Assert.Equal("Bearer " + Key, endpoint.Authorization);
        Assert.Empty(files.Asked);
    }

    [Fact]
    public async Task A_key_the_endpoint_refuses_is_reported_without_the_key()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.Unauthorized, "{}");

        var report = await Ask(Remote(Endpoint, Key, RemoteModel), StubFileFacts.Empty(), endpoint).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Contains("refused", report.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, report.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_remote_setting_left_out_is_named_before_anything_is_sent()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var report = await Ask(Remote(Endpoint, Key, null), StubFileFacts.Empty(), endpoint).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Contains(nameof(RemoteBackendOptions.Model), report.Reason, StringComparison.Ordinal);
        Assert.Equal(0, endpoint.Requests);
    }

    [Fact]
    public async Task A_configuration_a_newer_release_wrote_is_answered_as_nothing_chosen()
    {
        // The load stands back from a file a newer release wrote and reads none of
        // its fields, and the question inherits that: a shape posted by a newer page
        // is not a reason to look at a path under this release's rules.
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        var posted = Local(Tool, Model);
        posted.SchemaVersion = ConfigurationValidation.CurrentSchemaVersion + 1;

        var report = await Ask(posted, files, StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.False(report.IsReady);
        Assert.Equal(NotConfiguredBackend.BackendName, report.Backend);
        Assert.Empty(files.Asked);
    }

    [Fact]
    public async Task The_name_in_the_answer_is_the_plugin_s_own_spelling_whatever_case_was_typed()
    {
        var files = StubFileFacts.Empty().WithTool(Tool).WithModel(Model);
        var posted = Local(Tool, Model);
        posted.Backend = "  local ";

        var report = await Ask(posted, files, StubEndpoint.Answering(HttpStatusCode.OK, "{}")).ConfigureAwait(true);

        Assert.True(report.IsReady);
        Assert.Equal(LocalWhisperBackend.BackendName, report.Backend);
    }

    [Fact]
    public async Task An_operator_who_leaves_the_page_stops_the_question_rather_than_being_answered()
    {
        // The caller's token wins the tie, as it does inside the probe: somebody who
        // navigated away has learned nothing about their disk, and an answer built
        // for them would be one nobody reads.
        using var leaves = new CancellationTokenSource();
        var files = StubFileFacts.StoppingTheCaller(leaves);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Ask(Local(Tool, Model), files, StubEndpoint.Answering(HttpStatusCode.OK, "{}"), leaves.Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task The_seams_are_required()
    {
        var files = StubFileFacts.Empty();
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");
        var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing([]));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ReadinessQuestion.AskAsync(new PluginConfiguration(), null!, files, endpoint, CancellationToken.None)).ConfigureAwait(true);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ReadinessQuestion.AskAsync(new PluginConfiguration(), runner, null!, endpoint, CancellationToken.None)).ConfigureAwait(true);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ReadinessQuestion.AskAsync(new PluginConfiguration(), runner, files, null!, CancellationToken.None)).ConfigureAwait(true);
    }

    private static PluginConfiguration Local(string? tool, string? model) =>
        new()
        {
            Backend = LocalWhisperBackend.BackendName,
            LocalToolPath = tool ?? string.Empty,
            LocalModelPath = model ?? string.Empty,
        };

    private static PluginConfiguration Remote(string? url, string? key, string? model) =>
        new()
        {
            Backend = RemoteWhisperBackend.BackendName,
            RemoteBaseUrl = url ?? string.Empty,
            RemoteApiKey = key ?? string.Empty,
            RemoteModel = model ?? string.Empty,
        };

    private static Task<ReadinessReport> Ask(
        PluginConfiguration? posted,
        StubFileFacts files,
        StubEndpoint endpoint,
        CancellationToken cancellationToken = default) =>
        ReadinessQuestion.AskAsync(
            posted,
            ScriptedProcessRunner.Starting(ScriptedProcess.Printing([])),
            files,
            endpoint,
            cancellationToken);
}
