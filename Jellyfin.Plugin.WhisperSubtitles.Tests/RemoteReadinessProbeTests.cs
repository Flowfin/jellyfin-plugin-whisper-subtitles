using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What the remote backend answers when it is asked whether it can be used, and
/// what it sent to find out.
/// </summary>
/// <remarks>
/// The endpoint is a message handler, so nothing here opens a socket and the
/// request the probe built can be read field by field. What that buys is the two
/// assertions this suite would otherwise have to infer: that no audio left the
/// machine, and that the key went into a header and into no reason.
/// </remarks>
public sealed class RemoteReadinessProbeTests
{
    private const string Key = "sk-a-key-nobody-may-see";

    [Fact]
    public async Task An_endpoint_that_is_not_configured_is_named_before_anything_is_sent()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var readiness = await Probe(endpoint, new RemoteBackendOptions(null, null, null));

        Assert.False(readiness.IsReady);
        Assert.Contains("configuration page", readiness.Reason, StringComparison.Ordinal);
        Assert.Equal(0, endpoint.Requests);
    }

    [Fact]
    public async Task A_URL_this_backend_could_not_post_to_is_refused_before_anything_is_sent()
    {
        // The reason comes from the same reading that builds the endpoint for a
        // transcription, so a URL refused here is refused there for the same words.
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var readiness = await Probe(endpoint, Options(baseUrl: "ftp://box.example/whisper"));

        Assert.False(readiness.IsReady);
        Assert.Contains("http", readiness.Reason, StringComparison.Ordinal);
        Assert.Equal(0, endpoint.Requests);
    }

    [Fact]
    public async Task An_endpoint_that_answers_is_ready_and_was_asked_for_nothing_but_a_status()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, "{}");

        var readiness = await Probe(endpoint, Options());

        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Reason);
        Assert.Equal(1, endpoint.Requests);

        // No audio, and the key in a header. Both are the transcription's rules
        // holding on the one request that is not a transcription.
        Assert.Empty(endpoint.Body);
        Assert.Equal("Bearer " + Key, endpoint.Authorization);
        Assert.Equal(
            new Uri("https://transcription.example/v1/audio/transcriptions"),
            endpoint.RequestedUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_endpoint_that_will_not_answer_this_method_still_answered(HttpStatusCode status)
    {
        // The near-miss worth spending the effort on. A probe that required success
        // would refuse every endpoint that accepts POST at that URL and nothing
        // else, which is the ordinary shape of a transcription server, and the
        // operator would be sent to fix a configuration that was right.
        var readiness = await Probe(StubEndpoint.Answering(status, string.Empty), Options());

        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_refused_key_is_the_one_thing_a_status_says_about_the_configuration(HttpStatusCode status)
    {
        var readiness = await Probe(StubEndpoint.Answering(status, string.Empty), Options());

        Assert.False(readiness.IsReady);
        Assert.Contains("refused the configured key", readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_that_needs_a_key_where_none_is_set_says_which_of_the_two_it_is()
    {
        // The same status and a different sentence, because the two send an operator
        // to different places: one to check what they typed, one to type anything.
        var readiness = await Probe(
            StubEndpoint.Answering(HttpStatusCode.Unauthorized, string.Empty),
            Options(key: null));

        Assert.False(readiness.IsReady);
        Assert.Contains("without a key", readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_that_cannot_be_reached_says_so_and_quotes_what_went_wrong()
    {
        var readiness = await Probe(StubEndpoint.Unreachable("No such host is known."), Options());

        Assert.False(readiness.IsReady);
        Assert.Contains("could not be reached", readiness.Reason, StringComparison.Ordinal);
        Assert.Contains("No such host is known.", readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_that_does_not_answer_ends_at_the_probe_deadline()
    {
        // The wait is the plugin's own deadline rather than a sleep in this test,
        // and the setting bounds it: twenty milliseconds is how long this test can
        // take, whatever the machine.
        var options = Options(probeTimeout: TimeSpan.FromMilliseconds(20));

        var readiness = await Probe(StubEndpoint.NeverAnswering(), options);

        Assert.False(readiness.IsReady);
        Assert.Contains("did not answer", readiness.Reason, StringComparison.Ordinal);
        Assert.Contains(
            options.ProbeTimeout.ToString("g", CultureInfo.InvariantCulture),
            readiness.Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_probe_deadline_is_its_own_and_not_the_transcription_one()
    {
        // Without this the pair passes with one field read twice. The transcription
        // timeout here is long enough that a probe using it would outlive this test
        // rather than answer, so the deadline that fired is the probe's.
        var options = new RemoteBackendOptions(
            "https://transcription.example",
            Key,
            "a-model",
            TimeSpan.FromHours(1),
            RemoteBackendOptions.DefaultMaxResponseBytes,
            TimeSpan.FromMilliseconds(20));

        var readiness = await Probe(StubEndpoint.NeverAnswering(), options);

        Assert.False(readiness.IsReady);
        Assert.Contains("did not answer", readiness.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_operator_who_stops_the_probe_is_not_told_the_endpoint_is_slow()
    {
        // The stop lands while the request is in flight rather than before it is
        // made. Cancelling first is refused at the probe's first line, which is a
        // green test that never reaches the catch this is about.
        using var stopping = new CancellationTokenSource();

        var endpoint = new StubEndpoint(async (_, token) =>
        {
            await stopping.CancelAsync().ConfigureAwait(true);

            var never = new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using var cancels = token.Register(() => never.TrySetCanceled(token));

            return await never.Task.ConfigureAwait(true);
        });

        var backend = new RemoteWhisperBackend(endpoint, Options());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backend.CheckReadinessAsync(stopping.Token));
    }

    [Fact]
    public async Task The_key_reaches_no_reason_this_probe_produces_on_any_path()
    {
        // The list rather than one endpoint, for the same reason the transcription
        // suite keeps one: the rule is about the class and not about one path, so a
        // path added later with no line here is what this catches.
        var endpoints = new (string Name, StubEndpoint Endpoint)[]
        {
            ("a refusal quoting the key", StubEndpoint.Answering(HttpStatusCode.Forbidden, "Authorization: Bearer " + Key)),
            ("a server error", StubEndpoint.Answering(HttpStatusCode.InternalServerError, string.Empty)),
            ("an unreachable host quoting the key", StubEndpoint.Unreachable("Connecting with Bearer " + Key + " failed.")),
            ("an endpoint that never answers", StubEndpoint.NeverAnswering()),
        };

        foreach (var (name, endpoint) in endpoints)
        {
            var readiness = await Probe(endpoint, Options(probeTimeout: TimeSpan.FromMilliseconds(20)));

            Assert.DoesNotContain(Key, readiness.Reason ?? string.Empty, StringComparison.Ordinal);
            Assert.False(
                readiness.IsReady && readiness.Reason is not null,
                name + " answered ready and gave a reason anyway");
        }
    }

    [Fact]
    public void A_probe_deadline_of_nothing_is_refused_where_it_is_configured()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Options(probeTimeout: TimeSpan.Zero));
    }

    private static RemoteBackendOptions Options(
        string? key = Key,
        string baseUrl = "https://transcription.example",
        TimeSpan? probeTimeout = null) =>
        new(
            baseUrl,
            key,
            "a-model",
            TimeSpan.FromMinutes(1),
            RemoteBackendOptions.DefaultMaxResponseBytes,
            probeTimeout ?? RemoteBackendOptions.DefaultProbeTimeout);

    private static async Task<BackendReadiness> Probe(HttpMessageHandler endpoint, RemoteBackendOptions options) =>
        await new RemoteWhisperBackend(endpoint, options).CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);
}
