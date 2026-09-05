using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Api;

/// <summary>
/// What the page posts when an operator confirms a removal: the paths the
/// listing offered them.
/// </summary>
/// <remarks>
/// Paths and nothing else, and they are not what decides. The route lists the
/// record again and removes only a file the record names, that the listing
/// finds unchanged, that this list names, and that still hashes the same at the
/// moment of deletion. A path posted that the record does not name is never read.
/// </remarks>
public sealed class GeneratedSubtitleRemovalRequest
{
    /// <summary>
    /// Gets or sets the paths the listing offered and the operator confirmed.
    /// </summary>
    public IReadOnlyList<string> Paths { get; set; } = [];
}
