using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Unicode;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

/// <summary>
/// Turns the bytes an endpoint answered with into timed segments, or says why it
/// could not.
/// </summary>
/// <remarks>
/// Separate from the backend and pure, because this is the half that reads
/// untrusted bytes. Everything it is given came from a machine this plugin knows
/// nothing about, so it is written to be driven by a fuzzer without an HTTP stack
/// in the way, which is #82, and it is on the pure logic list #47 measures.
///
/// It refuses rather than repairs. A response missing its segments, carrying a
/// segment that ends before it starts, or holding a plain transcript with no
/// timings is a response this plugin cannot write a subtitle from, and a reader
/// that filled in the gaps would be inventing the timings a viewer then reads as
/// measured.
/// </remarks>
public static class TranscriptionResponseReader
{
    /// <summary>
    /// The response format this plugin asks the endpoint for.
    /// </summary>
    /// <remarks>
    /// The plain format returns one string, and a subtitle needs times. So this is
    /// not a preference: the request asks for the verbose form and this reader
    /// requires it, and the two constants are the same one.
    /// </remarks>
    public const string RequiredResponseFormat = "verbose_json";

    /// <summary>
    /// The furthest into the media a segment may be timed, in seconds.
    /// </summary>
    /// <remarks>
    /// Ten thousand hours, which is over a year of continuous playback. It is not
    /// a preference about long media: without it a finite number this reader
    /// accepted became a <c>TimeSpan</c> the framework refused to build, so an
    /// endpoint answering with <c>1e300</c> stopped the reader with an overflow
    /// instead of being told its answer could not be read. This reader answers
    /// rather than throws, and a number nothing bounded was the one way through
    /// that.
    ///
    /// The bound is the one the other reader of untrusted bytes already holds
    /// rather than a second idea about what a time can be:
    /// <see cref="Local.WhisperOutputReader"/> reads at most four digits of hours
    /// and says at that bound that media longer than a hundred hours is absurd
    /// rather than impossible.
    /// </remarks>
    public const double SecondsCeiling = 10000d * 3600d;

    /// <summary>
    /// Reads an endpoint's answer.
    /// </summary>
    /// <param name="json">The bytes the endpoint sent.</param>
    /// <param name="requestedLanguage">The language the request named, or null when it asked the endpoint to detect one.</param>
    /// <param name="segments">The segments, in the order the endpoint gave them.</param>
    /// <param name="language">The language of those segments, as the endpoint spells it.</param>
    /// <param name="problem">What is wrong with the response, when something is.</param>
    /// <returns>Whether the response could be read.</returns>
    /// <remarks>
    /// The language that comes back is the endpoint's own spelling and is not
    /// translated here. One server answers <c>en</c> and another answers
    /// <c>english</c> for the same audio, and mapping either onto what Jellyfin
    /// stores and what a file name has to say is #33. A reader that guessed would
    /// be the place that decides a file name, which is not this one.
    /// </remarks>
    public static bool TryRead(
        ReadOnlyMemory<byte> json,
        string? requestedLanguage,
        out IReadOnlyList<TimedSegment> segments,
        out string? language,
        out string? problem)
    {
        segments = Array.Empty<TimedSegment>();
        language = null;
        problem = null;

        // Before anything reads a string out of it. JSON is UTF-8 by definition
        // and the document parser does not check what is inside a string, so a
        // body carrying one byte that is not valid UTF-8 parses and then throws
        // at the moment somebody asks for the text. That is three places here,
        // the segment text, the language and the error message, and it is not a
        // shape a person would think to send: it is what an endpoint answering
        // in its own eight-bit encoding sends every time.
        //
        // Refused whole rather than repaired per string. A reader substituting a
        // replacement character would write a subtitle carrying it and call the
        // answer read.
        if (!Utf8.IsValid(json.Span))
        {
            problem = "The endpoint's answer is not valid UTF-8, which JSON has to be, so nothing in it can be read as text.";

            return false;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException malformed)
        {
            problem = "The endpoint's answer is not JSON. " + malformed.Message;

            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problem = "The endpoint's answer is JSON but not an object, so it carries no transcription.";

                return false;
            }

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                // An endpoint that answers 200 with an error object is a real shape,
                // and reading it as an empty transcription would write an empty
                // subtitle over a perfectly good item.
                problem = "The endpoint answered with an error rather than a transcription: " + Describe(error);

                return false;
            }

            if (!document.RootElement.TryGetProperty("segments", out var array)
                || array.ValueKind != JsonValueKind.Array)
            {
                problem = "The endpoint's answer carries no segments array, so it has no timings. "
                    + "This plugin asks for the "
                    + RequiredResponseFormat
                    + " response format, and an endpoint that ignores that returns a transcript without times.";

                return false;
            }

            var read = new List<TimedSegment>();

            foreach (var element in array.EnumerateArray())
            {
                if (!TryReadSegment(element, read.Count, out var segment, out problem))
                {
                    return false;
                }

                read.Add(segment!);
            }

            language = ReadLanguage(document.RootElement) ?? requestedLanguage;

            if (string.IsNullOrWhiteSpace(language))
            {
                problem = "The endpoint reported no language and the request named none, so the transcription is in a language nothing has stated.";

                return false;
            }

            segments = read;

            return true;
        }
    }

    private static bool TryReadSegment(
        JsonElement element,
        int index,
        out TimedSegment? segment,
        out string? problem)
    {
        segment = null;
        problem = null;

        var at = index.ToString(CultureInfo.InvariantCulture);

        if (element.ValueKind != JsonValueKind.Object)
        {
            problem = "Segment " + at + " is not an object.";

            return false;
        }

        if (!TryReadSeconds(element, "start", out var start))
        {
            problem = "Segment " + at + " has no readable start time in seconds.";

            return false;
        }

        if (!TryReadSeconds(element, "end", out var end))
        {
            problem = "Segment " + at + " has no readable end time in seconds.";

            return false;
        }

        if (start < 0 || end < 0)
        {
            problem = "Segment " + at + " is timed before the start of the media.";

            return false;
        }

        if (end < start)
        {
            problem = "Segment " + at + " ends before it starts.";

            return false;
        }

        if (start > SecondsCeiling || end > SecondsCeiling)
        {
            problem = "Segment " + at + " is timed past anything a library holds.";

            return false;
        }

        if (!element.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
        {
            problem = "Segment " + at + " carries no text.";

            return false;
        }

        var spoken = text.GetString() ?? string.Empty;

        segment = new TimedSegment(
            TimeSpan.FromSeconds(start),
            TimeSpan.FromSeconds(end),
            spoken.Trim());

        return true;
    }

    /// <remarks>
    /// A number, and a string holding a number is accepted too. Endpoints that
    /// serialise the seconds as text exist, the value is unambiguous either way,
    /// and this is a spelling rather than a claim about the audio.
    /// </remarks>
    private static bool TryReadSeconds(JsonElement element, string name, out double seconds)
    {
        seconds = 0;

        if (!element.TryGetProperty(name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetDouble(out seconds) && double.IsFinite(seconds);
        }

        return value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
            && double.IsFinite(seconds);
    }

    private static string? ReadLanguage(JsonElement root) =>
        root.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.String
            ? language.GetString()?.Trim()
            : null;

    private static string Describe(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
        {
            return error.GetString() ?? string.Empty;
        }

        if (error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String)
        {
            return message.GetString() ?? string.Empty;
        }

        return "it said nothing this plugin could read about why.";
    }
}
