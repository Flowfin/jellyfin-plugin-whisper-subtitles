using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// <c>interoperability/claims/jellyfin-plugin-whisper-subtitles.json</c> is what this
/// plugin claims from a server it shares, written where a program that compares it
/// against ten siblings can read it. This compares every entry in it against the
/// value the plugin actually produces, in both directions.
/// </summary>
/// <remarks>
/// A record of what a plugin claims is worth exactly as much as its agreement with
/// the plugin. Left uncompared it is a file somebody wrote once, and the first
/// rename turns it into a scan that reports a collision between two names neither
/// server ever sees, or misses one it does.
///
/// So each leg here takes a value out of the record and one out of the plugin and
/// requires the sets to be equal. A claim that moves without the record moving is
/// red, and a line added to the record that the plugin does not claim is red too.
///
/// TWO VALUES ARE ASSERTED IN A SECOND PLACE AND THAT IS DELIBERATE.
/// <see cref="ClaimedNamesTests"/> holds the task name and the subtitle file name
/// against literals of its own, and <c>SubtitleGenerationTaskTests</c> holds the task
/// key the same way. Both compare against the plugin, as these legs do, so the copies
/// cannot drift apart in silence: a rename reddens every one of them at once. What
/// this file adds is not a second opinion about those strings, it is the same set in
/// the form the comparison in #64 needs, and deleting an older guard to avoid an
/// assertion appearing twice would trade a working rename guard for tidiness.
///
/// WHAT THIS DOES NOT DO. It reads a file and the plugin's own types. Nothing here
/// boots a server, so what the server actually registers under these names is
/// outside it, and nothing here sees a sibling, so a collision between two installed
/// plugins is outside it as well. Those are the first condition of #64 and they need
/// the boot in #63. This is the recording half: it makes the record a reading of the
/// plugin rather than a description of it.
/// </remarks>
public class ClaimRecordTests
{
    /// <summary>
    /// The parts of the recorded subtitle file name that vary per item, and what each
    /// one is filled with to compare the shape against a name the plugin builds.
    /// </summary>
    /// <remarks>
    /// The base name comes off the media file, so the record carries a shape rather
    /// than a name. What this plugin decides is everything around the three
    /// placeholders: the order of the fields, the dots between them, and the marker
    /// sitting where the server's parser leaves it as a title.
    /// </remarks>
    private static readonly (string Placeholder, string Value)[] _nameParts =
    [
        ("<media file base name>", "Arrival (2016)"),
        ("<language>", "en"),
        ("<subtitle extension>", "srt"),
    ];

    /// <summary>
    /// The list the scan reads its kinds of claim out of, in the script itself.
    /// </summary>
    private static readonly Regex KindsTheScanCompares =
        new(@"^kinds=""([^""]+)""\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void The_record_names_this_plugin()
    {
        // Without this the legs below are about a file that could belong to anything,
        // and a scan reading a record that names the wrong repository reports a
        // collision against a claimant nobody can go and fix.
        Assert.Equal("jellyfin-plugin-whisper-subtitles", Record().GetProperty("plugin").GetString());
    }

    [Fact]
    public void The_identity_it_records_is_the_one_the_server_loads_the_plugin_under()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.Equal(
            plugin.Id.ToString(),
            Record().GetProperty("pluginId").GetString(),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_task_keys_it_records_are_the_keys_this_plugin_claims()
    {
        // The key is the row in the dashboard's task list. Two plugins sharing one is
        // a fight over that row, which is the collision this record exists to make
        // visible before a server is booted with both of them on it.
        Assert.Equal([Task().Key], Recorded("taskKeys"));
    }

    [Fact]
    public void The_task_names_it_records_are_the_names_this_plugin_claims()
    {
        Assert.Equal([Task().Name], Recorded("taskNames"));
    }

    [Fact]
    public void The_pages_it_records_are_the_pages_this_plugin_registers()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());

        Assert.Equal(
            plugin.GetPages().Select(page => page.Name).Order(StringComparer.Ordinal).ToArray(),
            Recorded("configurationPages").Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void The_subtitle_file_name_it_records_is_the_name_this_plugin_builds()
    {
        var recorded = Assert.Single(Recorded("subtitleFileNames"));
        var filled = _nameParts.Aggregate(
            recorded,
            (name, part) => name.Replace(part.Placeholder, part.Value, StringComparison.Ordinal));

        // Every placeholder resolved, so a shape that stopped naming one of the three
        // is a comparison against a literal with angle brackets in it rather than a
        // silent pass.
        Assert.DoesNotContain("<", filled, StringComparison.Ordinal);

        Assert.Equal(
            GeneratedSubtitleName.For(
                string.Create(CultureInfo.InvariantCulture, $"/media/Films/{_nameParts[0].Value}/{_nameParts[0].Value}.mkv"),
                _nameParts[1].Value,
                _nameParts[2].Value),
            filled);
    }

    [Fact]
    public void The_route_set_it_records_is_empty_while_this_plugin_answers_none()
    {
        // An empty set is the one that grows in silence, and what holds this one empty
        // is not this leg. RouteClaimsTests reads every source of the plugin and
        // refuses any shape that claims a path from the server, so the first
        // controller added here is red there before this line is stale. What this
        // says is only that the record agrees with that state today.
        Assert.Empty(Recorded("routes"));
    }

    [Fact]
    public void The_path_set_it_records_is_empty_while_this_plugin_fixes_no_location()
    {
        // This plugin writes three kinds of thing and fixes the location of none: the
        // subtitle goes where the library's setting says, the temporary audio into a
        // directory it is handed, and its configuration is written by the server where
        // the server keeps plugin data. There is no literal here for a sibling to
        // collide with. What it writes rather than where is docs/limits.md, read
        // against the sources by WriteLocationsTests.
        Assert.Empty(Recorded("paths"));
    }

    [Fact]
    public void Every_kind_the_scan_compares_is_a_kind_this_record_states()
    {
        // The scan and the record are two files that have to agree about what a claim
        // record holds. A kind added to the scan and not to the record reads as this
        // plugin claiming nothing of that kind, which is the opposite of what the
        // record would be saying, and the scan's own refusal for a missing field
        // cannot fire on a repository that never gained one.
        var record = Record();
        var stated = record.EnumerateObject()
            .Where(field => field.Value.ValueKind == JsonValueKind.Array)
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(KindsTheScanReads().Order(StringComparer.Ordinal).ToArray(), stated);
    }

    [Fact]
    public void The_reader_finds_the_kinds_the_scan_reads()
    {
        // The other side of the same vacuity. A scan this reader finds no kinds in
        // would make the comparison above pass for a record stating none, so it fails
        // here instead, naming the file it could not read them out of.
        Assert.NotEmpty(KindsTheScanReads());
    }

    /// <summary>
    /// The kinds of claim the scan compares, read out of the script rather than
    /// written down here, so the two cannot be made to agree by editing this file.
    /// </summary>
    private static string[] KindsTheScanReads()
    {
        var script = Path.Combine(RepositoryRoot(), ".github", "scripts", "refuse-a-claim-collision.sh");

        foreach (var line in File.ReadLines(script))
        {
            var match = KindsTheScanCompares.Match(line);

            if (match.Success)
            {
                return match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return [];
    }

    private static JsonElement Record()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "interoperability",
            "claims",
            "jellyfin-plugin-whisper-subtitles.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    }

    private static string[] Recorded(string kind) =>
        Record().GetProperty(kind).EnumerateArray().Select(value => value.GetString()!).ToArray();

    private static SubtitleGenerationTask Task() =>
        new(Array.Empty<BackendCandidate>());

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
