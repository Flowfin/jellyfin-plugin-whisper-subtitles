using System.IO;
using MediaBrowser.Controller.IO;

namespace AbiFloorBite;

/// <summary>
/// One call to a Jellyfin symbol that arrived inside the supported server line,
/// after the version the manifest promises to install on.
/// </summary>
/// <remarks>
/// <para>
/// The mistake this stands in for is not a careless one. An author writing
/// against the pinned package sees this method offered by the editor, uses it,
/// builds green, and ships a plugin that loads on the oldest server the manifest
/// claims and then throws where the call is reached. For a scheduled task that
/// is the middle of a run rather than the install.
/// </para>
/// <para>
/// The type is chosen so the failure is about the symbol and nothing else.
/// <c>MediaBrowser.Controller.IO.FileSystemHelper</c> exists in both versions and
/// carries <c>DeleteFile</c> and <c>DeleteEmptyFolders</c> in both, so the
/// namespace resolves, the type resolves, and only <c>ResolveLinkTarget</c> is
/// missing at the floor. A fixture naming a whole namespace that arrived later
/// would fail at the floor as well and would prove far less, because it would
/// also fail on a package that failed to restore.
/// </para>
/// <para>
/// Measured rather than remembered, by dumping the exported members of both
/// packages and comparing the two sets. The command is in the workflow that
/// builds this fixture.
/// </para>
/// </remarks>
internal static class SymbolAddedAfterTheFloor
{
    /// <summary>
    /// Follows a symbolic link the way a plugin written against the pinned
    /// package would.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The target of the link, or null where the path is not one.</returns>
    internal static FileInfo? Resolve(string path)
        => FileSystemHelper.ResolveLinkTarget(path, true);
}
