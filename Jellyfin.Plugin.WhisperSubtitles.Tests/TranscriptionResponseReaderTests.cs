using System;
using System.Text;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The reader is the place untrusted bytes stop being bytes. Everything it is
/// given came from a machine this plugin knows nothing about, so what these are
/// about is what it refuses rather than what it accepts.
///
/// It is tested without an HTTP stack in the way on purpose: this is the shape
/// #82 points a fuzzer at, and a fuzzer that has to build a response message
/// first is a fuzzer testing the response message.
/// </summary>
public class TranscriptionResponseReaderTests
{
    [Fact]
    public void A_verbose_answer_reads_as_the_segments_it_carries()
    {
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""
                {
                  "language": "de",
                  "segments": [
                    { "start": 1.5, "end": 3, "text": "  Was jemand gesagt hat.  " }
                  ]
                }
                """),
            requestedLanguage: null,
            out var segments,
            out var language,
            out var problem);

        Assert.True(read, problem);
        Assert.Equal("de", language);
        var only = Assert.Single(segments);
        Assert.Equal(TimeSpan.FromSeconds(1.5), only.Start);
        Assert.Equal(TimeSpan.FromSeconds(3), only.End);
        Assert.Equal("Was jemand gesagt hat.", only.Text);
    }

    [Fact]
    public void Seconds_written_as_text_are_read_as_seconds()
    {
        // A spelling and not a claim about the audio. Endpoints that serialise the
        // numbers as strings exist, and refusing them would refuse a transcript that
        // is not in any way ambiguous.
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""{ "language": "en", "segments": [ { "start": "0", "end": "1.25", "text": "A line." } ] }"""),
            requestedLanguage: null,
            out var segments,
            out _,
            out var problem);

        Assert.True(read, problem);
        Assert.Equal(TimeSpan.FromSeconds(1.25), Assert.Single(segments).End);
    }

    [Fact]
    public void An_empty_transcription_is_read_as_one_rather_than_refused()
    {
        // An endpoint that heard nothing in a silent stretch answers with no
        // segments in the array, which is different from answering with no array.
        // Refusing this would quarantine every item that is genuinely silent.
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""{ "language": "en", "segments": [] }"""),
            requestedLanguage: null,
            out var segments,
            out var language,
            out var problem);

        Assert.True(read, problem);
        Assert.Empty(segments);
        Assert.Equal("en", language);
    }

    [Fact]
    public void The_requested_language_stands_in_when_the_endpoint_names_none()
    {
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""{ "segments": [ { "start": 0, "end": 1, "text": "A line." } ] }"""),
            requestedLanguage: "en",
            out _,
            out var language,
            out var problem);

        Assert.True(read, problem);
        Assert.Equal("en", language);
    }

    [Fact]
    public void An_answer_naming_no_language_where_none_was_requested_is_refused()
    {
        // Nothing is written under a language nobody stated. The alternative is a
        // file name carrying a guess, which every part of the server downstream then
        // believes.
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""{ "segments": [ { "start": 0, "end": 1, "text": "A line." } ] }"""),
            requestedLanguage: null,
            out _,
            out _,
            out var problem);

        Assert.False(read);
        Assert.Contains("no language", problem!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_language_is_passed_on_as_the_endpoint_spelled_it()
    {
        // One server answers "en" and another answers "english" for the same audio.
        // Mapping either onto what Jellyfin stores and what a file name has to say
        // is #33, and a reader that mapped here would be the place that decides a
        // file name.
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""{ "language": "english", "segments": [ { "start": 0, "end": 1, "text": "A line." } ] }"""),
            requestedLanguage: null,
            out _,
            out var language,
            out var problem);

        Assert.True(read, problem);
        Assert.Equal("english", language);
    }

    [Theory]
    [InlineData("", "not JSON")]
    [InlineData("{", "not JSON")]
    [InlineData("[]", "not an object")]
    [InlineData("\"a transcript\"", "not an object")]
    [InlineData("""{ "text": "A line somebody said." }""", "no segments")]
    [InlineData("""{ "segments": "none" }""", "no segments")]
    [InlineData("""{ "error": { "message": "no such model" } }""", "no such model")]
    [InlineData("""{ "error": "no such model" }""", "no such model")]
    [InlineData("""{ "segments": [ 1, 2 ] }""", "not an object")]
    [InlineData("""{ "segments": [ { "end": 1, "text": "A" } ] }""", "start time")]
    [InlineData("""{ "segments": [ { "start": 0, "text": "A" } ] }""", "end time")]
    [InlineData("""{ "segments": [ { "start": "soon", "end": 1, "text": "A" } ] }""", "start time")]
    [InlineData("""{ "segments": [ { "start": -1, "end": 1, "text": "A" } ] }""", "before the start")]
    [InlineData("""{ "segments": [ { "start": 3, "end": 1, "text": "A" } ] }""", "ends before it starts")]
    [InlineData("""{ "segments": [ { "start": 0, "end": 1 } ] }""", "no text")]
    [InlineData("""{ "segments": [ { "start": 0, "end": 1, "text": 7 } ] }""", "no text")]
    public void What_cannot_be_read_as_timed_segments_is_refused_with_a_reason(string body, string says)
    {
        var read = TranscriptionResponseReader.TryRead(
            Bytes(body),
            requestedLanguage: "en",
            out var segments,
            out _,
            out var problem);

        Assert.False(read);
        Assert.Empty(segments);
        Assert.NotNull(problem);
        Assert.Contains(says, problem!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_answer_yields_no_segments_even_when_the_ones_before_it_were_good()
    {
        // The whole response or none of it. A subtitle written from the readable
        // prefix of a broken answer is a file that looks complete and stops
        // mid-film.
        var read = TranscriptionResponseReader.TryRead(
            Bytes("""
                {
                  "language": "en",
                  "segments": [
                    { "start": 0, "end": 1, "text": "A line." },
                    { "start": 5, "end": 2, "text": "Another." }
                  ]
                }
                """),
            requestedLanguage: null,
            out var segments,
            out _,
            out var problem);

        Assert.False(read);
        Assert.Empty(segments);
        Assert.Contains("Segment 1", problem!, StringComparison.Ordinal);
    }

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
}
