using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Estimation;

/// <summary>
/// What a run would cost, worked out without transcribing anything.
/// </summary>
/// <remarks>
/// Every figure an operator needs before committing a machine, and nothing that
/// required the machine to be committed to produce it. The type carries the
/// figures rather than the sentences that show them, because the surface that
/// shows them is not this and a report that had already been turned into prose
/// could not be shown two ways.
///
/// The two figures that are not numbers are here as sentences on purpose.
/// <see cref="ModelMemory"/> is a quotation of somebody else's published figure or
/// a statement that the memory belongs to another machine, and
/// <see cref="Estimation.DryRunEstimate.Refusal"/> is a reason rather than a
/// value; both are facts about where a number came from or why there is none, and
/// flattening either into a number is exactly the loss this plugin's estimate
/// rules exist against.
/// </remarks>
public sealed class DryRunReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DryRunReport"/> class.
    /// </summary>
    /// <param name="backend">The backend the run would use.</param>
    /// <param name="model">The model it would be given.</param>
    /// <param name="items">How many items the selection came to.</param>
    /// <param name="totalDuration">How much media those items hold.</param>
    /// <param name="estimate">The wall-clock range, or why there is none.</param>
    /// <param name="modelMemory">How much memory the model holds while it runs.</param>
    /// <param name="peakTemporaryAudioBytes">Temporary disk at the worst moment.</param>
    /// <param name="itemsAtOnce">How many items are transcribed together.</param>
    /// <param name="threadsPerItem">How many threads one transcription is given.</param>
    public DryRunReport(
        string backend,
        string model,
        int items,
        TimeSpan totalDuration,
        DryRunEstimate estimate,
        string modelMemory,
        long peakTemporaryAudioBytes,
        int itemsAtOnce,
        int threadsPerItem)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        Backend = backend;
        Model = model;
        Items = items;
        TotalDuration = totalDuration;
        Estimate = estimate;
        ModelMemory = modelMemory;
        PeakTemporaryAudioBytes = peakTemporaryAudioBytes;
        ItemsAtOnce = itemsAtOnce;
        ThreadsPerItem = threadsPerItem;
    }

    /// <summary>
    /// Gets the backend the run would use.
    /// </summary>
    public string Backend { get; }

    /// <summary>
    /// Gets the model it would be given.
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// Gets how many items the selection came to.
    /// </summary>
    public int Items { get; }

    /// <summary>
    /// Gets how much media those items hold.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the wall-clock range, or the reason there is none.
    /// </summary>
    public DryRunEstimate Estimate { get; }

    /// <summary>
    /// Gets how much memory the model holds while it runs, and whose figure that
    /// is.
    /// </summary>
    public string ModelMemory { get; }

    /// <summary>
    /// Gets how much temporary disk the extracted audio needs at peak, as a floor.
    /// </summary>
    public long PeakTemporaryAudioBytes { get; }

    /// <summary>
    /// Gets how many items the run transcribes at once.
    /// </summary>
    public int ItemsAtOnce { get; }

    /// <summary>
    /// Gets how many threads one transcription is given.
    /// </summary>
    /// <remarks>
    /// One transcription's budget rather than the run's. What the run holds at
    /// peak is this multiplied by <see cref="ItemsAtOnce"/>, and both are here so
    /// a surface can say either without a reader having to know which one it was
    /// given.
    /// </remarks>
    public int ThreadsPerItem { get; }
}
