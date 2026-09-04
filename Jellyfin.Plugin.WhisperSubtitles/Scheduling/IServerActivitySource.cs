namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// Where the busy-server rule gets the two numbers it decides on.
/// </summary>
/// <remarks>
/// A seam because the answer comes from the server's session manager, which is a
/// thing no test in this repository has. Everything on the far side of this is one
/// call into the host; everything on this side is a rule with numbers in front of
/// it, which is where the reasoning that matters lives.
///
/// It has to be cheap, and that is a requirement rather than an observation. The
/// rule is asked once per item, so a source that queried anything expensive would
/// turn a limit that exists to protect playback into a cost paid against playback.
/// The session manager answers this from state it already holds.
///
/// It is asked at the moment an item would START and never during one. An item
/// already running is not stopped because somebody pressed play, because the work
/// done so far would be thrown away and the person watching would be no better off
/// for it.
/// </remarks>
public interface IServerActivitySource
{
    /// <summary>
    /// What the server is doing for somebody else right now.
    /// </summary>
    /// <returns>The sessions and transcodes in flight.</returns>
    ServerActivity Current();
}
