using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Calibration;

/// <summary>
/// How much wall-clock time one second of audio has cost, kept as the whole
/// record of what has been transcribed rather than as the last answer.
/// </summary>
/// <remarks>
/// The number an estimate is built on is a ratio of processing time to audio
/// duration, and it belongs to a machine, a backend, a model and a thread count
/// rather than to this plugin. So it is measured here instead of being written
/// down, and it is refined from items a run has already finished, which costs an
/// operator nothing.
///
/// What this type decides is the refining rule, and there is one way to get it
/// wrong that is worth naming. A rule that lets the newest item carry the answer
/// - taking its ratio, or an average weighted so heavily towards recent items
/// that it amounts to the same - produces a factor that swings with whatever was
/// transcribed last. One dialogue-free film, one item that queued behind a
/// transcode, and the estimate an operator reads before starting a run is about
/// that item rather than about their library.
///
/// The rule is instead the ratio of everything measured so far: total processing
/// time over total audio duration. Two things follow from it, and both are
/// asserted rather than described. Folding the same items in a different order
/// gives the same factor, because a sum does not care about order, so there is no
/// position in the sequence that owns the answer. And the weight a new item
/// carries is its own audio duration over the audio duration of everything
/// measured, so the more that has been measured the less any one item moves it,
/// which is convergence rather than a promise of it.
///
/// The weighting is by audio duration and not by item count on purpose. A three
/// minute clip and a three hour film are not two equal readings of the same
/// quantity: the film is sixty times as much evidence about how long a second of
/// audio takes, and counting them equally would let a handful of short items
/// decide an estimate about a library of long ones.
///
/// What this deliberately does not carry is the settings it was measured under.
/// A factor measured at one thread count is not evidence about another, so the key
/// a stored factor lives under is <see cref="CalibrationKey"/> and the invalidation
/// that throws it away when the model or the thread count changes is
/// <see cref="CalibrationLedger"/>. The separation is kept rather than merged: this
/// type is arithmetic over durations and is right about the items it was folded
/// from whatever anybody does with it, and everything that can make it a lie is
/// about the circumstances.
/// </remarks>
public sealed class ThroughputFactor
{
    private readonly TimeSpan _audio;
    private readonly TimeSpan _work;
    private readonly double _fastest;
    private readonly double _slowest;

    private ThroughputFactor(TimeSpan audio, TimeSpan work, int items, double fastest, double slowest)
    {
        _audio = audio;
        _work = work;
        Items = items;
        _fastest = fastest;
        _slowest = slowest;
    }

    /// <summary>
    /// Gets how many seconds of wall-clock time one second of audio has cost.
    /// </summary>
    public double WorkPerSecondOfAudio => _work.Ticks / (double)_audio.Ticks;

    /// <summary>
    /// Gets the audio duration this factor was measured over.
    /// </summary>
    /// <remarks>
    /// This is what a reader needs to know how much the factor is worth. A ratio
    /// measured over four minutes of audio and one measured over forty hours are
    /// the same number and are not the same evidence.
    /// </remarks>
    public TimeSpan AudioMeasured => _audio;

    /// <summary>
    /// Gets how many completed items the factor is made of.
    /// </summary>
    public int Items { get; }

    /// <summary>
    /// Gets the smallest ratio any single measured item came in at.
    /// </summary>
    /// <remarks>
    /// THIS IS SPREAD AND NOT THE ANSWER, and keeping the two apart is what makes
    /// it safe to hold here. The remarks above argue at length that no single item
    /// may own the estimate, and nothing about that changes: the estimate is still
    /// the ratio of everything measured, and this pair is only ever the width
    /// around it.
    ///
    /// It exists because an estimate is asked for as a range, and a range whose
    /// two ends are the same number is a point estimate wearing a range's
    /// punctuation. What has actually been observed is the honest width to offer,
    /// so one measured item gives a range of zero width and says so through
    /// <see cref="Items"/>, and it widens as a library turns out to be more varied
    /// than the first item suggested.
    /// </remarks>
    public double FastestObserved => _fastest;

    /// <summary>
    /// Gets the largest ratio any single measured item came in at.
    /// </summary>
    /// <remarks>
    /// The other end of <see cref="FastestObserved"/>, and never below it.
    /// </remarks>
    public double SlowestObserved => _slowest;

    /// <summary>
    /// Starts a factor from one completed item.
    /// </summary>
    /// <param name="audioDuration">How long the audio was.</param>
    /// <param name="processingTime">How long transcribing it took.</param>
    /// <returns>A factor measured over that one item.</returns>
    public static ThroughputFactor Measured(TimeSpan audioDuration, TimeSpan processingTime)
    {
        Refuse(audioDuration, processingTime);

        var ratio = RatioOf(audioDuration, processingTime);

        return new ThroughputFactor(audioDuration, processingTime, 1, ratio, ratio);
    }

    /// <summary>
    /// Folds a completed item into the factor.
    /// </summary>
    /// <param name="audioDuration">How long the audio was.</param>
    /// <param name="processingTime">How long transcribing it took.</param>
    /// <returns>A factor measured over everything so far and this item as well.</returns>
    /// <remarks>
    /// A new instance rather than a mutation, so a factor handed to something
    /// that is estimating cannot change underneath it while a run folds items in.
    /// </remarks>
    public ThroughputFactor And(TimeSpan audioDuration, TimeSpan processingTime)
    {
        Refuse(audioDuration, processingTime);

        var ratio = RatioOf(audioDuration, processingTime);

        return new ThroughputFactor(
            _audio + audioDuration,
            _work + processingTime,
            Items + 1,
            Math.Min(_fastest, ratio),
            Math.Max(_slowest, ratio));
    }

    /// <summary>
    /// Says how long this much media is expected to take.
    /// </summary>
    /// <param name="mediaDuration">How long the media is.</param>
    /// <returns>The wall-clock time the factor expects it to cost.</returns>
    /// <remarks>
    /// Linear in the media duration, so a longer item never costs less than a
    /// shorter one. Both backends already promise that of their placeholder
    /// ranges and say it is the one property a caller may rely on, so a measured
    /// factor that dropped it would be a regression dressed as an improvement.
    ///
    /// A media duration long enough to overflow at this factor answers
    /// <see cref="TimeSpan.MaxValue"/> rather than throwing. The answer is wrong
    /// by then in a way no arithmetic fixes, and it is still the largest answer
    /// this can give, so the property above survives the edge instead of the edge
    /// becoming an exception a caller has to know about.
    /// </remarks>
    public TimeSpan Expect(TimeSpan mediaDuration) => At(WorkPerSecondOfAudio, mediaDuration);

    /// <summary>
    /// Says how long this much media would take at the fastest any measured item
    /// went.
    /// </summary>
    /// <param name="mediaDuration">How long the media is.</param>
    /// <returns>The short end of the range.</returns>
    /// <remarks>
    /// Never longer than <see cref="ExpectAtWorst"/>, because the ratio it applies
    /// is never larger, and never a promise: it is the best that has actually
    /// happened here and not a floor anything guarantees.
    /// </remarks>
    public TimeSpan ExpectAtBest(TimeSpan mediaDuration) => At(_fastest, mediaDuration);

    /// <summary>
    /// Says how long this much media would take at the slowest any measured item
    /// went.
    /// </summary>
    /// <param name="mediaDuration">How long the media is.</param>
    /// <returns>The long end of the range.</returns>
    public TimeSpan ExpectAtWorst(TimeSpan mediaDuration) => At(_slowest, mediaDuration);

    private static double RatioOf(TimeSpan audioDuration, TimeSpan processingTime) =>
        processingTime.Ticks / (double)audioDuration.Ticks;

    private static TimeSpan At(double ratio, TimeSpan mediaDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mediaDuration, TimeSpan.Zero);

        var ticks = mediaDuration.Ticks * ratio;

        return ticks >= long.MaxValue
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks((long)ticks);
    }

    private static void Refuse(TimeSpan audioDuration, TimeSpan processingTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(audioDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(processingTime, TimeSpan.Zero);
    }
}
