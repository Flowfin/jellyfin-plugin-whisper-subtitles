using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

namespace Jellyfin.Plugin.WhisperSubtitles.Fuzz;

/// <summary>
/// The parsers this repository fuzzes, one entry per target.
/// </summary>
/// <remarks>
/// Two of them are the surfaces that read bytes this plugin did not produce: the
/// output of a tool the operator chose, and the body an endpoint the operator
/// pointed at answered with. Both turn text into timestamps and both run
/// unattended on somebody else's server.
///
/// The third is not a parser. It exists so a run can show that this harness
/// reports a violation rather than passing over it, which is a thing a harness
/// has to prove about itself and cannot prove by finding nothing.
/// </remarks>
internal static class FuzzTargets
{
    /// <summary>
    /// The output of a whisper.cpp compatible tool, read line by line.
    /// </summary>
    private const string WhisperOutput = "whisper-output";

    /// <summary>
    /// The body a remote transcription endpoint answered with.
    /// </summary>
    private const string TranscriptionResponse = "transcription-response";

    /// <summary>
    /// The target that exists to be reported.
    /// </summary>
    private const string PropertyBite = "property-bite";

    /// <summary>
    /// Gets every target, by the name the workflow and the corpus directory use.
    /// </summary>
    internal static IReadOnlyDictionary<string, Action<byte[]>> All { get; }
        = new Dictionary<string, Action<byte[]>>(StringComparer.Ordinal)
        {
            [WhisperOutput] = ReadWhisperOutput,
            [TranscriptionResponse] = ReadTranscriptionResponse,
            [PropertyBite] = BiteOnPurpose,
        };

    /// <summary>
    /// Drives the reader of the local tool's output.
    /// </summary>
    /// <param name="input">The bytes the tool is imagined to have printed.</param>
    /// <remarks>
    /// Decoded with replacement rather than strictly, because that is what the
    /// caller does: the backend reads the child's standard output through the
    /// framework's decoder, which substitutes rather than throws. A harness that
    /// threw on the first invalid sequence would spend the run proving the
    /// decoder's behaviour instead of the reader's.
    /// </remarks>
    private static void ReadWhisperOutput(byte[] input)
    {
        var text = new UTF8Encoding(false, false).GetString(input);
        var reader = new WhisperOutputReader();

        var before = GC.GetAllocatedBytesForCurrentThread();

        foreach (var line in text.Split('\n'))
        {
            if (!reader.TryAccept(line.TrimEnd('\r'), out var problem))
            {
                // A refusal that says nothing leaves an operator with a failed run
                // and no sentence to act on, which is the failure the reason
                // messages exist against.
                if (string.IsNullOrWhiteSpace(problem))
                {
                    throw new PropertyViolatedException(
                        WhisperOutput + ": a line was refused and nothing was said about why.");
                }

                break;
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        SegmentProperties.Hold(reader.Segments, WhisperOutput);
        SegmentProperties.AllocationFollowsTheInput(allocated, input.Length, WhisperOutput);
    }

    /// <summary>
    /// Drives the reader of a remote endpoint's answer.
    /// </summary>
    /// <param name="input">The bytes the endpoint is imagined to have sent.</param>
    private static void ReadTranscriptionResponse(byte[] input)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();

        var read = TranscriptionResponseReader.TryRead(
            input,
            requestedLanguage: null,
            out var segments,
            out var language,
            out var problem);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        if (read)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new PropertyViolatedException(
                    TranscriptionResponse + ": the answer was read and the language it is in was left empty.");
            }

            SegmentProperties.Hold(segments, TranscriptionResponse);
        }
        else if (string.IsNullOrWhiteSpace(problem))
        {
            throw new PropertyViolatedException(
                TranscriptionResponse + ": the answer was refused and nothing was said about why.");
        }

        SegmentProperties.AllocationFollowsTheInput(allocated, input.Length, TranscriptionResponse);
    }

    /// <summary>
    /// Builds a segment straight out of the input and holds it to the same
    /// properties, with no parser in between.
    /// </summary>
    /// <param name="input">A start and an end, in seconds, written as text and separated by a space.</param>
    /// <remarks>
    /// This is how the harness is shown to report rather than to pass. It answers
    /// two questions a run of the two parsers cannot answer about itself. Under
    /// the replay, an input whose end is before its start has to come back
    /// reported, so a finding that never left the process would be caught. Under
    /// the fuzzer, a clean input has to be MUTATED into a reported one inside the
    /// first minute, which is the whole chain: the instrumentation, the crash
    /// handler, and the directory the finding is kept in. A run of the parsers
    /// that found nothing means nothing until that has happened.
    ///
    /// Text rather than packed numbers, so the seeds are seeds a person can read
    /// and a clone cannot mangle, and so a fuzzer flipping one digit lands on the
    /// case this exists for.
    /// </remarks>
    private static void BiteOnPurpose(byte[] input)
    {
        var text = new UTF8Encoding(false, false).GetString(input);
        var fields = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (fields.Length < 2
            || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
            || !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end)
            || !double.IsFinite(start)
            || !double.IsFinite(end))
        {
            return;
        }

        // Beyond this a TimeSpan cannot be built at all, and the property is the
        // same one: a time nothing in a library reaches. Raised here rather than
        // left to overflow, so the finding names the property instead of naming
        // the framework.
        const double Buildable = 1e12;

        if (Math.Abs(start) > Buildable || Math.Abs(end) > Buildable)
        {
            throw new PropertyViolatedException(
                PropertyBite + ": a segment is timed past anything a library holds.");
        }

        SegmentProperties.Hold(
            new[] { new TimedSegment(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), string.Empty) },
            PropertyBite);
    }
}
