using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// What became of one attempt to publish one subtitle.
/// </summary>
/// <remarks>
/// A returned value rather than an exception, and that is the whole point of the
/// type. A name that is already taken is a normal thing to find in a directory
/// full of the operator's own files, not a fault in the run, and a run that
/// counted it as a failure would report a library of hand corrected subtitles as
/// a thousand errors.
///
/// So the two states this carries are the two an operator has to be able to tell
/// apart: a subtitle was written, or one was already there and nothing was
/// touched. Everything else still throws, because everything else is a fault.
/// </remarks>
public sealed class SubtitlePublication
{
    private SubtitlePublication(SubtitlePublicationOutcome outcome, string path)
    {
        Outcome = outcome;
        Path = path;
    }

    /// <summary>
    /// Gets what became of the attempt.
    /// </summary>
    public SubtitlePublicationOutcome Outcome { get; }

    /// <summary>
    /// Gets the path the attempt was about.
    /// </summary>
    /// <remarks>
    /// The destination either way. Where the outcome is a skip this is the file
    /// that was found rather than one this plugin wrote, and it is carried so a
    /// report names the file an operator would go and look at.
    /// </remarks>
    public string Path { get; }

    /// <summary>
    /// Gets a value indicating whether a subtitle was written.
    /// </summary>
    public bool WasWritten => Outcome == SubtitlePublicationOutcome.Written;

    /// <summary>
    /// The outcome of an attempt that wrote the file.
    /// </summary>
    /// <param name="path">The name the file was published under.</param>
    /// <returns>The outcome.</returns>
    public static SubtitlePublication Written(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new SubtitlePublication(SubtitlePublicationOutcome.Written, path);
    }

    /// <summary>
    /// The outcome of an attempt that found the name already taken.
    /// </summary>
    /// <param name="path">The file that was found.</param>
    /// <returns>The outcome.</returns>
    public static SubtitlePublication SkippedBecauseSomethingIsAlreadyThere(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new SubtitlePublication(SubtitlePublicationOutcome.SkippedTargetExists, path);
    }
}
