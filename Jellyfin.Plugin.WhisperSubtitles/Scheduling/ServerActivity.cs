using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Scheduling;

/// <summary>
/// What the server is doing for somebody else at the moment an item would start.
/// </summary>
/// <remarks>
/// Two numbers rather than a verdict, because the definition of busy is a rule an
/// operator can read and argue with rather than something a source decides on its
/// own. A source that returned a boolean would be a second place the rule lived,
/// and the two would drift the first time either was changed.
///
/// Playback sessions and transcodes are counted apart because they are different
/// costs on the same machine. A direct-played stream is disk and network; a
/// transcode is the processors a transcription also wants. Counting them into one
/// number would make the rule unable to say which of the two it is answering, and
/// the rule as decided answers both.
/// </remarks>
public readonly struct ServerActivity : IEquatable<ServerActivity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerActivity"/> struct.
    /// </summary>
    /// <param name="playbackSessions">Sessions playing something right now.</param>
    /// <param name="transcodes">Transcodes running right now.</param>
    public ServerActivity(int playbackSessions, int transcodes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(playbackSessions);
        ArgumentOutOfRangeException.ThrowIfNegative(transcodes);

        PlaybackSessions = playbackSessions;
        Transcodes = transcodes;
    }

    /// <summary>
    /// Gets the number of sessions playing something right now.
    /// </summary>
    public int PlaybackSessions { get; }

    /// <summary>
    /// Gets the number of transcodes running right now.
    /// </summary>
    public int Transcodes { get; }

    /// <summary>
    /// Gets a server doing nothing for anybody.
    /// </summary>
    public static ServerActivity Idle => new(0, 0);

    /// <summary>
    /// Whether two readings are the same reading.
    /// </summary>
    /// <param name="left">One reading.</param>
    /// <param name="right">The other.</param>
    /// <returns><see langword="true"/> where both counts agree.</returns>
    public static bool operator ==(ServerActivity left, ServerActivity right) => left.Equals(right);

    /// <summary>
    /// Whether two readings differ.
    /// </summary>
    /// <param name="left">One reading.</param>
    /// <param name="right">The other.</param>
    /// <returns><see langword="true"/> where either count differs.</returns>
    public static bool operator !=(ServerActivity left, ServerActivity right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(ServerActivity other) =>
        PlaybackSessions == other.PlaybackSessions && Transcodes == other.Transcodes;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ServerActivity other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PlaybackSessions, Transcodes);
}
