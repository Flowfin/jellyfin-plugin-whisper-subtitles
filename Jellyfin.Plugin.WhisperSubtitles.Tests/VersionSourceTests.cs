using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The version is written in the manifest and nowhere else, and the properties
/// that stamp it into the assembly derive it from there rather than repeating it.
/// </summary>
/// <remarks>
/// The failure this is written against is a number put back into a second file,
/// which is the state this repository started in: the project properties said
/// 0.0.0.0 and the manifest said 1.0.0.0, and nothing compared them.
///
/// It is not a hypothetical and it is not a mistake somebody has to make by hand.
/// The shared changelog workflow this repository calls carries a release-prep step
/// that rewrites these three properties with a literal, on any repository where
/// the file exists:
///
///     sed -i Directory.Build.props \
///       -e "s;&lt;Version&gt;.*&lt;/Version&gt;;&lt;Version&gt;${VERSION}.0.0.0&lt;/Version&gt;;" \
///       -e "s;&lt;AssemblyVersion&gt;.*&lt;/AssemblyVersion&gt;;&lt;AssemblyVersion&gt;${VERSION}.0.0.0&lt;/AssemblyVersion&gt;;" \
///       -e "s;&lt;FileVersion&gt;.*&lt;/FileVersion&gt;;&lt;FileVersion&gt;${VERSION}.0.0.0&lt;/FileVersion&gt;;"
///
/// and sets the manifest to the same number in the same commit. So the two files
/// agree, the comparison in <c>PluginIdentityTests</c> between the manifest and the
/// stamped assembly passes, the build is green, and the property that the number
/// lives in one place is gone with nothing having said so. That is the shape this
/// leg exists for: a regression whose every other check reports success.
///
/// WHAT THIS DOES NOT DO. It reads the properties and not the build. A property
/// added under a condition, or a version set in a project file rather than here,
/// is outside what it looks at. It says nothing about the manifest either; the
/// manifest is the source and <c>PluginIdentityTests</c> is where what the build
/// did with it is compared.
///
/// A second leg was written here and taken out rather than shipped. It refused a
/// value that derives and then appends, <c>$(PluginVersion).0</c>, on the reading
/// that appending decides part of the number in a second place. That value makes
/// a five-part assembly version and the build refuses it before any test runs, so
/// the leg could not be shown to fail for the reason it names, and a guard nobody
/// has watched fail is not a guard.
/// </remarks>
public sealed class VersionSourceTests
{
    // The three properties the build stamps into the assembly. Read as a set
    // rather than one at a time, so a property that disappeared is a failure
    // rather than a leg that quietly stops asserting anything.
    private static readonly string[] _stampedProperties = ["Version", "AssemblyVersion", "FileVersion"];

    private static readonly Regex _property = new(
        @"<(?<key>Version|AssemblyVersion|FileVersion)>(?<value>[^<]*)</\k<key>>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_stamped_version_property_is_derived_rather_than_written_out()
    {
        foreach (var (key, value) in StampedVersions())
        {
            Assert.True(
                value.Contains("$(", StringComparison.Ordinal),
                $"Directory.Build.props sets {key} to {value}, which is a number written here rather than one derived from the manifest, so the version now lives in two files that nothing compares");
        }
    }

    [Fact]
    public void The_reader_finds_every_property_it_is_written_about()
    {
        // Guards both legs above. A reader that matched nothing would report a file
        // in which no version is written out, whatever the file said, and it would
        // report it in green.
        var found = StampedVersions().Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var key in _stampedProperties)
        {
            Assert.True(found.Contains(key), $"Directory.Build.props carries no {key} property, so nothing above judged it");
        }
    }

    private static List<KeyValuePair<string, string>> StampedVersions() =>
        _property
            .Matches(Read("Directory.Build.props"))
            .Select(match => new KeyValuePair<string, string>(match.Groups["key"].Value, match.Groups["value"].Value.Trim()))
            .ToList();

    // The file a clone checked out. What this is about is the bytes the build
    // reads, and the compiler is what knows where those are. Same reasoning as
    // CommunityFilesTests.
    private static string Read(string relativePath)
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;
        var path = Path.Combine(root, relativePath);

        Assert.True(File.Exists(path), $"{relativePath} was not found, looked in {path}");

        return File.ReadAllText(path);
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
