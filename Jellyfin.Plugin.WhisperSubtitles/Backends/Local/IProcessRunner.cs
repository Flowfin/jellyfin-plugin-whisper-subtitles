namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// The one place this plugin starts a program.
/// </summary>
/// <remarks>
/// Everything that would otherwise reach for <c>System.Diagnostics.Process</c>
/// goes through here, so a test can drive a backend without a binary on the
/// machine and so there is one surface to look at when asking what this plugin
/// is able to launch.
/// </remarks>
public interface IProcessRunner
{
    /// <summary>
    /// Starts a program and returns it without waiting.
    /// </summary>
    /// <param name="invocation">The program and its arguments.</param>
    /// <returns>The started program.</returns>
    /// <remarks>
    /// Throws when the program cannot be started at all, which is a different
    /// state from a program that started and failed, and the caller maps the two
    /// to different reasons.
    /// </remarks>
    IStartedProcess Start(ProcessInvocation invocation);
}
