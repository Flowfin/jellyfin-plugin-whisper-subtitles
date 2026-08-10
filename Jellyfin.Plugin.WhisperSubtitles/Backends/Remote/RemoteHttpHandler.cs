using System;
using System.Net.Http;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

/// <summary>
/// Owns the one message handler every remote request goes through.
/// </summary>
/// <remarks>
/// A type this plugin owns rather than an <see cref="HttpMessageHandler"/>
/// registered under its own name. The collection the composition root writes into
/// is the server's, shared with every other installed plugin, and a registration
/// under a framework type is one the last writer wins: this plugin either loses
/// its handler to a stranger or hands a stranger its own, which is the shape #1
/// calls one plugin changing another's behaviour outside a declared interface.
///
/// One handler for the lifetime of the plugin, because a handler owns the
/// connection pool and one per request is socket exhaustion.
/// <see cref="SocketsHttpHandler.PooledConnectionLifetime"/> is what makes that
/// safe rather than merely cheap: a connection held forever survives a DNS
/// change, so an operator who moves their endpoint keeps reaching the old address
/// until the server restarts. Fifteen minutes costs a handshake four times an
/// hour. The container disposes what it constructed, so nothing else has to.
/// </remarks>
public sealed class RemoteHttpHandler : IDisposable
{
    private readonly SocketsHttpHandler _handler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    };

    /// <summary>
    /// Gets the handler the remote backend sends through.
    /// </summary>
    public HttpMessageHandler Handler => _handler;

    /// <inheritdoc />
    public void Dispose() => _handler.Dispose();
}
