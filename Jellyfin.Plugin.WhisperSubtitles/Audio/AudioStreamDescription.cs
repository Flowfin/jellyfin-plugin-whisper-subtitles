namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// Everything choosing an audio stream needs to know about one of them, and
/// nothing else.
/// </summary>
/// <remarks>
/// A flat description rather than a server type, for the reason
/// <see cref="Selection.ItemDescription"/> is one: the choice has to be testable
/// without a library, and a test cannot fabricate one out of types that exist
/// only inside a server.
/// </remarks>
public sealed class AudioStreamDescription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AudioStreamDescription"/> class.
    /// </summary>
    /// <param name="index">The stream's index inside the container.</param>
    /// <param name="language">The language the server holds for it, or null when it holds none.</param>
    /// <param name="channels">How many channels it carries.</param>
    /// <param name="isDefault">Whether the container marks it as the default.</param>
    public AudioStreamDescription(int index, string? language, int channels, bool isDefault)
    {
        Index = index;
        Language = language;
        Channels = channels;
        IsDefault = isDefault;
    }

    /// <summary>
    /// Gets the stream's index inside the container.
    /// </summary>
    /// <remarks>
    /// The absolute index across every stream in the file, which is what the
    /// server reports and what the extractor maps on. An index counted among the
    /// audio streams alone would name a different stream in any file that has a
    /// video track before them, which is every film.
    /// </remarks>
    public int Index { get; }

    /// <summary>
    /// Gets the language the server holds for the stream, or null when it holds none.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets how many channels the stream carries.
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets a value indicating whether the container marks the stream as the default.
    /// </summary>
    public bool IsDefault { get; }
}
