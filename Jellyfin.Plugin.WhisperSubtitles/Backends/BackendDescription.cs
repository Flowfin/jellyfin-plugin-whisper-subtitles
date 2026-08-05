using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WhisperSubtitles.Backends;

/// <summary>
/// What a backend offers, so the administrator surface and the task can decide
/// without knowing which backend they are holding.
/// </summary>
public sealed class BackendDescription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackendDescription"/> class.
    /// </summary>
    /// <param name="name">The name shown to an operator.</param>
    /// <param name="models">The models this backend offers.</param>
    /// <param name="languages">The languages this backend offers, as the codes it accepts.</param>
    /// <param name="canDetectLanguage">Whether the backend can be asked to detect the language.</param>
    /// <param name="cancellationBudget">How long the backend may take to stop after cancellation.</param>
    public BackendDescription(
        string name,
        IReadOnlyList<string> models,
        IReadOnlyList<string> languages,
        bool canDetectLanguage,
        TimeSpan cancellationBudget)
    {
        Name = name;
        Models = models;
        Languages = languages;
        CanDetectLanguage = canDetectLanguage;
        CancellationBudget = cancellationBudget;
    }

    /// <summary>
    /// Gets the name shown to an operator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the models this backend offers.
    /// </summary>
    public IReadOnlyList<string> Models { get; }

    /// <summary>
    /// Gets the languages this backend offers, as the codes it accepts.
    /// </summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Gets a value indicating whether the backend can be asked to detect the language.
    /// </summary>
    public bool CanDetectLanguage { get; }

    /// <summary>
    /// Gets how long the backend may take to stop after cancellation.
    /// </summary>
    /// <remarks>
    /// Part of the contract rather than a courtesy. A backend that cannot stop
    /// within the time it states here does not satisfy this interface, and the
    /// number is stated so a test can hold it to that rather than waiting forever.
    /// </remarks>
    public TimeSpan CancellationBudget { get; }
}
