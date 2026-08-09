using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration;

/// <summary>
/// What came of reading the configuration: the settings in force, and everything
/// that had to be refused to arrive at them.
/// </summary>
/// <remarks>
/// Both halves together, because either alone is misleading. Settings with the
/// complaints thrown away is a run behaving in a way the file does not explain.
/// Complaints without the settings is an error report with no answer to what the
/// plugin is going to do now.
///
/// There is no failed variant of this. A configuration that cannot be read at all
/// still produces a load, with the documented defaults in force and one complaint
/// about the file, because a plugin that refuses to load is a plugin the operator
/// cannot repair through the page.
/// </remarks>
public sealed class ConfigurationLoad
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationLoad"/> class.
    /// </summary>
    /// <param name="inForce">The settings a run will use.</param>
    /// <param name="complaints">Every value that could not be honoured.</param>
    public ConfigurationLoad(SettingsInForce inForce, IReadOnlyList<SettingComplaint> complaints)
    {
        InForce = inForce;
        Complaints = complaints;
    }

    /// <summary>
    /// Gets the settings a run will use.
    /// </summary>
    public SettingsInForce InForce { get; }

    /// <summary>
    /// Gets every value that could not be honoured, in the order the fields are
    /// declared.
    /// </summary>
    public IReadOnlyList<SettingComplaint> Complaints { get; }
}
