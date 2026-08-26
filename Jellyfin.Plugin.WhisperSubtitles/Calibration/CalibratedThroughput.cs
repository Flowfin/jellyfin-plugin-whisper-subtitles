using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Calibration;

/// <summary>
/// A throughput factor together with the settings it was measured under and the
/// moment it was measured.
/// </summary>
/// <remarks>
/// <see cref="ThroughputFactor"/> deliberately carries neither, and this is the
/// type that puts the three together. The separation is the point: the factor is
/// arithmetic over durations and is right about the items it was folded from
/// whatever anybody does with it, and everything that can make it a lie is about
/// the circumstances, which live here.
///
/// The moment is a parameter rather than a reading of the clock. This plugin
/// judges nothing by the wall clock on its own, so a caller hands in the moment
/// it is recording, and a test states one instead of racing one.
/// </remarks>
public sealed class CalibratedThroughput
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalibratedThroughput"/> class.
    /// </summary>
    /// <param name="key">The settings it was measured under.</param>
    /// <param name="factor">What was measured.</param>
    /// <param name="measuredAt">When the newest item in it was folded in.</param>
    public CalibratedThroughput(CalibrationKey key, ThroughputFactor factor, DateTimeOffset measuredAt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factor);

        Key = key;
        Factor = factor;
        MeasuredAt = measuredAt;
    }

    /// <summary>
    /// Gets the settings it was measured under.
    /// </summary>
    public CalibrationKey Key { get; }

    /// <summary>
    /// Gets what was measured.
    /// </summary>
    public ThroughputFactor Factor { get; }

    /// <summary>
    /// Gets when the newest item in it was folded in.
    /// </summary>
    /// <remarks>
    /// The newest rather than the first, because what a reader wants to know is
    /// how old the evidence is, and a measurement refined an hour ago is not a
    /// month old because it started a month ago.
    /// </remarks>
    public DateTimeOffset MeasuredAt { get; }

    /// <summary>
    /// The same measurement with one more completed item folded into it.
    /// </summary>
    /// <param name="audioDuration">How much audio that item held.</param>
    /// <param name="processingTime">How long transcribing it took.</param>
    /// <param name="at">When it finished.</param>
    /// <returns>The refined measurement.</returns>
    /// <remarks>
    /// The key does not move. An item transcribed under different settings is not
    /// this measurement refined; it is a different measurement, and the ledger is
    /// where that distinction is enforced rather than here, because this type has
    /// only ever been told one key.
    /// </remarks>
    public CalibratedThroughput And(TimeSpan audioDuration, TimeSpan processingTime, DateTimeOffset at) =>
        new(Key, Factor.And(audioDuration, processingTime), at);
}
