using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Audio;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A removal that takes nothing off any disk and says what it was asked to take.
/// </summary>
internal sealed class StubFileRemoval : IFileRemoval
{
    /// <summary>
    /// Gets the paths this was asked to delete, in order.
    /// </summary>
    public List<string> Removed { get; } = [];

    public void Delete(string path) => Removed.Add(path);
}
