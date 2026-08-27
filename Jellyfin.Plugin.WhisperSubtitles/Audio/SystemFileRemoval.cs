using System.IO;

namespace Jellyfin.Plugin.WhisperSubtitles.Audio;

/// <summary>
/// The removal that reaches the disk, behind <see cref="IFileRemoval"/>.
/// </summary>
/// <remarks>
/// It sits in a file of its own so that the composition root is the one place it
/// is built. It used to be a private class inside <see cref="TemporaryAudioSweep"/>
/// held by a static property, with a one-argument overload closing over it, so a
/// caller took the real removal without asking any container for it and nothing
/// said so. That is the shape <see cref="PluginServiceRegistrator"/> exists
/// against, and <c>CompositionRootTests</c> is what refuses the next one.
///
/// It carries no state and nothing but the call, because everything interesting
/// about a sweep is the sweep's: which directory it reads, which names it matches,
/// and what it does with a file that will not go. Those are asserted through a
/// double rather than against a real disk, for the reason <see cref="IFileRemoval"/>
/// gives in its own remarks.
/// </remarks>
public sealed class SystemFileRemoval : IFileRemoval
{
    /// <inheritdoc />
    public void Delete(string path) => File.Delete(path);
}
