namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// The two paths the local backend needs, and nothing else.
/// </summary>
/// <remarks>
/// A type of its own rather than the plugin configuration, so the backend can be
/// built and driven in a test without a server writing a file. Where these values
/// come from, how they are validated and what happens to an invalid one is #40,
/// and the page an operator types them into is #36.
///
/// Neither path is ever downloaded. That is a fixed property of this plugin
/// rather than a default: the operator supplies the tool and the model, and a
/// plugin that fetched several gigabytes it was not asked for would be making a
/// trust decision on somebody else's server.
/// </remarks>
public sealed class LocalBackendOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalBackendOptions"/> class.
    /// </summary>
    /// <param name="executablePath">The whisper.cpp compatible command line tool.</param>
    /// <param name="modelPath">The model file to hand it.</param>
    public LocalBackendOptions(string? executablePath, string? modelPath)
    {
        ExecutablePath = executablePath;
        ModelPath = modelPath;
    }

    /// <summary>
    /// Gets the whisper.cpp compatible command line tool, or null when the operator has named none.
    /// </summary>
    public string? ExecutablePath { get; }

    /// <summary>
    /// Gets the model file to hand it, or null when the operator has named none.
    /// </summary>
    public string? ModelPath { get; }

    /// <summary>
    /// Gets a value indicating whether both paths have been named.
    /// </summary>
    /// <remarks>
    /// Named, not checked. Whether the file at either path exists, runs, or is a
    /// model at all is the readiness probe in #15, and this property says nothing
    /// about it.
    /// </remarks>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ExecutablePath) && !string.IsNullOrWhiteSpace(ModelPath);
}
