using System;

namespace Jellyfin.Plugin.WhisperSubtitles.Detection;

/// <summary>
/// The confidence a detected language has to reach before anything is written
/// under it.
/// </summary>
/// <remarks>
/// A type of its own rather than the plugin configuration, for the same reason
/// <see cref="Backends.Local.LocalBackendOptions"/> is one: the rule can be built
/// and driven in a test without a server writing a file. Where an operator types
/// the number is the configuration page in #36, and the schema and validation
/// around it are #40. Both of those exist and this number is on neither of them,
/// so today the floor is a value this plugin carries and not one an operator can
/// see.
///
/// The floor is a setting rather than a constant because the right number depends
/// on the library. A collection of clean studio audio can afford a high floor; a
/// collection of field recordings with a high floor produces nothing at all, and
/// an operator who cannot lower it will turn detection off instead and lose the
/// refusal along with it.
/// </remarks>
public sealed class DetectionOptions
{
    /// <summary>
    /// The floor used when the operator has named none.
    /// </summary>
    /// <remarks>
    /// A default and not a measurement. Nothing in this repository has measured
    /// what any backend's detector scores on any corpus, and the number is stated
    /// here so a later measurement has something to argue with rather than a
    /// value hidden in a branch. It is set high because the failure this whole
    /// rule exists against is a confident wrong language written into a library
    /// and believed by everything downstream, and the cost of the other mistake
    /// is one item left untranscribed with its candidate and score recorded.
    /// </remarks>
    public const double DefaultConfidenceFloor = 0.8;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetectionOptions"/> class.
    /// </summary>
    /// <param name="confidenceFloor">The score a detected language has to reach, between zero and one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The floor is not a score.</exception>
    public DetectionOptions(double confidenceFloor = DefaultConfidenceFloor)
    {
        // Refused rather than clamped. A floor above one refuses every detection
        // and a floor below zero accepts every detection, and both are settings
        // that look like they are doing something while doing the opposite of
        // what whoever typed them meant. NaN compares false against every
        // threshold, so it is refused by the same check rather than by a second
        // one.
        if (!(confidenceFloor >= 0 && confidenceFloor <= 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceFloor),
                confidenceFloor,
                "The confidence floor is a score between zero and one.");
        }

        ConfidenceFloor = confidenceFloor;
    }

    /// <summary>
    /// Gets the score a detected language has to reach before it is accepted.
    /// </summary>
    /// <remarks>
    /// At the floor is accepted, below it is not. An operator who types the number
    /// a backend reported and expects that item to pass gets the item, which is
    /// the reading that surprises nobody.
    /// </remarks>
    public double ConfidenceFloor { get; }
}
