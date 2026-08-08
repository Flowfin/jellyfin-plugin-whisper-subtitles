using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Tells the server that an item has a new external subtitle, under a rule about
/// how often that may happen.
/// </summary>
/// <remarks>
/// A subtitle nobody can select is not a delivered subtitle. The file is on disk
/// as soon as the write finishes, and it becomes a selectable track when the
/// server looks at the item again, which without this happens at the next library
/// scan.
///
/// THE RULE, because the issue asks that it be written down rather than
/// implemented and left to be inferred:
///
/// One refresh request in flight at a time, however many items a run is
/// transcribing at once, and each item asked for at most once per run.
///
/// Both halves are about the machine rather than about correctness. A run is
/// allowed to transcribe several items at once, and #19 caps that; nothing caps
/// what follows each one, so a run finishing four items together would ask the
/// server to reread four items while it is still transcribing the next four. The
/// serialisation costs nothing worth having: a refresh is short, and an item whose
/// refresh waits behind another still has its file on disk.
///
/// The once-per-run half is for the case where a run writes twice for one item, a
/// second language after a first. The second write does not need its own refresh
/// if the first has not happened yet, and asking twice for one item is the shape
/// that turns a large library into work for the rest of the server.
///
/// What this does not do is retry. A refresh that failed is recorded and the item
/// counts as done, because the file is already correct and the server will find it
/// on its own schedule.
/// </remarks>
public sealed class SubtitleDelivery : IDisposable
{
    private readonly IItemRefresher _refresher;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private readonly HashSet<Guid> _asked = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleDelivery"/> class.
    /// </summary>
    /// <param name="refresher">The seam every refresh request goes through.</param>
    public SubtitleDelivery(IItemRefresher refresher)
    {
        _refresher = refresher ?? throw new ArgumentNullException(nameof(refresher));
    }

    /// <summary>
    /// Gets how many items this run has asked about.
    /// </summary>
    /// <remarks>
    /// Here so a run can say what it did, and so the once-per-run half of the rule
    /// is observable from outside rather than only from the seam.
    /// </remarks>
    public int ItemsAskedAbout
    {
        get
        {
            lock (_asked)
            {
                return _asked.Count;
            }
        }
    }

    /// <summary>
    /// Reports one written subtitle to the server.
    /// </summary>
    /// <param name="itemId">The item the file belongs to.</param>
    /// <param name="path">The file that was written.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The file, and what happened when the server was told.</returns>
    /// <remarks>
    /// Cancellation propagates and a refresh failure does not. The two are
    /// different events: an operator stopping the run is not something to swallow,
    /// and a server that refused a refresh has not damaged anything.
    /// </remarks>
    public async Task<DeliveredSubtitle> ReportAsync(
        Guid itemId,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!FirstTimeFor(itemId))
        {
            return new DeliveredSubtitle(path, false, "This run has already asked the server to look at this item.");
        }

        await _oneAtATime.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _refresher.RefreshAsync(itemId, cancellationToken).ConfigureAwait(false);

            return new DeliveredSubtitle(path, true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception refused)
        {
            // Deliberately every other exception. The file is written and correct,
            // and whatever a server implementation throws on the way to a metadata
            // refresh must not turn that into a failed item. The reason is carried
            // out so the run can say what happened rather than losing it here.
            return new DeliveredSubtitle(
                path,
                false,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The subtitle is written. Asking the server to look at the item again failed, so it becomes selectable when the server next reads the folder. {0}",
                    refused.Message));
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    /// <summary>
    /// Releases the one thing this holds.
    /// </summary>
    /// <remarks>
    /// One of these belongs to one run, so it is disposed where the run ends. It
    /// exists because the rule needs something to wait on; there is nothing else in
    /// here to release.
    /// </remarks>
    public void Dispose() => _oneAtATime.Dispose();

    private bool FirstTimeFor(Guid itemId)
    {
        lock (_asked)
        {
            return _asked.Add(itemId);
        }
    }
}
