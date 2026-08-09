using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// Every name the configuration may hold for a backend.
/// </summary>
/// <remarks>
/// One set in one place, and it lives beside the backends because this is the
/// folder allowed to name concrete ones. A validator that carried its own copy
/// would be a second list to keep in step, and the failure it would produce is
/// the quiet one: an operator types a name that is real, the copy has not heard
/// of it, and the run falls back to transcribing nothing while the page says the
/// setting is wrong.
///
/// Comparison is case insensitive here because it is case insensitive in
/// <see cref="BackendSelector"/>, and a validator stricter than the thing it
/// validates would refuse a setting that then worked.
/// </remarks>
public static class BackendNames
{
    private static readonly FrozenSet<string> _known = new[]
    {
        NotConfiguredBackend.BackendName,
        Local.LocalWhisperBackend.BackendName,
        Remote.RemoteWhisperBackend.BackendName,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets every backend name this plugin answers to.
    /// </summary>
    public static IReadOnlyCollection<string> Known => _known;

    /// <summary>
    /// Whether this plugin has a backend under that name.
    /// </summary>
    /// <param name="name">Whatever the configuration held.</param>
    /// <returns>True where a backend answers to it.</returns>
    public static bool IsKnown(string? name) =>
        name is not null && _known.Contains(name.Trim());
}
