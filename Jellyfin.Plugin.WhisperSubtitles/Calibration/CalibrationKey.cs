using System;
using System.Globalization;

namespace Jellyfin.Plugin.WhisperSubtitles.Calibration;

/// <summary>
/// The settings a throughput measurement was taken under, and the whole of what
/// decides whether it is evidence about the next run.
/// </summary>
/// <remarks>
/// Three parts, and each one is here because changing it changes how long a second
/// of audio takes. The backend, because a tool on this machine and an endpoint
/// somewhere else are not the same measurement. The model, because that is the
/// largest single lever on the cost of a second of audio. The thread count,
/// because it is how much of the machine one transcription is given.
///
/// What is NOT here is the machine. A factor measured on this server and read on
/// this server needs no machine in its key, and a factor that travelled to another
/// machine would need far more than one field to be honest about it. Nothing here
/// travels: this key lives beside a measurement in memory and neither survives a
/// restart, which is stated at <see cref="CalibrationLedger"/> rather than implied.
///
/// The model is a string rather than a path type on purpose. What identifies a
/// model to an operator is whatever they typed, and this compares two of those for
/// equality rather than resolving either; a key that resolved paths would call two
/// spellings of one file different measurements or two different files the same
/// one, depending on which direction it got wrong.
/// </remarks>
public sealed class CalibrationKey : IEquatable<CalibrationKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalibrationKey"/> class.
    /// </summary>
    /// <param name="backend">The backend that did the transcribing.</param>
    /// <param name="model">The model it was given.</param>
    /// <param name="threads">The threads one transcription was allowed.</param>
    public CalibrationKey(string backend, string model, int threads)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backend);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        Backend = backend.Trim();
        Model = model.Trim();
        Threads = threads;
    }

    /// <summary>
    /// Gets the backend that did the transcribing.
    /// </summary>
    public string Backend { get; }

    /// <summary>
    /// Gets the model it was given.
    /// </summary>
    /// <remarks>
    /// Empty is a legal value and means a backend that takes no model, which is
    /// not the same fact as a model nobody recorded. Nothing here can tell those
    /// two apart, and the caller that knows is the one that builds the key.
    /// </remarks>
    public string Model { get; }

    /// <summary>
    /// Gets the threads one transcription was allowed.
    /// </summary>
    public int Threads { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Ordinal on both strings. A backend name is a name this plugin answers to
    /// and a model is a path an operator typed, and neither is text in a language,
    /// so a culture-sensitive comparison would make the key mean different things
    /// on two servers with different locales.
    /// </remarks>
    public bool Equals(CalibrationKey? other) =>
        other is not null
        && Threads == other.Threads
        && string.Equals(Backend, other.Backend, StringComparison.Ordinal)
        && string.Equals(Model, other.Model, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CalibrationKey);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(Backend),
        StringComparer.Ordinal.GetHashCode(Model),
        Threads);

    /// <summary>
    /// The key as the one line something showing a measurement says it was taken
    /// under.
    /// </summary>
    /// <returns>The backend, the model and the thread count.</returns>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0}, model {1}, {2} thread(s)",
        Backend,
        Model.Length == 0 ? "(none)" : Model,
        Threads);
}
