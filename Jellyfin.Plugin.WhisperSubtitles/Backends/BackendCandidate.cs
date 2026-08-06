using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// One backend the operator could have chosen, and what selection needs to know
/// about it without running it.
/// </summary>
public sealed class BackendCandidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackendCandidate"/> class.
    /// </summary>
    /// <param name="name">The name the configuration uses for this backend.</param>
    /// <param name="backend">The backend itself.</param>
    /// <param name="missingSettings">The settings this backend cannot run without and does not have.</param>
    public BackendCandidate(string name, ITranscriptionBackend backend, IReadOnlyList<string> missingSettings)
    {
        Name = name;
        Backend = backend;
        MissingSettings = missingSettings;
    }

    /// <summary>
    /// Gets the name the configuration uses for this backend.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the backend itself.
    /// </summary>
    public ITranscriptionBackend Backend { get; }

    /// <summary>
    /// Gets the settings this backend cannot run without and does not have.
    /// </summary>
    /// <remarks>
    /// Kept apart from the readiness check on purpose. "You have not filled in the
    /// model path" and "the model path is filled in and the file is not there" are
    /// different sentences to an operator and have different repairs, and a
    /// readiness check that answered both would have to say the vaguer of the two.
    /// It is also the answer that costs nothing: it is a look at the configuration
    /// rather than a look at the machine.
    /// </remarks>
    public IReadOnlyList<string> MissingSettings { get; }
}
