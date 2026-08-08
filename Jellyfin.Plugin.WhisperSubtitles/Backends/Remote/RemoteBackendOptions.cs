using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

/// <summary>
/// What the remote backend needs to reach an endpoint, and nothing else.
/// </summary>
/// <remarks>
/// A type of its own rather than the plugin configuration, for the same reason
/// <see cref="Local.LocalBackendOptions"/> is one: the backend can be built and
/// driven in a test without a server writing a file. Where these values come
/// from, how they are validated and what happens to an invalid one is #40, and
/// the page an operator types them into is #36.
///
/// The key is a secret and this type does not make it less of one. It is held
/// here, it goes into one request header, and nothing in this namespace puts it
/// into a message, a URL or a form field. What holds that is
/// <see cref="RemoteWhisperBackend"/> and the tests over it.
/// </remarks>
public sealed class RemoteBackendOptions
{
    /// <summary>
    /// How many bytes of response this plugin will read before refusing it.
    /// </summary>
    /// <remarks>
    /// Eight mebibytes. A verbose transcript of a three hour film with one segment
    /// per sentence is on the order of a megabyte, so this leaves room for a long
    /// item and still refuses something that is not a transcript at all. The
    /// ceiling exists because the response comes from a machine this plugin knows
    /// nothing about: without it, a proxy in front of the endpoint that streams
    /// without stopping is a server process growing until it is killed.
    /// </remarks>
    public const long DefaultMaxResponseBytes = 8L * 1024 * 1024;

    /// <summary>
    /// The path this plugin posts to, under whatever base URL the operator gave.
    /// </summary>
    /// <remarks>
    /// Fixed rather than configurable. The request shape is the OpenAI audio
    /// transcription one, and an endpoint that speaks it serves it here; an
    /// endpoint that serves it somewhere else is not the same interface with a
    /// different path, it is a different interface.
    /// </remarks>
    public const string TranscriptionPath = "v1/audio/transcriptions";

    /// <summary>
    /// How long one transcription request may take before it is abandoned.
    /// </summary>
    /// <remarks>
    /// Ten minutes, and it covers the whole request: the upload of the audio, the
    /// endpoint's own transcription, and the response. An endpoint transcribes a
    /// long item slowly, so this is a setting an operator with feature films and a
    /// modest endpoint has to raise rather than a bound the plugin can pick for
    /// them. It is deliberately not infinite: a request nobody ever abandons holds
    /// a slot in the run for as long as the endpoint stays silent.
    /// </remarks>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteBackendOptions"/> class.
    /// </summary>
    /// <param name="baseUrl">The endpoint's base URL.</param>
    /// <param name="apiKey">The key to send, or null when the endpoint needs none.</param>
    /// <param name="model">The model name to ask the endpoint for.</param>
    public RemoteBackendOptions(string? baseUrl, string? apiKey, string? model)
        : this(baseUrl, apiKey, model, DefaultRequestTimeout, DefaultMaxResponseBytes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteBackendOptions"/> class.
    /// </summary>
    /// <param name="baseUrl">The endpoint's base URL.</param>
    /// <param name="apiKey">The key to send, or null when the endpoint needs none.</param>
    /// <param name="model">The model name to ask the endpoint for.</param>
    /// <param name="requestTimeout">How long one request may take.</param>
    /// <param name="maxResponseBytes">How many bytes of response to read before refusing it.</param>
    public RemoteBackendOptions(
        string? baseUrl,
        string? apiKey,
        string? model,
        TimeSpan requestTimeout,
        long maxResponseBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxResponseBytes, 0);

        BaseUrl = baseUrl;
        ApiKey = apiKey;
        Model = model;
        RequestTimeout = requestTimeout;
        MaxResponseBytes = maxResponseBytes;
    }

    /// <summary>
    /// Gets the endpoint's base URL, or null when the operator has named none.
    /// </summary>
    public string? BaseUrl { get; }

    /// <summary>
    /// Gets the key to send, or null when the endpoint needs none.
    /// </summary>
    /// <remarks>
    /// Optional, because an OpenAI compatible server an operator runs themselves
    /// commonly accepts anything or nothing here, and requiring a key would make
    /// them invent one.
    /// </remarks>
    public string? ApiKey { get; }

    /// <summary>
    /// Gets the model name to ask the endpoint for, or null when the operator has named none.
    /// </summary>
    /// <remarks>
    /// A name and not a choice from a list. Which models an endpoint serves is the
    /// endpoint's business, this plugin never enumerates them, and a list held here
    /// would be a list of one vendor's names.
    /// </remarks>
    public string? Model { get; }

    /// <summary>
    /// Gets how long one transcription request may take before it is abandoned.
    /// </summary>
    public TimeSpan RequestTimeout { get; }

    /// <summary>
    /// Gets how many bytes of response this plugin will read before refusing it.
    /// </summary>
    public long MaxResponseBytes { get; }

    /// <summary>
    /// Gets a value indicating whether the settings this backend cannot run without have been named.
    /// </summary>
    /// <remarks>
    /// Named, not reached. Whether the host resolves, answers, or is a
    /// transcription endpoint at all is the probe in #15, and this property says
    /// nothing about it.
    /// </remarks>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);

    /// <summary>
    /// Works out the URL to post to, or says why the configured one cannot be used.
    /// </summary>
    /// <param name="endpoint">The URL to post to, when there is one.</param>
    /// <param name="problem">What is wrong with the configured URL, when something is.</param>
    /// <returns>Whether a URL could be worked out.</returns>
    /// <remarks>
    /// Three shapes are accepted and the reason is what an operator actually pastes
    /// into the box. Some copy the host, some copy the host with the API version on
    /// it because that is what every client library calls a base URL, and some copy
    /// the full endpoint out of the documentation they were reading. All three name
    /// the same endpoint, and refusing two of them would be a plugin being right
    /// about a slash.
    ///
    /// What is not tolerated is anything about the host or the scheme. A URL that
    /// is relative, or that is not http or https, is refused rather than repaired,
    /// because guessing there is guessing where the audio goes.
    /// </remarks>
    public bool TryGetEndpoint(out Uri? endpoint, out string? problem)
    {
        endpoint = null;
        problem = null;

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            problem = "No endpoint URL is configured.";

            return false;
        }

        if (!Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, out var parsed))
        {
            problem = "The configured endpoint URL is not a complete URL. It has to start with https:// or http:// and name a host.";

            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            problem = string.Format(
                CultureInfo.InvariantCulture,
                "The configured endpoint URL uses the scheme {0}, and this backend speaks http and https only.",
                parsed.Scheme);

            return false;
        }

        var path = parsed.AbsolutePath.TrimEnd('/');

        if (path.EndsWith("/audio/transcriptions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new Uri(parsed.GetLeftPart(UriPartial.Authority) + path);

            return true;
        }

        var suffix = path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "/audio/transcriptions"
            : "/" + TranscriptionPath;

        endpoint = new Uri(parsed.GetLeftPart(UriPartial.Authority) + path + suffix);

        return true;
    }
}
