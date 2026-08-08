namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// The language one library asks for.
/// </summary>
/// <remarks>
/// A class with settable properties and a parameterless constructor because the
/// server persists the configuration with <c>XmlSerializer</c>, which refuses a
/// dictionary and refuses an immutable type. The shape a dictionary would have
/// been is built from these when selection needs it, so the type the operator's
/// file holds and the type the code reads are allowed to differ.
/// </remarks>
public class LibraryLanguageTarget
{
    /// <summary>
    /// Gets or sets the library, as the identifier the server gives it.
    /// </summary>
    /// <remarks>
    /// A string rather than a Guid, because this is read back out of a file an
    /// operator can edit and a value that will not parse has to be droppable
    /// without the whole configuration failing to load.
    /// </remarks>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what that library asks for: a language code, or the reserved
    /// word for detection.
    /// </summary>
    public string Target { get; set; } = string.Empty;
}
