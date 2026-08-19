using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Hygiene;

/// <summary>
/// Reads one pull request out of the environment and reports what each rule
/// decided about it.
/// </summary>
/// <remarks>
/// Everything it reads arrives as an environment variable or as a file the
/// workflow wrote, and nothing is interpolated into a command line anywhere, so a
/// body containing whatever somebody typed cannot become an instruction.
///
/// The failing tier decides the exit code. The advisory tier prints and is read by
/// a person, and a run where every advisory rule objected still ends in nought.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <returns>Nought unless a failing-tier rule was broken.</returns>
    public static int Main()
    {
        var body = Environment.GetEnvironmentVariable("PR_BODY") ?? string.Empty;
        var subjects = LinesOf(Environment.GetEnvironmentVariable("PR_COMMIT_SUBJECTS_FILE"));
        var paths = LinesOf(Environment.GetEnvironmentVariable("PR_CHANGED_PATHS_FILE"));
        var changedLines = NumberIn(Environment.GetEnvironmentVariable("PR_CHANGED_LINES"));

        // The manifest at both ends of the range, because the version and the
        // changelog are two fields of one file and the changed paths cannot tell
        // them apart. The rule refuses a pair it could not read, so a run that was
        // handed nothing here does not read as one that found nothing wrong.
        var baseManifest = TextOf(Environment.GetEnvironmentVariable("PR_BASE_MANIFEST_FILE"));
        var headManifest = TextOf(Environment.GetEnvironmentVariable("PR_HEAD_MANIFEST_FILE"));

        if (subjects.Length == 0)
        {
            // A range that walked no commits is not a clean pull request, it is a
            // check that read nothing, and the two look identical from outside.
            Console.WriteLine("::error::No commit subjects were read, so nothing was judged.");
            return 1;
        }

        var failing = HygieneRules.FailingTier(body, subjects, baseManifest, headManifest);
        var advisory = HygieneRules.AdvisoryTier(paths, changedLines);

        Console.WriteLine("Rules that decide:");
        foreach (var verdict in failing)
        {
            Report(verdict, decides: true);
        }

        Console.WriteLine();
        Console.WriteLine("Rules that annotate and decide nothing:");
        foreach (var verdict in advisory)
        {
            Report(verdict, decides: false);
        }

        var broken = failing.Count(verdict => !verdict.Held);
        if (broken == 0)
        {
            return 0;
        }

        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"::error::{broken} rule(s) that decide were not satisfied."));
        return 1;
    }

    private static void Report(Verdict verdict, bool decides)
    {
        var mark = verdict.Held ? "ok  " : decides ? "FAIL" : "note";
        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"  {mark}  {verdict.Rule}: {verdict.Detail}"));

        if (!verdict.Held && !decides)
        {
            Console.WriteLine(
                string.Create(CultureInfo.InvariantCulture, $"::notice::{verdict.Rule}: {verdict.Detail}"));
        }
    }

    private static string[] LinesOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static string? TextOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return File.ReadAllText(path);
    }

    private static int NumberIn(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
