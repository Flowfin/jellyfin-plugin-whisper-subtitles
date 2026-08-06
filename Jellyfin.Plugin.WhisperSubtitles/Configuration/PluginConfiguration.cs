using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// Deliberately empty. The server writes this type to disk and reads it back on
/// every start, so anything declared here has to be carried by every later
/// version or migrated away from, and a demonstration setting is a migration
/// nobody wanted. Each real setting arrives with the feature that reads it.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
}
