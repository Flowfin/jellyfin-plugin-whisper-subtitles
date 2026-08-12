using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A file system that answers whatever a test wrote about a path, and records
/// which paths were asked about.
/// </summary>
/// <remarks>
/// Every case the probe exists for is arranged here rather than on a disk, and
/// that is the whole reason the seam is there: a file that exists and may not be
/// executed is one chmod on Linux and has no spelling at all on Windows, and a
/// model of a plausible size is a megabyte written into a temporary directory on
/// every run of the suite.
///
/// A path nothing was written about is answered as absent, because that is what a
/// file system says about a path nobody created.
/// </remarks>
internal sealed class StubFileFacts : IFileFacts
{
    private readonly Dictionary<string, FileFacts> _byPath = new(StringComparer.Ordinal);

    private readonly bool _neverAnswers;

    private readonly CancellationTokenSource? _stops;

    private StubFileFacts(bool neverAnswers, CancellationTokenSource? stops = null)
    {
        _neverAnswers = neverAnswers;
        _stops = stops;
    }

    /// <summary>
    /// Gets the paths that were asked about, in the order they were asked.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// A file system that has nothing at any path.
    /// </summary>
    public static StubFileFacts Empty() => new(neverAnswers: false);

    /// <summary>
    /// A file system that never comes back, so the probe's own deadline is what
    /// ends the wait.
    /// </summary>
    /// <remarks>
    /// Waits on the token and on nothing else. A delay would read the same way to a
    /// caller and would put a duration nobody chose into the suite, which the
    /// determinism scan refuses by name.
    /// </remarks>
    public static StubFileFacts NeverAnswering() => new(neverAnswers: true);

    /// <summary>
    /// A file system that stops the caller while it is being asked, and then never
    /// answers.
    /// </summary>
    /// <remarks>
    /// The cancellation has to land INSIDE the probe rather than before it. A token
    /// that is already cancelled when the probe is called is refused at its first
    /// line, so a test arranged that way passes whatever the catch does with the two
    /// cases afterwards, which is the shape that hid a missing guard here once.
    /// </remarks>
    public static StubFileFacts StoppingTheCaller(CancellationTokenSource stops) =>
        new(neverAnswers: true, stops);

    /// <summary>
    /// Writes a file at a path.
    /// </summary>
    public StubFileFacts With(string path, long sizeInBytes, bool? isExecutable)
    {
        _byPath[path] = new FileFacts(exists: true, sizeInBytes, isExecutable);

        return this;
    }

    /// <summary>
    /// Writes a tool at a path, of a size nothing here reads.
    /// </summary>
    public StubFileFacts WithTool(string path, bool? isExecutable = true) =>
        With(path, sizeInBytes: 4096, isExecutable);

    /// <summary>
    /// Writes a model at a path, large enough for the probe to believe it.
    /// </summary>
    public StubFileFacts WithModel(string path, long? sizeInBytes = null) =>
        With(path, sizeInBytes ?? (LocalBackendOptions.SmallestPlausibleModelBytes * 40), isExecutable: null);

    public async Task<FileFacts> DescribeAsync(string path, CancellationToken cancellationToken)
    {
        Asked.Add(path);

        if (_stops is not null)
        {
            await _stops.CancelAsync().ConfigureAwait(false);
        }

        if (_neverAnswers)
        {
            var never = new TaskCompletionSource<FileFacts>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cancels = cancellationToken.Register(() => never.TrySetCanceled(cancellationToken));

            return await never.Task.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return _byPath.TryGetValue(path, out var facts)
            ? facts
            : SystemFileFacts.Nothing;
    }
}
