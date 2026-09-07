using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.WhisperSubtitles.Library;

/// <summary>
/// The reading that reaches the server's library.
/// </summary>
/// <remarks>
/// The only type in this plugin that names a server library type, which is what
/// makes the seam above worth having: every other part of a run takes values, and
/// the translation from the server's entities into those values happens here and
/// nowhere else.
///
/// WHICH SERVER CALLS STAND BEHIND EACH ANSWER WAS READ RATHER THAN REMEMBERED,
/// on both supported lines, out of the assemblies the two pins name. The commands
/// are in the pull request that landed this class. Every member used below is
/// declared identically on 10.11 and on 12.0, which is why there is one
/// implementation and no conditional compilation: <c>GetItemList</c>,
/// <c>GetItemById</c>, <c>GetLibraryOptions</c> and <c>GetCollectionFolders</c> on
/// the library manager, and <c>GetMediaStreams</c>, <c>GetInternalMetadataPath</c>
/// and <c>GetBaseItemKind</c> on the item.
///
/// THE QUERY IS DELIBERATELY WIDE. It asks the store for video items that are not
/// placeholders and stops there, so the kind an operator put in scope, the
/// libraries they enabled and every other bound stay in
/// <see cref="ItemSelection"/>. Narrowing here would put half of an operator's
/// settings behind a seam a test cannot vary and would make the estimate and the
/// run disagree about what the population was.
/// </remarks>
public sealed class ServerLibrarySource : ILibrarySource
{
    private readonly ILibraryManager _library;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerLibrarySource"/> class.
    /// </summary>
    /// <param name="library">The server's library manager.</param>
    public ServerLibrarySource(ILibraryManager library)
    {
        ArgumentNullException.ThrowIfNull(library);

        _library = library;
    }

    /// <inheritdoc />
    public IReadOnlyList<ItemDescription> ItemsUnderConsideration()
    {
        // Recursive with no parent is the whole store. IsVirtualItem excludes the
        // rows a server keeps for an episode it knows about and does not have, and
        // transcribing one of those is not a thing that can be attempted: there is
        // no file.
        var query = new InternalItemsQuery
        {
            MediaTypes = [MediaType.Video],
            Recursive = true,
            IsVirtualItem = false,
        };

        return _library.GetItemList(query)
            .Where(item => item is not null)
            .Select(Describe)
            .ToList();
    }

    /// <inheritdoc />
    public ItemLocation? LocationOf(Guid itemId)
    {
        var item = _library.GetItemById(itemId);

        if (item is null || string.IsNullOrWhiteSpace(item.Path))
        {
            return null;
        }

        // The library option and not a plugin setting. An operator who told the
        // server not to write into their media tree said something this plugin does
        // not get to overrule, and the option belongs to the item's library rather
        // than to the server, so it is read per item.
        return new ItemLocation(
            item.Path,
            item.GetInternalMetadataPath(),
            _library.GetLibraryOptions(item).SaveSubtitlesWithMedia);
    }

    /// <summary>
    /// One item, as the parts of a run read it.
    /// </summary>
    /// <param name="item">The server's entity.</param>
    /// <returns>The description.</returns>
    private ItemDescription Describe(BaseItem item)
    {
        var streams = item.GetMediaStreams() ?? [];

        return new ItemDescription(
            item.Id,
            item.Name ?? string.Empty,
            LibraryOf(item),
            item.GetBaseItemKind().ToString(),
            TimeSpan.FromTicks(item.RunTimeTicks ?? 0),
            streams.Any(stream => stream.Type == MediaStreamType.Audio),
            SubtitleLanguagesOf(streams),
            new DateTimeOffset(DateTime.SpecifyKind(item.DateCreated, DateTimeKind.Utc)));
    }

    /// <summary>
    /// The library an item is in.
    /// </summary>
    /// <remarks>
    /// The collection folder rather than the item's immediate parent, because that
    /// is the identifier an operator's per-library settings are keyed by. An item
    /// the server places in no collection folder answers <see cref="Guid.Empty"/>,
    /// which no enabled-library set contains, so selection leaves it alone instead
    /// of guessing which library's language it should be transcribed into.
    /// </remarks>
    /// <param name="item">The server's entity.</param>
    /// <returns>The library's identifier, or an empty one.</returns>
    private Guid LibraryOf(BaseItem item)
    {
        var folders = _library.GetCollectionFolders(item);

        return folders is { Count: > 0 } ? folders[0].Id : Guid.Empty;
    }

    /// <summary>
    /// The languages the item already has a subtitle in.
    /// </summary>
    /// <remarks>
    /// From any source, which is what the description's own remarks ask for: an
    /// embedded track and a file somebody downloaded both arrive as subtitle
    /// streams on the item, and both mean an operator watching it would see one.
    ///
    /// A stream with no language is dropped rather than carried as an empty string.
    /// Selection compares a target against these, and an empty entry would match
    /// nothing while making the list read as though it held an answer.
    /// </remarks>
    /// <param name="streams">The item's media streams.</param>
    /// <returns>The languages, in the order the server reports them.</returns>
    private static List<string> SubtitleLanguagesOf(IReadOnlyList<MediaStream> streams) =>
        streams
            .Where(stream => stream.Type == MediaStreamType.Subtitle)
            .Select(stream => stream.Language)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language!)
            .ToList();
}
