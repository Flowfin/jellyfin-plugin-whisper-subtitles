using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Selection;

/// <summary>
/// The bounds an operator set on what a run may touch.
/// </summary>
/// <remarks>
/// Separate from <c>PluginConfiguration</c>, which carries no settings yet and
/// which is a type the server serialises. This is the shape selection reads, so
/// selection stays a function of its arguments and a test can vary one bound at a
/// time.
/// </remarks>
public sealed class SelectionOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionOptions"/> class.
    /// </summary>
    /// <param name="enabledLibraries">The libraries the operator enabled.</param>
    /// <param name="kindsInScope">The item types in scope, as the server names them.</param>
    /// <param name="targetLanguage">The language to transcribe into.</param>
    /// <param name="maximumItemDuration">The longest item a run may take, or null for no bound.</param>
    /// <param name="addedSince">Only items added at or after this moment, or null for no bound.</param>
    public SelectionOptions(
        IReadOnlyList<Guid> enabledLibraries,
        IReadOnlyList<string> kindsInScope,
        string? targetLanguage,
        TimeSpan? maximumItemDuration,
        DateTimeOffset? addedSince)
    {
        EnabledLibraries = enabledLibraries;
        KindsInScope = kindsInScope;
        TargetLanguage = targetLanguage;
        MaximumItemDuration = maximumItemDuration;
        AddedSince = addedSince;
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
    /// Gets the language to transcribe into.
    /// </summary>
    public string? TargetLanguage { get; }

    /// <summary>
    /// Gets the longest item a run may take, or null for no bound.
    /// </summary>
    public TimeSpan? MaximumItemDuration { get; }

    /// <summary>
    /// Gets the moment before which items are left alone, or null for no bound.
    /// </summary>
    public DateTimeOffset? AddedSince { get; }
}
