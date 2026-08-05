using System;
using System.Globalization;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The paths a server hands a plugin at construction.
/// </summary>
/// <remarks>
/// Constructing a plugin reads at least one of these, so this cannot simply
/// refuse. Every path points under a directory that is never created, so a test
/// that stops reading identity and starts doing real file work fails on a
/// missing directory rather than writing somewhere on the machine running the
/// suite. Nothing here touches a display, a trust store or an elevated right.
/// </remarks>
internal sealed class UnwrittenApplicationPaths : IApplicationPaths
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        string.Format(CultureInfo.InvariantCulture, "whisper-subtitles-tests-{0:N}", Guid.NewGuid()));

    public string ProgramDataPath => Under("program-data");

    public string WebPath => Under("web");

    public string ProgramSystemPath => Under("program-system");

    public string DataPath => Under("data");

    public string ImageCachePath => Under("image-cache");

    public string PluginsPath => Under("plugins");

    public string PluginConfigurationsPath => Under("plugin-configurations");

    public string LogDirectoryPath => Under("log");

    public string ConfigurationDirectoryPath => Under("configuration");

    public string SystemConfigurationFilePath => Path.Combine(Under("configuration"), "system.xml");

    public string CachePath => Under("cache");

    public string TempDirectory => Under("temp");

    public string VirtualDataPath => Under("virtual-data");

    public string TrickplayPath => Under("trickplay");

    public string BackupPath => Under("backup");

    // Both members are the same on 10.11 and on 12.0, so no conditional is needed
    // for either. Both do real work on disk in the server's own implementation,
    // which is why they refuse here rather than doing nothing: a silent no-op
    // would let a test believe a directory was made.
    public void MakeSanityCheckOrThrow() => throw new NotSupportedException(Reason);

    public void CreateAndCheckMarker(string path, string markerName, bool recursive = false)
        => throw new NotSupportedException(Reason);

    private static string Reason =>
        "The identity tests construct the plugin without a server and create nothing on disk.";

    private string Under(string leaf) => Path.Combine(_root, leaf);
}

/// <summary>
/// The serializer a server hands a plugin at construction. Reading the plugin
/// identity never touches stored configuration, so every call here is a defect
/// and says so instead of answering.
/// </summary>
internal sealed class ThrowingXmlSerializer : IXmlSerializer
{
    public object DeserializeFromStream(Type type, Stream stream) => throw new NotSupportedException(Reason);

    public void SerializeToStream(object obj, Stream stream) => throw new NotSupportedException(Reason);

    public void SerializeToFile(object obj, string file) => throw new NotSupportedException(Reason);

    public object DeserializeFromFile(Type type, string file) => throw new NotSupportedException(Reason);

    public object DeserializeFromBytes(Type type, byte[] buffer) => throw new NotSupportedException(Reason);

    private static string Reason =>
        "The identity tests do not read or write plugin configuration.";
}
