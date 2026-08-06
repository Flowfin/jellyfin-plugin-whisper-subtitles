using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Selection;

/// <summary>
/// Everything selection needs to know about one library item, and nothing else.
/// </summary>
/// <remarks>
/// A flat description rather than a server type on purpose. Selection is the
/// decision that determines what a run costs, so it has to be answerable before
/// the run and testable without a server, and a test cannot fabricate a library
/// out of types that only exist inside one. Whatever reads the real library fills
/// these in.
/// </remarks>
public sealed class ItemDescription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemDescription"/> class.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="name">The item's name, used only to order two items that are otherwise equal.</param>
    /// <param name="libraryId">The library the item is in.</param>
    /// <param name="kind">The item's type, as the server names it.</param>
    /// <param name="duration">How long the media is.</param>
    /// <param name="hasAudioStream">Whether the item has any audio to transcribe.</param>
    /// <param name="subtitleLanguages">The languages the item already has a subtitle in, from any source.</param>
    /// <param name="dateAdded">When the item was added to the library.</param>
    public ItemDescription(
        Guid id,
        string name,
        Guid libraryId,
        string kind,
        TimeSpan duration,
        bool hasAudioStream,
        IReadOnlyList<string> subtitleLanguages,
        DateTimeOffset dateAdded)
    {
        Id = id;
        Name = name;
        LibraryId = libraryId;
        Kind = kind;
        Duration = duration;
        HasAudioStream = hasAudioStream;
        SubtitleLanguages = subtitleLanguages;
        DateAdded = dateAdded;
    }

    /// <summary>
    /// Gets the item's identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the item's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the library the item is in.
    /// </summary>
    public Guid LibraryId { get; }

    /// <summary>
    /// Gets the item's type, as the server names it.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets how long the media is.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets a value indicating whether the item has any audio to transcribe.
    /// </summary>
    public bool HasAudioStream { get; }

    /// <summary>
    /// Gets the languages the item already has a subtitle in, from any source.
    /// </summary>
    /// <remarks>
    /// From any source is the whole point: an embedded track, a file somebody
    /// downloaded and a file this plugin wrote last week all count, because an
    /// operator watching the item would see one either way and transcribing it
    /// again spends the machine on something nobody asked for.
    /// </remarks>
    public IReadOnlyList<string> SubtitleLanguages { get; }

    /// <summary>
    /// Gets when the item was added to the library.
    /// </summary>
    public DateTimeOffset DateAdded { get; }
}
