namespace Jellyfin.Plugin.WhisperSubtitles.Api;

/// <summary>
/// The one segment every path this plugin answers sits under.
/// </summary>
/// <remarks>
/// Written once, because a path is a claim on a server this plugin shares and
/// the claim record, the page and every controller spell it from here. A second
/// prefix would be a second claim nobody argued for.
/// </remarks>
public static class PluginRoute
{
    /// <summary>
    /// The segment every path this plugin answers sits under.
    /// </summary>
    public const string Prefix = "WhisperSubtitles";
}
