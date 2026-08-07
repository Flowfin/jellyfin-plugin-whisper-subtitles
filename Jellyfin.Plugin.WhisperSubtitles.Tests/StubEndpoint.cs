using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// An endpoint that answers whatever a test tells it to, and records what it was
/// asked.
/// </summary>
/// <remarks>
/// A message handler rather than a server on a port. Nothing here opens a socket,
/// so the suite stays headless and offline, and the request the plugin built can
/// be read field by field, which a server would only let a test infer from what
/// came back.
/// </remarks>
internal sealed class StubEndpoint : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _answer;

    public StubEndpoint(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _answer = answer;
    }

    /// <summary>
    /// Gets how many requests reached this endpoint.
    /// </summary>
    public int Requests { get; private set; }

    /// <summary>
    /// Gets the URL of the last request, or null when none arrived.
    /// </summary>
    public Uri? RequestedUrl { get; private set; }

    /// <summary>
    /// Gets the authorization header of the last request, as it went over the wire.
    /// </summary>
    public string? Authorization { get; private set; }

    /// <summary>
    /// Gets every header value of the last request, joined, so a test can search
    /// all of them at once for something that should be in exactly one.
    /// </summary>
    public string HeaderText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the body of the last request as text.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// An endpoint answering with a status, a body and a declared content type.
    /// </summary>
    public static StubEndpoint Answering(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new((_, _) =>
        {
            var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        });

    /// <summary>
    /// An endpoint that answers with more bytes than it declares.
    /// </summary>
    /// <remarks>
    /// The declared length is the interesting half. A reader that believed
    /// Content-Length would let this one through, and Content-Length comes from the
    /// same machine as the body.
    /// </remarks>
    public static StubEndpoint AnsweringLonger(int bytes, long declaredLength) =>
        new((_, _) =>
        {
            var content = new StreamContent(new MemoryStream(new byte[bytes]));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentLength = declaredLength;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

    /// <summary>
    /// An endpoint that never answers, so the plugin's own deadline is what ends
    /// the request.
    /// </summary>
    public static StubEndpoint NeverAnswering() =>
        new(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);

            throw new InvalidOperationException("unreachable");
        });

    /// <summary>
    /// An endpoint that cannot be reached at all.
    /// </summary>
    public static StubEndpoint Unreachable(string message) =>
        new((_, _) => throw new HttpRequestException(message));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests++;
        RequestedUrl = request.RequestUri;
        Authorization = request.Headers.Authorization?.ToString();
        HeaderText = Describe(request);
        Body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return await _answer(request, cancellationToken).ConfigureAwait(false);
    }

    private static string Describe(HttpRequestMessage request)
    {
        var lines = new List<string>();

        foreach (var header in request.Headers)
        {
            lines.Add(header.Key + ": " + string.Join(", ", header.Value));
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                lines.Add(header.Key + ": " + string.Join(", ", header.Value));
            }
        }

        return string.Join("\n", lines);
    }
}
