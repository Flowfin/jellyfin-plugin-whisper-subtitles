using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Selection;

/// <summary>
/// The bounds an operator set on what a run may touch.
/// </summary>
/// <remarks>
/// Separate from <c>PluginConfiguration</c>, which is the type the server
/// serialises and stores on disk. What separates them is not that one side is
/// empty - a bound here and a setting there can name the same thing - but that
/// this is the shape selection reads, so selection stays a function of its
/// arguments and a test can vary one bound at a time.
/// </remarks>
public sealed class SelectionOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionOptions"/> class.
    /// </summary>
    /// <param name="enabledLibraries">The libraries the operator enabled.</param>
    /// <param name="kindsInScope">The item types in scope, as the server names them.</param>
    /// <param name="targetLanguage">The language to transcribe into where the item's library names none.</param>
    /// <param name="maximumItemDuration">The longest item a run may take, or null for no bound.</param>
    /// <param name="addedSince">Only items added at or after this moment, or null for no bound.</param>
    /// <param name="quarantinedItems">The items the attempt ledger is skipping.</param>
    /// <param name="targetLanguagesByLibrary">The target each library names for itself.</param>
    public SelectionOptions(
        IReadOnlyList<Guid> enabledLibraries,
        IReadOnlyList<string> kindsInScope,
        string? targetLanguage,
        TimeSpan? maximumItemDuration,
        DateTimeOffset? addedSince,
        IReadOnlySet<Guid>? quarantinedItems = null,
        IReadOnlyDictionary<Guid, string>? targetLanguagesByLibrary = null)
    {
        EnabledLibraries = enabledLibraries;
        KindsInScope = kindsInScope;
        TargetLanguage = targetLanguage;
        MaximumItemDuration = maximumItemDuration;
        AddedSince = addedSince;
        QuarantinedItems = quarantinedItems ?? new HashSet<Guid>();
        TargetLanguagesByLibrary = targetLanguagesByLibrary ?? new Dictionary<Guid, string>();
    }

    /// <summary>
    /// Gets the libraries the operator enabled.
    /// </summary>
    public IReadOnlyList<Guid> EnabledLibraries { get; }

    /// <summary>
    /// Gets the item types in scope, as the server names them.
    /// </summary>
    public IReadOnlyList<string> KindsInScope { get; }

    /// <summary>
    /// Gets the language to transcribe into where the item's library names none.
    /// </summary>
    /// <remarks>
    /// The server-wide answer, and the one a new library gets. A library that names
    /// its own target does not read this.
    /// </remarks>
    public string? TargetLanguage { get; }

    /// <summary>
    /// Gets the target each library names for itself.
    /// </summary>
    /// <remarks>
    /// Which language to produce is not one decision for a whole server. A library
    /// of films in one language and a shelf of recordings in another need different
    /// answers, and the alternative to holding them per library is an operator
    /// running the task once per language with the rest of the libraries switched
    /// off by hand.
    /// </remarks>
    public IReadOnlyDictionary<Guid, string> TargetLanguagesByLibrary { get; }

    /// <summary>
    /// Gets the longest item a run may take, or null for no bound.
    /// </summary>
    public TimeSpan? MaximumItemDuration { get; }

    /// <summary>
    /// Gets the moment before which items are left alone, or null for no bound.
    /// </summary>
    public DateTimeOffset? AddedSince { get; }

    /// <summary>
    /// Gets the items the attempt ledger is skipping.
    /// </summary>
    /// <remarks>
    /// Passed in rather than read from the ledger here, so selection stays a
    /// function of its arguments and a dry run can be shown for a ledger other
    /// than the live one.
    /// </remarks>
    public IReadOnlySet<Guid> QuarantinedItems { get; }

    /// <summary>
    /// The target that applies to items in one library.
    /// </summary>
    /// <param name="libraryId">The library.</param>
    /// <returns>What that library asks for, or the server-wide answer where it asks for nothing.</returns>
    /// <remarks>
    /// The per-library value wins, and a blank one does not count as a value: a
    /// library whose field was cleared falls back to the default rather than being
    /// left with no target, which would silently drop it out of every run.
    /// </remarks>
    public string? TargetFor(Guid libraryId) =>
        TargetLanguagesByLibrary.TryGetValue(libraryId, out var named) && !LanguageTarget.IsAbsent(named)
            ? named
            : TargetLanguage;
}
