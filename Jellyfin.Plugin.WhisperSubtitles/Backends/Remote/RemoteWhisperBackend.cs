using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

/// <summary>
/// Sends the audio to an endpoint that speaks the OpenAI audio transcription
/// request, and reads timed segments back.
/// </summary>
/// <remarks>
/// One request shape reaches several servers an operator may already have, which
/// is why it was chosen. It is not an endorsement of a vendor and nothing here
/// names one: the base URL is whatever the operator typed, and a self-hosted
/// server speaking the same request is the case this was written against as much
/// as any hosted one.
///
/// What leaves the machine is the extracted audio of every selected item, and
/// where it goes is the host in that URL. That is a decision only an operator can
/// make, and the configuration page states it before this backend can be chosen,
/// in the block it carries as WhisperSubtitlesRemoteDisclosure: shown and hidden
/// with the remote settings, naming the host read out of the URL an operator
/// typed and never the key beside it.
///
/// What #81 still owes is the same three facts in the log line written the first
/// time a run uses this backend, so that somebody reading logs afterwards sees it
/// was in use without opening the configuration. Nothing in this plugin logs at
/// all, and the first logger is #73.
///
/// TLS verification is not something this class turns off, and there is no option
/// for it. The bound worth stating rather than implying: the handler is injected,
/// so the verification behaviour is a property of the handler this backend is
/// given. Nothing here relaxes it, and the real handler is composed in #71. An
/// operator with a private certificate authority installs it where their runtime
/// looks, which is outside this plugin.
/// </remarks>
public sealed class RemoteWhisperBackend : ITranscriptionBackend
{
    /// <summary>
    /// The name this backend reports.
    /// </summary>
    public const string BackendName = "Remote";

    /// <summary>
    /// What replaces the key wherever text this plugin produces might have carried it.
    /// </summary>
    public const string RedactedKey = "[redacted]";

    /// <summary>
    /// How much of an endpoint's refusal is quoted back to the operator.
    /// </summary>
    /// <remarks>
    /// A refusal usually names its cause in the first sentence, and the whole body
    /// can be a web page. Bounded rather than trusted, because this string ends up
    /// in a log line and in a run's outcome.
    /// </remarks>
    private const int RefusalQuoteLimit = 512;

    private readonly HttpMessageHandler _handler;
    private readonly RemoteBackendOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteWhisperBackend"/> class.
    /// </summary>
    /// <param name="handler">The seam every request goes through.</param>
    /// <param name="options">The endpoint this backend was configured with.</param>
    /// <remarks>
    /// A message handler and not an <see cref="HttpClient"/>, so a test drives
    /// every path in here without a socket and without a server to point at. The
    /// handler owns the connection pool and outlives any one request, so this class
    /// never disposes it.
    /// </remarks>
    public RemoteWhisperBackend(HttpMessageHandler handler, RemoteBackendOptions options)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The model and language lists are empty, and both are statements rather than
    /// gaps. Which models an endpoint serves and which languages they cover is the
    /// endpoint's business; this plugin never asks it to enumerate either, and a
    /// list here would be one vendor's names presented as the interface.
    ///
    /// Detection is offered, because the request may omit the language and the
    /// verbose response states the language it produced. What the response does not
    /// carry is a confidence, and that pair is now said in two flags rather than
    /// one: it detects, and it cannot say how sure it is.
    ///
    /// What follows from the pair is decided in <see cref="Detection.LanguageAcceptance"/>
    /// and not here. This backend is used with a language named for the library,
    /// and a run that asks it to detect is refused before any audio is extracted.
    /// The capability is still reported as it is rather than hidden to make the
    /// refusal go away, because an endpoint that grows a confidence field turns
    /// this into a one-line change instead of a rediscovery.
    /// </remarks>
    public BackendDescription Description { get; } = new(
        BackendName,
        Array.Empty<string>(),
        Array.Empty<string>(),
        canDetectLanguage: true,
        canReportLanguageConfidence: false,
        cancellationBudget: TimeSpan.FromSeconds(5));

    /// <inheritdoc />
    /// <remarks>
    /// The configuration is read first and the endpoint is only reached once it
    /// makes sense to, so an operator who has typed nothing is told that rather than
    /// being told a request failed.
    ///
    /// The request is a GET to the same URL a transcription is posted to, and it is
    /// the cheapest thing that can be asked of it: no audio leaves the machine, and
    /// the endpoint is not asked to enumerate anything, so nothing here depends on a
    /// path beyond the one this backend already uses. An endpoint that accepts POST
    /// there and nothing else refuses the method, which is an answer and therefore a
    /// pass. What it separates is a host that answers from one that does not, and a
    /// key this endpoint accepts from one it refuses.
    ///
    /// WHAT A READY ANSWER DOES NOT MEAN, said here because the opposite is the easy
    /// reading of a green tick: nothing has been transcribed, so nothing here shows
    /// the endpoint can transcribe, that it serves the configured model, or that it
    /// will still be there when a run starts. It reached the host, and the host did
    /// not refuse the key.
    /// </remarks>
    public async Task<BackendReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsComplete)
        {
            return new BackendReadiness(
                false,
                "The remote backend needs an endpoint URL and a model name. Set both on the plugin's configuration page.");
        }

        if (!_options.TryGetEndpoint(out var endpoint, out var problem))
        {
            return new BackendReadiness(false, problem);
        }

        using var client = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.ProbeTimeout);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
            }

            // Headers and not the body. What this reads is the status line, and a
            // probe that pulled a response body down would be the one request in
            // this plugin with no ceiling on what it reads.
            using var response = await client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new BackendReadiness(
                    false,
                    string.IsNullOrWhiteSpace(_options.ApiKey)
                        ? "The endpoint answered, and it will not accept a request without a key. Set one on the plugin's configuration page."
                        : "The endpoint answered, and it refused the configured key.");
            }

            return new BackendReadiness(true, null);
        }
        catch (OperationCanceledException)
        {
            // The caller's token wins the tie, for the same reason it wins it in a
            // transcription: somebody who navigated away from the page has not
            // discovered anything about their endpoint.
            cancellationToken.ThrowIfCancellationRequested();

            return new BackendReadiness(
                false,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The endpoint did not answer within {0}.",
                    _options.ProbeTimeout.ToString("g", CultureInfo.InvariantCulture)));
        }
        catch (HttpRequestException unreached)
        {
            return new BackendReadiness(
                false,
                "The endpoint could not be reached. " + Redact(unreached.Message));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A placeholder range and marked as one wherever it is shown, for the same
    /// reason the local backend's is: nothing here has measured this endpoint. It
    /// is wider than the local one at the fast end because the work happens on a
    /// machine whose size this plugin cannot see, and a busy endpoint queues.
    /// What replaces it is a measurement, which needs a run over an item that the
    /// scheduled task does not perform, so it waits on #183, and #37 refuses to
    /// show a number when there is none.
    ///
    /// Linear in the media duration, so a longer item never costs less than a
    /// shorter one, which is the one property a caller may rely on.
    /// </remarks>
    public CostEstimate EstimateCost(TimeSpan mediaDuration) =>
        new(mediaDuration / 5, mediaDuration * 5);

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        if (!_options.IsComplete)
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.BackendNotReady,
                "The remote backend has no endpoint URL or no model name.");
        }

        if (!_options.TryGetEndpoint(out var endpoint, out var problem))
        {
            throw new TranscriptionFailedException(
                TranscriptionFailureReason.BackendNotReady,
                problem!);
        }

        // Before the request rather than after it. An audio file that cannot be
        // opened is a fact about this machine, and finding it out by uploading
        // nothing to somebody else's endpoint would report their answer as the
        // reason.
        FileStream audio;

        try
        {
            audio = File.OpenRead(request.AudioFilePath);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw Failure(
                TranscriptionFailureReason.AudioUnreadable,
                "The extracted audio could not be read, so nothing was sent.",
                unreadable);
        }

        // The client is a wrapper over the injected handler and holds no state worth
        // keeping; the handler holds the connections. Its own timeout is off because
        // the one below is linked to the caller's token, which is what lets a
        // timeout be told apart from an operator stopping the run.
        using var client = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);

        try
        {
            using var content = BuildBody(request, audio);
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                // The one place the key is written. It goes in a header, not in the
                // URL and not in the body, so nothing that logs a URL or echoes a
                // form field can carry it.
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
            }

            using var response = await client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new TranscriptionFailedException(
                    TranscriptionFailureReason.BackendFailed,
                    await DescribeRefusalAsync(response, deadline.Token).ConfigureAwait(false));
            }

            var declared = response.Content.Headers.ContentType?.MediaType;

            if (!IsJson(declared))
            {
                throw new TranscriptionFailedException(
                    TranscriptionFailureReason.OutputUnparseable,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The endpoint answered successfully with {0} rather than JSON, so the URL points at something other than a transcription endpoint.",
                        declared is null ? "no declared content type" : "content type " + declared));
            }

            var body = await ReadBoundedAsync(response.Content, _options.MaxResponseBytes, deadline.Token)
                .ConfigureAwait(false);

            if (body is null)
            {
                throw new TranscriptionFailedException(
                    TranscriptionFailureReason.OutputUnparseable,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The endpoint's answer is longer than the {0} bytes this plugin will read, so it was refused unread.",
                        _options.MaxResponseBytes.ToString(CultureInfo.InvariantCulture)));
            }

            if (!TranscriptionResponseReader.TryRead(body.Value, request.Language, out var segments, out var language, out var unreadable))
            {
                throw new TranscriptionFailedException(
                    TranscriptionFailureReason.OutputUnparseable,
                    Redact(unreadable ?? "The endpoint's answer could not be read as timed segments."));
            }

            progress.Report(1);

            return new TranscriptionResult(segments, language!);
        }
        catch (OperationCanceledException stopped)
        {
            // The caller's token wins the tie. An operator stopping a run while a
            // request happened to be timing out has cancelled, and reporting that as
            // an unreachable endpoint would send them looking at their network.
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw Failure(
                TranscriptionFailureReason.BackendUnreachable,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The endpoint did not answer within {0}.",
                    _options.RequestTimeout.ToString("g", CultureInfo.InvariantCulture)),
                stopped);
        }
        catch (HttpRequestException unreached)
        {
            throw Failure(
                TranscriptionFailureReason.BackendUnreachable,
                "The endpoint could not be reached. " + Redact(unreached.Message),
                unreached);
        }
        finally
        {
            await audio.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the multipart request the endpoint reads.
    /// </summary>
    /// <remarks>
    /// Four fields at most: the audio, the model, the response format, and the
    /// language when the request named one. A request that names no language is
    /// asking the endpoint to detect one, and omitting the field is how that is
    /// spelled; sending it empty is how an endpoint comes to transcribe in a
    /// language called nothing.
    /// </remarks>
    private MultipartFormDataContent BuildBody(TranscriptionRequest request, Stream audio)
    {
        var content = new MultipartFormDataContent();

        var file = new StreamContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", Path.GetFileName(request.AudioFilePath));

        content.Add(new StringContent(_options.Model!.Trim()), "model");
        content.Add(new StringContent(TranscriptionResponseReader.RequiredResponseFormat), "response_format");

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            content.Add(new StringContent(request.Language.Trim()), "language");
        }

        return content;
    }

    /// <summary>
    /// Says what an endpoint refused with, without repeating its whole answer.
    /// </summary>
    /// <remarks>
    /// The body is quoted because the cause is usually in it: a model the endpoint
    /// does not have, a request larger than it allows, a credential it did not
    /// accept. It is quoted bounded and redacted, because a gateway that refuses a
    /// request commonly echoes the headers it received, and this string is on its
    /// way to a log the operator may share.
    /// </remarks>
    private async Task<string> DescribeRefusalAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);

        var said = await ReadBoundedAsync(response.Content, RefusalQuoteLimit, cancellationToken)
            .ConfigureAwait(false);

        var quoted = said is null || said.Value.Length == 0
            ? "It said nothing about why."
            : Redact(Encoding.UTF8.GetString(said.Value.Span).Trim());

        return string.Format(
            CultureInfo.InvariantCulture,
            "The endpoint refused the request with status {0}. {1}",
            status,
            quoted);
    }

    /// <summary>
    /// Reads a response body, and refuses one longer than it is allowed to be.
    /// </summary>
    /// <returns>The bytes, or null when there were more than <paramref name="limit"/> of them.</returns>
    /// <remarks>
    /// The bound is on the bytes that arrive and not on the declared length.
    /// Content-Length comes from the same machine as the body, so a response can
    /// declare one byte and send until the process dies; reading the header would
    /// be asking the thing being bounded how big it is.
    /// </remarks>
    private static async Task<ReadOnlyMemory<byte>?> ReadBoundedAsync(
        HttpContent content,
        long limit,
        CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            using var read = new MemoryStream();
            var chunk = new byte[8192];

            while (true)
            {
                var got = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);

                if (got == 0)
                {
                    return read.ToArray();
                }

                if (read.Length + got > limit)
                {
                    return null;
                }

                await read.WriteAsync(chunk.AsMemory(0, got), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <remarks>
    /// The suffix rather than the exact type, so an endpoint answering
    /// <c>application/vnd.something+json</c> is read and an endpoint answering
    /// <c>text/html</c> is not.
    /// </remarks>
    private static bool IsJson(string? mediaType) =>
        mediaType is not null
        && mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the failure, and leaves behind an underlying exception that mentions
    /// the key.
    /// </summary>
    /// <remarks>
    /// Redacting this plugin's own message is not enough, and a test caught that
    /// rather than a review: printing an exception prints the ones under it, so a
    /// transport error that quoted the key put it back into everything that logs a
    /// caught exception. The cause is still in the message above, redacted, so what
    /// is lost when this drops one is a stack trace rather than the reason.
    /// </remarks>
    private TranscriptionFailedException Failure(
        TranscriptionFailureReason reason,
        string message,
        Exception underlying) =>
        Mentions(underlying)
            ? new TranscriptionFailedException(reason, message)
            : new TranscriptionFailedException(reason, message, underlying);

    private bool Mentions(Exception underlying) =>
        !string.IsNullOrWhiteSpace(_options.ApiKey)
        && underlying.ToString().Contains(_options.ApiKey.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// Takes the configured key out of text this plugin is about to hand somebody.
    /// </summary>
    /// <remarks>
    /// The rule is that the key is never logged, never echoed back and never in an
    /// error message, and the only text here that comes from outside is what the
    /// endpoint said. So this is the one place that can break the rule, and it is
    /// the place that holds it: a gateway echoing the Authorization header into its
    /// refusal is the case, and it has happened to enough people to be the reason
    /// this function exists rather than a hypothetical.
    /// </remarks>
    private string Redact(string text)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return text;
        }

        var key = _options.ApiKey.Trim();

        return text.Replace(key, RedactedKey, StringComparison.Ordinal);
    }
}
