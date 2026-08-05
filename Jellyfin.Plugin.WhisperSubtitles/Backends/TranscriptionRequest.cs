namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// What to transcribe, and in which language.
/// </summary>
public sealed class TranscriptionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionRequest"/> class.
    /// </summary>
    /// <param name="audioFilePath">The audio file to transcribe.</param>
    /// <param name="language">The language to transcribe in, or null to ask the backend to detect it.</param>
    public TranscriptionRequest(string audioFilePath, string? language)
    {
        AudioFilePath = audioFilePath;
        Language = language;
    }

    /// <summary>
    /// Gets the audio file to transcribe.
    /// </summary>
    public string AudioFilePath { get; }

    /// <summary>
    /// Gets the language to transcribe in, or null to ask the backend to detect it.
    /// </summary>
    /// <remarks>
    /// Null is a request to detect and not a request to translate. A backend
    /// transcribes what was spoken; asking for a language other than the spoken one
    /// is not something this interface can express, on purpose.
    /// </remarks>
    public string? Language { get; }
}
