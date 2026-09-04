using Jellyfin.Plugin.WhisperSubtitles.Scheduling;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A server activity source that answers with whatever a test put in it.
/// </summary>
/// <remarks>
/// The stand-in for the one call the busy-server rule makes into the host. The real
/// source reads the server's session manager, which no test here has, and the rule
/// itself is a function of two numbers - so this is where those numbers come from
/// when the rule is exercised through something that asks for a source rather than
/// through the rule directly.
///
/// It is settable rather than fixed at construction, because the question the rule
/// answers is "what is the server doing NOW" and a run asks it once per item. A
/// source that could not change between two asks could not stand in for a server
/// somebody started watching halfway through a run, which is the case the limit
/// exists for.
///
/// It counts its asks, because "cheap" is a requirement of this seam rather than an
/// observation about it: the rule runs once per item to protect playback, and a
/// caller that asked several times per item would be paying that cost against the
/// thing it is protecting.
/// </remarks>
internal sealed class StubServerActivitySource : IServerActivitySource
{
    /// <summary>
    /// Gets or sets what the server is doing when it is next asked.
    /// </summary>
    public ServerActivity Activity { get; set; } = ServerActivity.Idle;

    /// <summary>
    /// Gets how many times it has been asked.
    /// </summary>
    public int Asks { get; private set; }

    /// <inheritdoc />
    public ServerActivity Current()
    {
        Asks++;

        return Activity;
    }
}
