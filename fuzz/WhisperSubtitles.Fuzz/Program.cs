using System;
using System.IO;
using System.Linq;
using SharpFuzz;

namespace Jellyfin.Plugin.WhisperSubtitles.Fuzz;

/// <summary>
/// Runs one target, either under the fuzzer or over a single input.
/// </summary>
/// <remarks>
/// The input arrives on standard input in both modes, so the two read the same
/// bytes through the same code and a seed that behaves one way under the replay
/// behaves the same way under the fuzzer.
///
/// The replay mode is why a change to this harness is checkable without a fuzzer
/// installed. It is not a substitute for the run: it executes each seed once and
/// discovers nothing.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Runs the harness.
    /// </summary>
    /// <param name="args">The target name, and <c>--once</c> to replay one input.</param>
    /// <returns>Nought where the target ran and the properties held.</returns>
    public static int Main(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return Usage("No target was named.");
        }

        if (!FuzzTargets.All.TryGetValue(args[0], out var target))
        {
            return Usage("There is no target called " + args[0] + ".");
        }

        var once = args.Length > 1 && string.Equals(args[1], "--once", StringComparison.Ordinal);

        if (once)
        {
            // Deliberately not SharpFuzz's own single-run helper, which refuses to
            // start unless the assembly under it has been instrumented. Replay has
            // to work on an ordinary build, because the point of it is that a
            // change to this harness is checkable by somebody with no fuzzer
            // installed. It is the same bytes through the same target either way.
            target(ReadAll(Console.OpenStandardInput()));
        }
        else
        {
            Fuzzer.Run(stream => target(ReadAll(stream)));
        }

        return 0;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static int Usage(string problem)
    {
        Console.Error.WriteLine(problem);
        Console.Error.WriteLine("usage: WhisperSubtitles.Fuzz <target> [--once]");
        Console.Error.WriteLine("targets: " + string.Join(", ", FuzzTargets.All.Keys.OrderBy(name => name, StringComparer.Ordinal)));
        return 2;
    }
}
