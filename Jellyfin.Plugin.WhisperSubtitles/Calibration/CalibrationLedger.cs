using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Calibration;

/// <summary>
/// Holds the throughput measurement in force, and throws it away the moment the
/// settings it was taken under stop being the settings a run would use.
/// </summary>
/// <remarks>
/// The failure this exists against is an estimate that is confidently wrong. A
/// factor measured with the tiny model on eight threads, reused after somebody
/// switched to large on two, says a two hour film will take twenty minutes and it
/// will take hours. That is worse than having no estimate, because an operator who
/// is told nothing waits and an operator who is told twenty minutes plans around
/// it.
///
/// So a measurement is asked for BY key and answers only to its own. There is no
/// call that returns whatever is held.
///
/// IT DISCARDS RATHER THAN KEEPING ONE PER KEY, and that is a decision rather than
/// the obvious shape. Keeping a measurement per key would mean an operator who
/// tried the large model and went back to tiny would get their old tiny factor
/// back instead of measuring again, which is strictly more useful and is what a
/// cache would do. It is not what #38 asks for: that issue says a settings change
/// INVALIDATES the factor, and the two differ on a case that matters, which is a
/// key that comes back after the machine underneath it has changed. A kept
/// measurement has no way to notice that; a discarded one is measured again. What
/// this costs is a re-measurement after a round trip, and that is the trade the
/// literal reading makes. Whether the cache is wanted instead belongs in #38.
///
/// NOTHING HERE SURVIVES A RESTART. This is memory, like <c>AttemptLedger</c>, and
/// no type in this plugin writes to where the server puts plugin data. That is not
/// an oversight of this type: the store a factor would persist in is waiting behind
/// no open issue at all, which #38 records and `docs/limits.md` states about the
/// same absence from the other direction.
/// </remarks>
public sealed class CalibrationLedger
{
    private CalibratedThroughput? _held;

    /// <summary>
    /// Gets a value indicating whether any measurement is held at all.
    /// </summary>
    /// <remarks>
    /// For a surface reporting that nothing has been measured yet, which is a
    /// different sentence from a measurement that does not apply. Answering that
    /// question is not the same as handing the measurement over, so this says
    /// whether and <see cref="For"/> says what.
    /// </remarks>
    public bool HasAMeasurement => _held is not null;

    /// <summary>
    /// The measurement taken under these settings, or nothing.
    /// </summary>
    /// <param name="key">The settings a run would use.</param>
    /// <returns>The measurement, or null where none was taken under them.</returns>
    public CalibratedThroughput? For(CalibrationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _held is not null && _held.Key.Equals(key) ? _held : null;
    }

    /// <summary>
    /// Folds one completed item into the measurement for these settings.
    /// </summary>
    /// <param name="key">The settings that item was transcribed under.</param>
    /// <param name="audioDuration">How much audio it held.</param>
    /// <param name="processingTime">How long transcribing it took.</param>
    /// <param name="at">When it finished.</param>
    /// <returns>The measurement now in force.</returns>
    /// <remarks>
    /// One entry point for the first item and for every one after it. A separate
    /// "start a calibration" call would be a second place that decides what
    /// happens when the key has moved, and the two would disagree the first time
    /// somebody changed a setting mid-run.
    ///
    /// An item transcribed under a key other than the one held replaces the
    /// measurement rather than joining it. Folding it in would average two
    /// quantities that are not the same quantity, and the result would be a number
    /// that describes no configuration the operator has ever run.
    /// </remarks>
    public CalibratedThroughput Record(
        CalibrationKey key,
        TimeSpan audioDuration,
        TimeSpan processingTime,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(key);

        var held = For(key);

        _held = held is null
            ? new CalibratedThroughput(key, ThroughputFactor.Measured(audioDuration, processingTime), at)
            : held.And(audioDuration, processingTime, at);

        return _held;
    }

    /// <summary>
    /// Throws away whatever is held, because the settings a run would use are no
    /// longer the ones it was measured under.
    /// </summary>
    /// <param name="key">The settings a run would use now.</param>
    /// <returns>True where a measurement was discarded.</returns>
    /// <remarks>
    /// Separate from <see cref="Record"/> because the two are told by different
    /// things. A run tells this ledger that an item finished; a configuration
    /// change tells it that everything it holds is about a machine nobody is
    /// running any more, and that happens with no item in sight. Without this the
    /// stale measurement would sit there until the next completed item, and every
    /// estimate read in between would be the confident lie above.
    /// </remarks>
    public bool InvalidateUnless(CalibrationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_held is null || _held.Key.Equals(key))
        {
            return false;
        }

        _held = null;

        return true;
    }
}
