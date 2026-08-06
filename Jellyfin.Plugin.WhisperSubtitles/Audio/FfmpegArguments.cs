using System;
using System.Globalization;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// The argument vector the media tool is handed, and nothing that runs it.
/// </summary>
/// <remarks>
/// Separated from the extractor so the thing most likely to be wrong is the
/// thing easiest to read. An argument list is a sequence a person has to check
/// element by element, and checking it inside a test that also has to arrange a
/// process, a temporary directory and a cancellation token is how a wrong flag
/// survives review.
///
/// It is a vector and never a command line, for the reason
/// <see cref="ProcessInvocation"/> gives: two of the elements here come from a
/// media library and one from an operator, so a single string would put a
/// quoting rule between what was meant and what runs.
/// </remarks>
public static class FfmpegArguments
{
    /// <summary>
    /// Builds the invocation that extracts one audio stream.
    /// </summary>
    /// <param name="encoderPath">The media tool, which is the server's own and never one this plugin found.</param>
    /// <param name="inputPath">The media file to read.</param>
    /// <param name="streamIndex">The index inside that file of the stream to take.</param>
    /// <param name="outputPath">Where to write the extracted audio.</param>
    /// <param name="ceilingBytes">The most the output may reach before the tool stops writing.</param>
    /// <returns>The program and its arguments.</returns>
    public static ProcessInvocation Build(
        string encoderPath,
        string inputPath,
        int streamIndex,
        string outputPath,
        long ceilingBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentOutOfRangeException.ThrowIfNegative(streamIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(ceilingBytes, 1);

        return new ProcessInvocation(
            encoderPath,
            [
                // Nothing here reads a terminal. Without this the tool inherits a
                // standard input it may block on, inside a server process that has
                // nobody to type at it.
                "-nostdin",

                // The banner and the progress table are several hundred lines per
                // item on the error stream, and the error stream is what a failure
                // message is built from. At this level what is left is the sentence
                // that says why it stopped.
                "-hide_banner",
                "-loglevel", "error",

                // The output name is unique per attempt, so this is not a licence
                // to overwrite anything an operator has. It covers the file this
                // plugin itself left behind when a process died between creating
                // the name and writing to it.
                "-y",

                "-i", inputPath,

                // The absolute stream index, which is what the server reports.
                "-map", string.Create(CultureInfo.InvariantCulture, $"0:{streamIndex}"),

                // Everything that is not this stream is refused rather than left to
                // a default. A container with attached cover art turns into a video
                // stream in the output otherwise, and a WAV file carrying a picture
                // is not a thing a transcription tool has to be asked to read.
                "-vn", "-sn", "-dn",

                "-acodec", "pcm_s16le",
                "-ar", string.Create(CultureInfo.InvariantCulture, $"{PcmAudio.SampleRate}"),
                "-ac", string.Create(CultureInfo.InvariantCulture, $"{PcmAudio.Channels}"),

                // The ceiling, held by the tool itself. A check after the fact
                // notices a file that grew too large; only this stops it growing.
                // The extractor still measures the result, because stopping at the
                // ceiling produces a truncated file that would otherwise be
                // transcribed as though it were the whole item.
                "-fs", ceilingBytes.ToString(CultureInfo.InvariantCulture),

                "-f", "wav",
                outputPath
            ]);
    }
}
