using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Calibration;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Selection;

namespace Jellyfin.Plugin.WhisperSubtitles.Estimation;

/// <summary>
/// Works out what a run would cost, and transcribes nothing on the way there.
/// </summary>
/// <remarks>
/// THE BACKEND IS HELD AND NEVER ASKED TO TRANSCRIBE. It is a parameter because
/// the report is about the backend a run would use and has to name it, and
/// because a dry run that was never handed one could not be shown to have left it
/// alone. Holding it is what makes
/// <c>DryRunTests.A_dry_run_asks_the_backend_for_no_transcription</c> a claim
/// about this code rather than about its signature.
///
/// <see cref="ITranscriptionBackend.EstimateCost"/> IS DELIBERATELY NOT CALLED,
/// and this is the part to read before adding it back. Both configured backends
/// answer that with a range built from the media duration alone, and both say in
/// their own remarks that the range is a placeholder. A caller cannot tell a
/// placeholder from a calibrated range by looking at the two times it gets, so
/// showing it here would put a number in front of an operator that means nothing
/// and looks exactly like one that does. The measurement is asked for instead,
/// and where there is none the estimate refuses rather than falling back to the
/// placeholder.
///
/// NOTHING HERE READS A DISK, A CLOCK OR A NETWORK. The moment a measurement was
/// taken travels inside the measurement, the audio figure is arithmetic over
/// durations, and the model figure is a table lookup over a file name. A dry run
/// that reached any of the three would be doing a piece of the run in order to
/// say what the run would cost.
/// </remarks>
public static class DryRun
{
    /// <summary>
    /// Selects the work and reports what it would cost.
    /// </summary>
    /// <param name="items">Everything the run could consider.</param>
    /// <param name="options">What the selection accepts.</param>
    /// <param name="settings">The settings a run would use.</param>
    /// <param name="backend">The backend a run would use, which is not asked to transcribe.</param>
    /// <param name="model">The model that backend would be given.</param>
    /// <param name="calibration">Where a measurement of this server's throughput is held.</param>
    /// <returns>The report.</returns>
    public static DryRunReport Perform(
        IReadOnlyList<ItemDescription> items,
        SelectionOptions options,
        SettingsInForce settings,
        ITranscriptionBackend backend,
        string model,
        CalibrationLedger calibration)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(calibration);

        var selected = ItemSelection.Select(items, options);

        var key = new CalibrationKey(settings.Backend, model, settings.ThreadsPerItem);

        return new DryRunReport(
            settings.Backend,
            model,
            selected.Candidates.Count,
            selected.TotalDuration,
            EstimateFor(calibration, key, selected.TotalDuration),
            ModelMemory.SentenceFor(settings.Backend, model),
            TemporaryAudioPeak.BytesFor(selected.Candidates, settings.ItemsAtOnce),
            settings.ItemsAtOnce,
            settings.ThreadsPerItem);
    }

    private static DryRunEstimate EstimateFor(
        CalibrationLedger calibration,
        CalibrationKey key,
        TimeSpan totalDuration)
    {
        var measurement = calibration.For(key);

        if (measurement is not null)
        {
            return DryRunEstimate.From(measurement, totalDuration);
        }

        return calibration.HasAMeasurement
            ? DryRunEstimate.BecauseTheSettingsMoved()
            : DryRunEstimate.BecauseNothingIsMeasured();
    }
}
