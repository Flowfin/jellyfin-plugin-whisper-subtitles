using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What these are about is the failure half. A remote endpoint answers with
/// whatever it likes, including a login page, a gateway error and a body larger
/// than any transcript, and every one of those has to end as a typed reason with
/// nothing written and nothing leaked.
///
/// The audio is a real file in a temporary directory, because the backend opens
/// it, and the endpoint is a message handler, because nothing in this suite opens
/// a socket.
/// </summary>
public sealed class RemoteWhisperBackendTests : IDisposable
{
    private const string Key = "sk-a-key-nobody-may-see";

    private const string VerboseAnswer = """
        {
          "task": "transcribe",
          "language": "en",
          "duration": 4.5,
          "text": "A line somebody said. And another one.",
          "segments": [
            { "id": 0, "start": 0.0, "end": 2.25, "text": " A line somebody said." },
            { "id": 1, "start": 2.25, "end": 4.5, "text": " And another one." }
          ]
        }
        """;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    public RemoteWhisperBackendTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(AudioPath, Encoding.ASCII.GetBytes("not really audio"));
    }

    private string AudioPath => Path.Combine(_directory, "A Film.wav");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task A_verbose_answer_becomes_the_segments_it_carries()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);

        var result = await Transcribe(endpoint, "en");

        Assert.Equal("en", result.Language);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(TimeSpan.Zero, result.Segments[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.25), result.Segments[0].End);
        Assert.Equal("A line somebody said.", result.Segments[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(4.5), result.Segments[1].End);
    }

    [Fact]
    public async Task The_request_names_the_endpoint_the_model_and_the_format_and_carries_the_key_in_one_header()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);

        await Transcribe(endpoint, "en");

        Assert.Equal(
            new Uri("https://transcription.example/v1/audio/transcriptions"),
            endpoint.RequestedUrl);

        var body = Unquoted(endpoint.Body);

        Assert.Contains("name=model", body, StringComparison.Ordinal);
        Assert.Contains("a-model", body, StringComparison.Ordinal);
        Assert.Contains("verbose_json", body, StringComparison.Ordinal);
        Assert.Contains("name=language", body, StringComparison.Ordinal);
        Assert.Contains("A Film.wav", body, StringComparison.Ordinal);

        // The one place the key is allowed, and nowhere else. A URL ends up in
        // whatever logs a request, and a form field ends up in whatever echoes one
        // back.
        Assert.Equal("Bearer " + Key, endpoint.Authorization);
        Assert.DoesNotContain(Key, endpoint.RequestedUrl!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, endpoint.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_that_names_no_language_asks_the_endpoint_to_detect_one()
    {
        // Omitted rather than sent empty. An endpoint given language= with nothing
        // after it is being told the language is called nothing.
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);

        var result = await Transcribe(endpoint, language: null);

        Assert.DoesNotContain("name=language", Unquoted(endpoint.Body), StringComparison.Ordinal);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task An_endpoint_that_needs_no_key_is_sent_none()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);

        await Transcribe(endpoint, "en", Options(key: null));

        Assert.Null(endpoint.Authorization);
    }

    [Fact]
    public async Task A_refusal_with_a_body_is_the_backend_failing_and_quotes_what_it_said()
    {
        var endpoint = StubEndpoint.Answering(
            HttpStatusCode.BadRequest,
            """{ "error": { "message": "The model a-model does not exist." } }""");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(endpoint, "en"));

        Assert.Equal(TranscriptionFailureReason.BackendFailed, failed.Reason);
        Assert.Contains("400", failed.Message, StringComparison.Ordinal);
        Assert.Contains("The model a-model does not exist.", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refusal_that_echoes_the_key_back_does_not_carry_it_into_the_message()
    {
        // A gateway refusing a request commonly quotes the headers it received. The
        // plugin's own message is the place that leak becomes permanent, because
        // that string is what goes into a log an operator may paste somewhere.
        var endpoint = StubEndpoint.Answering(
            HttpStatusCode.Unauthorized,
            "Rejected request with header Authorization: Bearer " + Key);

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(endpoint, "en"));

        Assert.Equal(TranscriptionFailureReason.BackendFailed, failed.Reason);
        Assert.DoesNotContain(Key, failed.ToString(), StringComparison.Ordinal);
        Assert.Contains(RemoteWhisperBackend.RedactedKey, failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_that_answers_with_a_server_error_is_the_backend_failing()
    {
        // Reachable and refusing is BackendFailed rather than BackendUnreachable,
        // which is the division docs/troubleshooting.md turns into two different
        // things for an operator to do.
        var endpoint = StubEndpoint.Answering(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html>", "text/html");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(endpoint, "en"));

        Assert.Equal(TranscriptionFailureReason.BackendFailed, failed.Reason);
        Assert.Contains("502", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_that_does_not_answer_within_the_timeout_is_unreachable()
    {
        // The wait is the plugin's own deadline rather than a sleep in the test, and
        // the setting bounds it: twenty milliseconds is how long this test can take,
        // whatever the machine.
        var endpoint = StubEndpoint.NeverAnswering();

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Transcribe(endpoint, "en", Options(timeout: TimeSpan.FromMilliseconds(20))));

        Assert.Equal(TranscriptionFailureReason.BackendUnreachable, failed.Reason);
        Assert.Contains("did not answer", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_endpoint_that_cannot_be_reached_is_unreachable()
    {
        var endpoint = StubEndpoint.Unreachable("No such host is known.");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(endpoint, "en"));

        Assert.Equal(TranscriptionFailureReason.BackendUnreachable, failed.Reason);
        Assert.Contains("No such host is known.", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_operator_stopping_the_run_is_cancellation_and_not_a_timeout()
    {
        // The two arrive at the same catch and mean opposite things. A cancelled run
        // reported as an unreachable endpoint sends somebody to look at their
        // network for a request they stopped themselves.
        using var stopping = new CancellationTokenSource();

        var endpoint = new StubEndpoint(async (_, token) =>
        {
            await stopping.CancelAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            throw new InvalidOperationException("unreachable");
        });

        var backend = new RemoteWhisperBackend(endpoint, Options());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backend.TranscribeAsync(
                new TranscriptionRequest(AudioPath, "en"),
                new Progress<double>(),
                stopping.Token));
    }

    [Fact]
    public async Task An_answer_longer_than_the_ceiling_is_refused_unread()
    {
        // Declared as ten bytes and a hundred thousand long. The ceiling is on what
        // arrives, so the declaration buys the endpoint nothing.
        var endpoint = StubEndpoint.AnsweringLonger(bytes: 100_000, declaredLength: 10);

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => Transcribe(endpoint, "en", Options(maxResponseBytes: 4096)));

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.Contains("4096", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_is_unparseable_whatever_it_declares()
    {
        var page = StubEndpoint.Answering(HttpStatusCode.OK, "<html><body>Please sign in</body></html>", "text/html");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(page, "en"));

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.Contains("text/html", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_declaring_json_and_carrying_something_else_is_unparseable()
    {
        var lying = StubEndpoint.Answering(HttpStatusCode.OK, "<html><body>Please sign in</body></html>");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(lying, "en"));

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.Contains("not JSON", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_with_no_timings_is_unparseable_rather_than_an_empty_subtitle()
    {
        var plain = StubEndpoint.Answering(HttpStatusCode.OK, """{ "text": "A line somebody said." }""");

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(plain, "en"));

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.Contains("no segments", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_key_reaches_no_message_this_backend_produces_on_any_failure_path()
    {
        // One assertion over every way this backend can fail, rather than a line in
        // each test above. The rule is about the class and not about one path, so a
        // path added later without a line here is the failure this catches.
        var endpoints = new (string Name, StubEndpoint Endpoint)[]
        {
            ("a refusal quoting the key", StubEndpoint.Answering(HttpStatusCode.Forbidden, "Authorization: Bearer " + Key)),
            ("a refusal with no body", StubEndpoint.Answering(HttpStatusCode.InternalServerError, string.Empty)),
            ("an unreachable host quoting the key", StubEndpoint.Unreachable("Connecting with Bearer " + Key + " failed.")),
            ("a login page", StubEndpoint.Answering(HttpStatusCode.OK, "<html>" + Key + "</html>", "text/html")),
            ("json that is not a transcription", StubEndpoint.Answering(HttpStatusCode.OK, """{ "text": "no timings" }""")),
            ("an answer over the ceiling", StubEndpoint.AnsweringLonger(bytes: 100_000, declaredLength: 10)),
            ("an endpoint that never answers", StubEndpoint.NeverAnswering()),
        };

        foreach (var (name, endpoint) in endpoints)
        {
            var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
                () => Transcribe(
                    endpoint,
                    "en",
                    Options(timeout: TimeSpan.FromMilliseconds(20), maxResponseBytes: 4096)));

            Assert.DoesNotContain(Key, failed.ToString(), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(failed.Message), name + " failed without saying anything");
        }
    }

    [Fact]
    public async Task Audio_that_cannot_be_read_ends_the_item_before_anything_is_sent()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);
        var backend = new RemoteWhisperBackend(endpoint, Options());

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => backend.TranscribeAsync(
                new TranscriptionRequest(Path.Combine(_directory, "not-here.wav"), "en"),
                new Progress<double>(),
                CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.AudioUnreadable, failed.Reason);
        Assert.Equal(0, endpoint.Requests);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("https://transcription.example", null)]
    [InlineData(null, "a-model")]
    public async Task A_backend_missing_a_setting_says_which_and_sends_nothing(string? baseUrl, string? model)
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);
        var backend = new RemoteWhisperBackend(endpoint, new RemoteBackendOptions(baseUrl, Key, model));

        var readiness = await backend.CheckReadinessAsync(CancellationToken.None);

        Assert.False(readiness.IsReady);
        Assert.Contains("endpoint URL and a model name", readiness.Reason!, StringComparison.Ordinal);

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => backend.TranscribeAsync(
                new TranscriptionRequest(AudioPath, "en"),
                new Progress<double>(),
                CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendNotReady, failed.Reason);
        Assert.Equal(0, endpoint.Requests);
    }

    [Theory]
    [InlineData("transcription.example/v1")]
    [InlineData("ftp://transcription.example/v1")]
    [InlineData("file:///etc/passwd")]
    public async Task A_url_that_names_no_reachable_scheme_and_host_is_refused_rather_than_repaired(string configured)
    {
        // Guessing about a path is a plugin being right about a slash. Guessing about
        // a scheme or a host is a plugin deciding where the audio goes.
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);
        var backend = new RemoteWhisperBackend(endpoint, new RemoteBackendOptions(configured, Key, "a-model"));

        var readiness = await backend.CheckReadinessAsync(CancellationToken.None);

        Assert.False(readiness.IsReady);

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => backend.TranscribeAsync(
                new TranscriptionRequest(AudioPath, "en"),
                new Progress<double>(),
                CancellationToken.None));

        Assert.Equal(TranscriptionFailureReason.BackendNotReady, failed.Reason);
        Assert.Equal(0, endpoint.Requests);
    }

    [Theory]
    [InlineData("https://transcription.example")]
    [InlineData("https://transcription.example/")]
    [InlineData("https://transcription.example/v1")]
    [InlineData("https://transcription.example/v1/")]
    [InlineData("https://transcription.example/v1/audio/transcriptions")]
    public async Task Every_shape_an_operator_pastes_reaches_the_same_endpoint(string configured)
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);
        var backend = new RemoteWhisperBackend(endpoint, new RemoteBackendOptions(configured, Key, "a-model"));

        await backend.TranscribeAsync(
            new TranscriptionRequest(AudioPath, "en"),
            new Progress<double>(),
            CancellationToken.None);

        Assert.Equal(
            new Uri("https://transcription.example/v1/audio/transcriptions"),
            endpoint.RequestedUrl);
    }

    [Fact]
    public async Task A_path_under_a_prefix_keeps_the_prefix()
    {
        // An endpoint behind a reverse proxy on a sub-path is the ordinary
        // self-hosted case, and dropping the prefix would post to the proxy's root.
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);
        var backend = new RemoteWhisperBackend(
            endpoint,
            new RemoteBackendOptions("https://box.example/whisper/", Key, "a-model"));

        await backend.TranscribeAsync(
            new TranscriptionRequest(AudioPath, "en"),
            new Progress<double>(),
            CancellationToken.None);

        Assert.Equal(
            new Uri("https://box.example/whisper/v1/audio/transcriptions"),
            endpoint.RequestedUrl);
    }

    [Fact]
    public async Task A_configured_endpoint_is_ready_without_anything_being_reached()
    {
        var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer);
        var backend = new RemoteWhisperBackend(endpoint, Options());

        var readiness = await backend.CheckReadinessAsync(CancellationToken.None);

        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Reason);
        Assert.Equal(0, endpoint.Requests);
    }

    [Fact]
    public void The_estimate_never_makes_a_longer_item_cheaper_than_a_shorter_one()
    {
        var backend = new RemoteWhisperBackend(StubEndpoint.NeverAnswering(), Options());

        var shorter = backend.EstimateCost(TimeSpan.FromMinutes(20));
        var longer = backend.EstimateCost(TimeSpan.FromMinutes(90));

        Assert.True(shorter.Shortest <= shorter.Longest);
        Assert.True(longer.Shortest >= shorter.Shortest);
        Assert.True(longer.Longest >= shorter.Longest);
    }

    [Fact]
    public async Task Progress_reaches_one_when_segments_came_back_and_not_when_none_did()
    {
        // No intermediate report, because the endpoint sends none and a fraction
        // this plugin made up is a progress bar that lies at whatever rate it was
        // written to. One report at the end is what it honestly knows.
        var done = new RecordingProgress();

        var backend = new RemoteWhisperBackend(
            StubEndpoint.Answering(HttpStatusCode.OK, VerboseAnswer),
            Options());

        await backend.TranscribeAsync(new TranscriptionRequest(AudioPath, "en"), done, CancellationToken.None);

        Assert.Equal(new[] { 1d }, done.Reported);

        var refused = new RecordingProgress();
        var failing = new RemoteWhisperBackend(
            StubEndpoint.Answering(HttpStatusCode.BadRequest, "no"),
            Options());

        await Assert.ThrowsAsync<TranscriptionFailedException>(
            () => failing.TranscribeAsync(new TranscriptionRequest(AudioPath, "en"), refused, CancellationToken.None));

        Assert.Empty(refused.Reported);
    }

    private static string Unquoted(string text) => text.Replace("\"", string.Empty, StringComparison.Ordinal);

    private static RemoteBackendOptions Options(
        string? key = Key,
        TimeSpan? timeout = null,
        long? maxResponseBytes = null) =>
        new(
            "https://transcription.example",
            key,
            "a-model",
            timeout ?? TimeSpan.FromMinutes(1),
            maxResponseBytes ?? RemoteBackendOptions.DefaultMaxResponseBytes);

    private Task<TranscriptionResult> Transcribe(
        StubEndpoint endpoint,
        string? language,
        RemoteBackendOptions? options = null)
    {
        var backend = new RemoteWhisperBackend(endpoint, options ?? Options());

        return backend.TranscribeAsync(
            new TranscriptionRequest(AudioPath, language),
            new Progress<double>(),
            CancellationToken.None);
    }
}
