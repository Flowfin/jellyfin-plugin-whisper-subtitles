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

        if (string.IsNullOrWhiteSpace(options.TargetLanguage))
        {
            // Without a target language there is no question to answer: "does this
            // item already have a subtitle in the language we are about to make one
            // in" has no truth value. Selecting nothing is the only answer that
            // cannot transcribe a library into a language nobody asked for.
            return new SelectionResult(Array.Empty<ItemDescription>(), TimeSpan.Zero);
        }

        var target = options.TargetLanguage.Trim();

        var candidates = items
            .Where(item => item is not null)
            .Where(item => options.EnabledLibraries.Contains(item.LibraryId))
            .Where(item => options.KindsInScope.Any(k => string.Equals(k, item.Kind, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item.HasAudioStream)
            .Where(item => !HasSubtitleIn(item, target))
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
