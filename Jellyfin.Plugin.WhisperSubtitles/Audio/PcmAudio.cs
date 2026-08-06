using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// The one audio format this plugin extracts, and what a length of it costs on
/// disk.
/// </summary>
/// <remarks>
/// Sixteen kilohertz, one channel, sixteen bits per sample. It is not a
/// preference. The whisper.cpp command line tool accepts nothing else, and the
/// remote endpoints accept more but transcode internally, so sending them
/// anything larger spends bandwidth and their processor to arrive at this.
///
/// The numbers matter beyond the arguments because this format has no
/// compression: the size of the extracted file is a function of the length of
/// the media and nothing else, which is what lets the ceiling below be decided
/// before anything is written rather than discovered when a disk fills.
/// </remarks>
public static class PcmAudio
{
    /// <summary>
    /// Samples per second.
    /// </summary>
    public const int SampleRate = 16000;

    /// <summary>
    /// Channels, which is one, because a transcription of a stereo recording is
    /// the same transcription and twice the bytes.
    /// </summary>
    public const int Channels = 1;

    /// <summary>
    /// Bits per sample.
    /// </summary>
    public const int BitsPerSample = 16;

    /// <summary>
    /// Bytes one second of this format occupies.
    /// </summary>
    public const int BytesPerSecond = SampleRate * Channels * (BitsPerSample / 8);

    /// <summary>
    /// The bytes a WAV file spends before its first sample.
    /// </summary>
    /// <remarks>
    /// The canonical header. An encoder may write more than this, which is why
    /// the estimate below is described as a floor rather than as the size.
    /// </remarks>
    public const int HeaderBytes = 44;

    /// <summary>
    /// The ceiling this plugin holds unless an operator sets another.
    /// </summary>
    /// <remarks>
    /// Two gibibytes, which is a little over eighteen hours of this format. The
    /// number comes from the format rather than from taste: a WAV file carries
    /// its sizes in 32-bit fields, so four gibibytes is a wall the container
    /// itself hits, and half of it leaves room for an encoder that writes a
    /// larger header than the canonical one while still refusing the case this
    /// exists for.
    ///
    /// What it exists for is an item whose length nobody expected, on a server
    /// whose disk nobody was watching. Eighteen hours of audio is not a film, and
    /// an operator who genuinely has one says so rather than finding out when the
    /// disk is full.
    /// </remarks>
    public const long DefaultCeilingBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// The smallest this format can be for a given length of media.
    /// </summary>
    /// <param name="duration">How long the media is.</param>
    /// <returns>Bytes.</returns>
    /// <remarks>
    /// A floor and not a prediction. An encoder that writes a longer header, or
    /// rounds a partial sample up, produces a slightly larger file, and nothing
    /// here depends on the difference: this answers whether a length is obviously
    /// too large before anything runs, and the file that was actually produced is
    /// measured afterwards.
    /// </remarks>
    public static long SmallestSizeFor(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        return HeaderBytes + ((long)duration.TotalSeconds * BytesPerSecond);
    }
}
