using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

/// <summary>
/// A program to run and the arguments to hand it, as separate strings.
/// </summary>
/// <remarks>
/// There is no command line here and no place to put one. The executable path
/// and the model path come from an operator, and one of the arguments is derived
/// from a media item, so a single string would put a quoting rule between what
/// was meant and what runs. Every element of <see cref="Arguments"/> reaches the
/// program as exactly one argument, whatever it contains.
/// </remarks>
public sealed class ProcessInvocation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessInvocation"/> class.
    /// </summary>
    /// <param name="executablePath">The program to run.</param>
    /// <param name="arguments">The arguments, one element per argument.</param>
    public ProcessInvocation(string executablePath, IReadOnlyList<string> arguments)
    {
        ExecutablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }

    /// <summary>
    /// Gets the program to run.
    /// </summary>
    public string ExecutablePath { get; }

    /// <summary>
    /// Gets the arguments, one element per argument.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }
}
