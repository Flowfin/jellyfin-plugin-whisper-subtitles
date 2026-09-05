using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// What a removal did.
/// </summary>
public sealed class GeneratedSubtitleRemoval
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSubtitleRemoval"/> class.
    /// </summary>
    /// <param name="removed">The files taken off the disk.</param>
    /// <param name="keptBecauseChanged">The files kept because their bytes differed from the listing.</param>
    /// <param name="alreadyGone">The files that were gone before the removal reached them.</param>
    public GeneratedSubtitleRemoval(
        IReadOnlyList<string> removed,
        IReadOnlyList<string> keptBecauseChanged,
        IReadOnlyList<string> alreadyGone)
    {
        Removed = removed ?? throw new ArgumentNullException(nameof(removed));
        KeptBecauseChanged = keptBecauseChanged ?? throw new ArgumentNullException(nameof(keptBecauseChanged));
        AlreadyGone = alreadyGone ?? throw new ArgumentNullException(nameof(alreadyGone));
    }

    /// <summary>
    /// Gets the files taken off the disk.
    /// </summary>
    public IReadOnlyList<string> Removed { get; }

    /// <summary>
    /// Gets the files kept because their bytes differed from the listing.
    /// </summary>
    public IReadOnlyList<string> KeptBecauseChanged { get; }

    /// <summary>
    /// Gets the files that were gone before the removal reached them.
    /// </summary>
    public IReadOnlyList<string> AlreadyGone { get; }
}
