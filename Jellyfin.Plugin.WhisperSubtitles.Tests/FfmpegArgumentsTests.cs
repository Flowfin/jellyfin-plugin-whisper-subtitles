using System;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The argument vector is a pure function of five values, and every assertion
/// here is about one element of it. A wrong flag in this list produces a file
/// that exists, is the right length and is the wrong thing, which no test
/// downstream of it can tell from the right thing.
/// </summary>
public class FfmpegArgumentsTests
{
    private const string Encoder = "/usr/lib/jellyfin-ffmpeg/ffmpeg";
    private const string Input = "/media/films/A Film (2001)/A Film.mkv";
    private const string Output = "/var/lib/jellyfin/plugins/whisper/tmp/abc.wav";

    [Fact]
    public void The_program_is_the_path_it_was_handed_and_never_one_this_plugin_went_looking_for()
    {
        // The whole rule about the media tool, in one assertion. There is no
        // search, no fallback name and no second candidate: what the server said
        // is what runs, and a change that introduced any of those would have to
        // move this line to pass.
        var invocation = FfmpegArguments.Build(Encoder, Input, 1, Output, 1024);

        Assert.Equal(Encoder, invocation.ExecutablePath);
    }

    [Fact]
    public void The_format_is_the_only_one_the_transcription_tools_accept()
    {
        var arguments = FfmpegArguments.Build(Encoder, Input, 1, Output, 1024).Arguments;

        Assert.Equal("pcm_s16le", After(arguments, "-acodec"));
        Assert.Equal("16000", After(arguments, "-ar"));
        Assert.Equal("1", After(arguments, "-ac"));
        Assert.Equal("wav", After(arguments, "-f"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void The_stream_is_mapped_by_its_index_inside_the_container(int index)
    {
        // Absolute and not counted among the audio streams alone. A film with one
        // video track and two audio tracks has its second audio stream at index 2,
        // and a mapping that counted only audio would take the first one.
        var arguments = FfmpegArguments.Build(Encoder, Input, index, Output, 1024).Arguments;

        Assert.Equal($"0:{index}", After(arguments, "-map"));
    }

    [Fact]
    public void Everything_that_is_not_the_chosen_stream_is_refused()
    {
        var arguments = FfmpegArguments.Build(Encoder, Input, 1, Output, 1024).Arguments;

        Assert.Contains("-vn", arguments);
        Assert.Contains("-sn", arguments);
        Assert.Contains("-dn", arguments);
    }

    [Fact]
    public void The_ceiling_is_handed_to_the_tool_rather_than_only_checked_afterwards()
    {
        // A check after the fact notices a file that grew too large. Only this
        // stops it growing, which is the difference between an item that fails and
        // a disk that fills.
        var arguments = FfmpegArguments.Build(Encoder, Input, 1, Output, 4096).Arguments;

        Assert.Equal("4096", After(arguments, "-fs"));
    }

    [Fact]
    public void The_tool_neither_reads_a_terminal_nor_fills_the_error_stream()
    {
        // Both are about running inside a server. There is nobody to type at it,
        // and the error stream is what a failure message is built from.
        var arguments = FfmpegArguments.Build(Encoder, Input, 1, Output, 1024).Arguments;

        Assert.Contains("-nostdin", arguments);
        Assert.Contains("-hide_banner", arguments);
        Assert.Equal("error", After(arguments, "-loglevel"));
    }

    [Fact]
    public void The_media_file_and_the_output_reach_the_tool_as_single_arguments()
    {
        // Both contain spaces and parentheses, and one of them came out of a media
        // library. A command line would put a quoting rule between what was meant
        // and what runs.
        var arguments = FfmpegArguments.Build(Encoder, Input, 1, Output, 1024).Arguments;

        Assert.Equal(Input, After(arguments, "-i"));
        Assert.Equal(Output, arguments[^1]);
    }

    [Theory]
    [InlineData("", Input, Output)]
    [InlineData(" ", Input, Output)]
    [InlineData(Encoder, "", Output)]
    [InlineData(Encoder, Input, "")]
    public void A_missing_path_is_refused_rather_than_turned_into_an_argument(
        string encoder,
        string input,
        string output)
    {
        Assert.ThrowsAny<ArgumentException>(() => FfmpegArguments.Build(encoder, input, 1, output, 1024));
    }

    [Fact]
    public void A_negative_index_or_an_impossible_ceiling_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FfmpegArguments.Build(Encoder, Input, -1, Output, 1024));
        Assert.Throws<ArgumentOutOfRangeException>(() => FfmpegArguments.Build(Encoder, Input, 1, Output, 0));
    }

    private static string After(System.Collections.Generic.IReadOnlyList<string> arguments, string flag)
    {
        var at = arguments.ToList().IndexOf(flag);

        Assert.True(at >= 0, $"the argument list carries no {flag}");
        Assert.True(at + 1 < arguments.Count, $"{flag} is the last argument and has no value");

        return arguments[at + 1];
    }
}
