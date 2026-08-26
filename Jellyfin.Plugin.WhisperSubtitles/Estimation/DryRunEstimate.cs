using System;
using System.Globalization;
using Jellyfin.Plugin.WhisperSubtitles.Calibration;

namespace Jellyfin.Plugin.WhisperSubtitles.Estimation;

/// <summary>
/// The wall-clock range a dry run offers for the selected work, or the sentence
/// it gives instead of a number.
/// </summary>
/// <remarks>
/// THE ABSENCE IS A STATE OF THIS TYPE AND NOT A ZERO. An estimate with no
/// measurement behind it and an estimate of no time at all are opposite facts,
/// and a type that answered both with two zero-length TimeSpans would let a
/// surface show "0 to 0" to an operator whose library is forty hours long. So a
/// caller asks <see cref="HasANumber"/> and gets either a range with its
/// provenance or a refusal with its reason, and there is no third shape.
///
/// EVERY NUMBER CARRIES WHAT IT WAS BUILT ON. The provenance sentence names the
/// settings the measurement was taken under, how much audio and how many items it
/// was folded from, and when the newest of those finished. That is what makes the
/// range arguable: an operator who reads "measured over four minutes of audio, one
/// item, an hour ago" knows exactly how much to trust it, and one who reads a bare
/// range does not.
///
/// TWO ABSENCES, NOT ONE. Nothing measured yet and something measured under other
/// settings are different facts with different repairs - the first is waiting for
/// a run to finish an item, the second is the operator having changed the model or
/// the thread count - and <see cref="CalibrationLedger"/> already keeps them
/// apart. Collapsing them here would throw that away at the last step.
/// </remarks>
public sealed class DryRunEstimate
{
    /// <summary>
    /// What an operator is told when no item has been transcribed at all yet.
    /// </summary>
    public const string NothingMeasuredYet =
        "No time is given: nothing has been transcribed on this server yet, so there is no measurement to build an estimate on.";

    /// <summary>
    /// What an operator is told when the measurement in hand was taken under other
    /// settings.
    /// </summary>
    public const string MeasuredUnderOtherSettings =
        "No time is given: the measurement in hand was taken under different settings, and a factor measured under other settings is not evidence about this run.";

    private DryRunEstimate(TimeSpan shortest, TimeSpan longest, string provenance)
    {
        Shortest = shortest;
        Longest = longest;
        Provenance = provenance;
    }

    private DryRunEstimate(string refusal)
    {
        Refusal = refusal;
    }

    /// <summary>
    /// Gets a value indicating whether there is a range to show.
    /// </summary>
    public bool HasANumber => Refusal is null;

    /// <summary>
    /// Gets the short end of the range, which means nothing unless
    /// <see cref="HasANumber"/>.
    /// </summary>
    public TimeSpan Shortest { get; }

    /// <summary>
    /// Gets the long end of the range, which means nothing unless
    /// <see cref="HasANumber"/>.
    /// </summary>
    public TimeSpan Longest { get; }

    /// <summary>
    /// Gets what the range was built on and when that was measured, or null where
    /// there is no range.
    /// </summary>
    public string? Provenance { get; }

    /// <summary>
    /// Gets why there is no range, or null where there is one.
    /// </summary>
    public string? Refusal { get; }

    /// <summary>
    /// A range built from a measurement taken under the settings this run would
    /// use.
    /// </summary>
    /// <param name="measurement">The measurement in force for those settings.</param>
    /// <param name="totalDuration">How much media the run would transcribe.</param>
    /// <returns>The range and its provenance.</returns>
    public static DryRunEstimate From(CalibratedThroughput measurement, TimeSpan totalDuration)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalDuration, TimeSpan.Zero);

        var factor = measurement.Factor;

        return new DryRunEstimate(
            factor.ExpectAtBest(totalDuration),
            factor.ExpectAtWorst(totalDuration),
            string.Format(
                CultureInfo.InvariantCulture,
                "Measured on this server under {0}, over {1} item(s) and {2} of audio, most recently at {3}.",
                measurement.Key,
                factor.Items,
                Spell(factor.AudioMeasured),
                measurement.MeasuredAt.ToString("u", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// No range, because nothing has been transcribed yet.
    /// </summary>
    /// <returns>The refusal.</returns>
    public static DryRunEstimate BecauseNothingIsMeasured() => new(NothingMeasuredYet);

    /// <summary>
    /// No range, because what is held was measured under other settings.
    /// </summary>
    /// <returns>The refusal.</returns>
    public static DryRunEstimate BecauseTheSettingsMoved() => new(MeasuredUnderOtherSettings);

    private static string Spell(TimeSpan duration) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:0.#} hour(s)",
        duration.TotalHours);
}
