using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Selection;

/// <summary>
/// Decides which items a run touches.
/// </summary>
/// <remarks>
/// A function of its arguments and nothing else. It reads no server service, no
/// clock and no file, so an operator can be shown what a run would do before it
/// starts and a test can hand it a fabricated library and assert the answer
/// exactly. That is also what makes the cost estimate checkable: the estimate is
/// built on the total duration this returns.
/// </remarks>
public static class ItemSelection
{
    /// <summary>
    /// Chooses the items a run would transcribe.
    /// </summary>
    /// <param name="items">The library, as descriptions.</param>
    /// <param name="options">The bounds the operator set.</param>
    /// <returns>The candidates in the order a run would take them, and their total duration.</returns>
    public static SelectionResult Select(IReadOnlyList<ItemDescription> items, SelectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);

        var candidates = items
            .Where(item => item is not null)
            .Where(item => options.EnabledLibraries.Contains(item.LibraryId))
            .Where(item => options.KindsInScope.Any(k => string.Equals(k, item.Kind, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item.HasAudioStream)
            .Where(item => !options.QuarantinedItems.Contains(item.Id))
            .Where(item => WantsASubtitle(item, options.TargetFor(item.LibraryId)))
            .Where(item => options.MaximumItemDuration is null || item.Duration <= options.MaximumItemDuration.Value)
            .Where(item => options.AddedSince is null || item.DateAdded >= options.AddedSince.Value)
            .OrderBy(item => item.DateAdded)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();

        var total = TimeSpan.Zero;

        foreach (var item in candidates)
        {
            total += item.Duration;
        }

        return new SelectionResult(candidates, total);
    }

    /// <summary>
    /// Whether this item still needs the subtitle its library asks for.
    /// </summary>
    /// <remarks>
    /// The target is read per item, from the item's own library, because a run
    /// covers libraries that do not want the same language. It is the same value
    /// the write will use, which is what the agreement between this filter and the
    /// setting rests on: a second copy of the fallback rule anywhere would be a
    /// place for selection to skip an item the write would then have transcribed
    /// into something else.
    ///
    /// Three states, and none of them is the other two. No target at all selects
    /// nothing: "does this item already have a subtitle in the language we are
    /// about to make one in" has no truth value, and selecting nothing is the only
    /// answer that cannot transcribe a library into a language nobody asked for.
    /// Detection selects an item with no subtitle in any language, because the
    /// language that will come out is not knowable here and an item that already
    /// has a track is not the one to spend a machine on finding out. A named
    /// language selects an item that does not already have that one.
    /// </remarks>
    private static bool WantsASubtitle(ItemDescription item, string? target)
    {
        if (LanguageTarget.IsAbsent(target))
        {
            return false;
        }

        if (LanguageTarget.IsDetection(target))
        {
            return item.SubtitleLanguages.Count == 0;
        }

        return !HasSubtitleIn(item, target!.Trim());
    }

    /// <summary>
    /// Whether the item already has a subtitle in the target language.
    /// </summary>
    /// <remarks>
    /// The comparison is on the code as both sides spell it, ignoring case and
    /// surrounding space. It does not know that <c>eng</c> and <c>en</c> are the
    /// same language: mapping between the code sets a server, a container and an
    /// operator each use is #33, and doing half of it here would be a second
    /// mapping to keep in step with that one.
    /// </remarks>
    private static bool HasSubtitleIn(ItemDescription item, string target) =>
        item.SubtitleLanguages.Any(
            language => language is not null
                && string.Equals(language.Trim(), target, StringComparison.OrdinalIgnoreCase));
}
