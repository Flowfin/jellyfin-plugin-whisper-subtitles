using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jellyfin.Plugin.WhisperSubtitles.Library;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Selection;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A library made of values, so a run can be driven with no server.
/// </summary>
/// <remarks>
/// The whole reason <see cref="ILibrarySource"/> exists. Behind the real one is a
/// store this suite cannot start, and the two answers a run needs out of it are a
/// list and a lookup, so the stand-in is a list and a dictionary and nothing
/// cleverer.
///
/// It records what it was asked, because "the run read the library once" and "the
/// run read it per item" are different claims and neither is visible from the
/// answer.
/// </remarks>
internal sealed class StubLibrarySource : ILibrarySource
{
    private readonly IReadOnlyList<ItemDescription> _items;
    private readonly IReadOnlyDictionary<Guid, ItemLocation> _locations;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubLibrarySource"/> class.
    /// </summary>
    /// <param name="items">The items this library holds.</param>
    /// <param name="locations">Where each item's files are, for the items that have somewhere.</param>
    public StubLibrarySource(
        IReadOnlyList<ItemDescription> items,
        IReadOnlyDictionary<Guid, ItemLocation>? locations = null)
    {
        _items = items;
        _locations = locations ?? new Dictionary<Guid, ItemLocation>();
    }

    /// <summary>
    /// Gets the number of times the population was asked for.
    /// </summary>
    public int TimesTheItemsWereRead { get; private set; }

    /// <summary>
    /// Gets the items whose location was asked for, in order.
    /// </summary>
    public Collection<Guid> Located { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<ItemDescription> ItemsUnderConsideration()
    {
        TimesTheItemsWereRead++;

        return _items;
    }

    /// <inheritdoc />
    public ItemLocation? LocationOf(Guid itemId)
    {
        Located.Add(itemId);

        return _locations.TryGetValue(itemId, out var location) ? location : null;
    }
}
