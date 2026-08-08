using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// Asks the server to look at one item again.
/// </summary>
/// <remarks>
/// One item and never a library. A library refresh triggered once per item during
/// a long run is its own resource problem, and it is the failure this interface's
/// shape refuses: there is nothing to pass it but an item.
///
/// Which server call stands behind it is decided where the adapter is written,
/// which is #71, and this is deliberately narrow enough for either candidate: a
/// queued refresh the server paces itself, or a refresh awaited here. What every
/// implementation owes is that it either completes or throws, and that it touches
/// only the item it was given.
/// </remarks>
public interface IItemRefresher
{
    /// <summary>
    /// Asks the server to look at one item again.
    /// </summary>
    /// <param name="itemId">The item whose files changed.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>A task that completes once the server has been asked.</returns>
    Task RefreshAsync(Guid itemId, CancellationToken cancellationToken);
}
