using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Reading the configuration answers a question about it and changes nothing in
/// it.
/// </summary>
/// <remarks>
/// #41 asks that a file a later release wrote is refused without being modified,
/// and the thing that can be modified is the object the server holds. The server
/// deserialises the file once, keeps that object, and writes that same object
/// back whenever anything asks it to save. A read that repaired a value in place
/// would therefore put this release's defaults into the server's own copy, and
/// the next save would write them over the file this release declined to read.
/// Nothing would report it: the load would still complain about the version, a
/// run would still do nothing, and the file would still be gone.
///
/// The route is not hypothetical. <see cref="PluginConfiguration.TargetLanguagesByLibrary"/>
/// hands the live configuration to the same reader, so every caller that asks for
/// the per-library targets passes the server's object through the code these
/// tests hold.
///
/// WHAT THIS DOES NOT REACH. The other way to the same loss is the server writing
/// the file because something posted a configuration to it, and no test here can
/// refuse that, because the write is the server's own. The surface that posts is
/// #36 and that half stays where #41 leaves it. What is held here is this
/// plugin's hands off the object it was given.
///
/// The comparison proves itself in the last leg rather than being trusted. A
/// snapshot that read nothing, or one that compared two descriptions no change
/// could move apart, would pass every leg above it for free, which is the shape
/// this file would otherwise take.
/// </remarks>
public class ConfigurationLeftUntouchedTests
{
    private const string NotALibrary = "not a library";

    [Fact]
    public void The_reader_sees_every_setting_the_configuration_declares()
    {
        // Guards every leg below. A snapshot over no properties compares nothing
        // against nothing and reports an untouched configuration whatever the
        // reader did to it.
        var seen = Snapshot(Written()).Keys;

        Assert.Contains(nameof(PluginConfiguration.SchemaVersion), seen);
        Assert.Contains(nameof(PluginConfiguration.Backend), seen);
        Assert.Contains(nameof(PluginConfiguration.TargetLanguage), seen);
        Assert.Contains(nameof(PluginConfiguration.LibraryTargets), seen);

        Assert.Equal(
            ConfigurationValidation.ValidatedFields.OrderBy(name => name, StringComparer.Ordinal),
            seen.Where(ConfigurationValidation.ValidatedFields.Contains).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void A_file_a_newer_release_wrote_is_left_exactly_as_it_was()
    {
        // The case #41 names. Every value here is one this release would refuse if
        // it read the file at all, so a reader that repaired as it went would have
        // the most to change on exactly this configuration.
        var configuration = Written();
        configuration.SchemaVersion = ConfigurationValidation.CurrentSchemaVersion + 1;

        var before = Snapshot(configuration);
        var load = ConfigurationValidation.Of(configuration);

        Assert.NotEmpty(load.Complaints);
        Assert.Equal(before, Snapshot(configuration));
    }

    [Fact]
    public void A_file_this_release_can_read_is_left_exactly_as_it_was()
    {
        // The neighbouring case, and it is the one that would move if a fallback
        // were written back rather than merely reported: this file is read field by
        // field and every field fails its rule.
        var configuration = Written();

        var before = Snapshot(configuration);
        var load = ConfigurationValidation.Of(configuration);

        Assert.NotEmpty(load.Complaints);
        Assert.Equal(before, Snapshot(configuration));
    }

    [Fact]
    public void A_file_with_nothing_wrong_with_it_is_left_alone_too()
    {
        // Without this the two legs above would be satisfied by a reader that only
        // leaves a configuration alone when it has already given up on it.
        var configuration = new PluginConfiguration
        {
            SchemaVersion = ConfigurationValidation.CurrentSchemaVersion,
            Backend = LocalWhisperBackend.BackendName,
            TargetLanguage = "eng",
            LibraryTargets =
            [
                new LibraryLanguageTarget
                {
                    LibraryId = "2b4a3f10-6c1e-4f2a-9a55-0f1b2c3d4e5f",
                    Target = "deu"
                }
            ]
        };

        var before = Snapshot(configuration);
        var load = ConfigurationValidation.Of(configuration);

        Assert.Empty(load.Complaints);
        Assert.Equal(before, Snapshot(configuration));
    }

    [Fact]
    public void The_rows_are_left_alone_inside_the_array_as_well_as_beside_it()
    {
        // A row is a class with settable properties, so the cheapest repair anybody
        // would write is one that fixes a row in place and leaves the array where it
        // was. The snapshot describes the rows rather than the array, and the array
        // instance is compared separately so a reader that replaced the whole
        // property with a cleaned copy is caught as well.
        var configuration = Written();
        var rows = configuration.LibraryTargets;

        var before = Snapshot(configuration);
        ConfigurationValidation.Of(configuration);

        Assert.Same(rows, configuration.LibraryTargets);
        Assert.Equal(NotALibrary, configuration.LibraryTargets[0].LibraryId);
        Assert.Equal(before, Snapshot(configuration));
    }

    [Fact]
    public void The_comparison_would_see_a_change_it_was_shown()
    {
        // The near-miss for the legs above, and the reason they are worth anything.
        // Each setting is moved on its own, so a description that collapsed a field
        // into a constant, or a snapshot that dropped one, fails here instead of
        // reporting every configuration untouched forever.
        var configuration = Written();
        var before = Snapshot(configuration);

        configuration.SchemaVersion += 1;
        Assert.NotEqual(before, Snapshot(configuration));
        configuration.SchemaVersion -= 1;

        configuration.Backend = LocalWhisperBackend.BackendName;
        Assert.NotEqual(before, Snapshot(configuration));
        configuration.Backend = "whisperx";

        configuration.TargetLanguage = "eng";
        Assert.NotEqual(before, Snapshot(configuration));
        configuration.TargetLanguage = "klingon";

        configuration.LibraryTargets[0].Target = "deu";
        Assert.NotEqual(before, Snapshot(configuration));
        configuration.LibraryTargets[0].Target = "klingon";

        // And back where it started, so the leg proves the comparison moves in both
        // directions rather than only away from where it began.
        Assert.Equal(before, Snapshot(configuration));
    }

    /// <summary>
    /// A configuration wrong in every field a rule decides, so a reader that
    /// repaired anything has something to repair.
    /// </summary>
    private static PluginConfiguration Written() => new()
    {
        SchemaVersion = ConfigurationValidation.CurrentSchemaVersion,
        Backend = "whisperx",
        TargetLanguage = "klingon",
        LibraryTargets =
        [
            new LibraryLanguageTarget
            {
                LibraryId = NotALibrary,
                Target = "klingon"
            }
        ]
    };

    private static Dictionary<string, string> Snapshot(PluginConfiguration configuration) =>
        Settings().ToDictionary(
            setting => setting.Name,
            setting => Describe(setting.GetValue(configuration)),
            StringComparer.Ordinal);

    /// <summary>
    /// Every setting the configuration declares, found rather than listed, so a
    /// field added with a later feature is covered on the day it arrives.
    /// </summary>
    private static IEnumerable<PropertyInfo> Settings() =>
        typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(setting => setting.CanRead && setting.CanWrite)
            .OrderBy(setting => setting.Name, StringComparer.Ordinal);

    private static string Describe(object? value) => value switch
    {
        null => "<absent>",
        LibraryLanguageTarget[] rows => string.Join("|", rows.Select(Describe)),
        LibraryLanguageTarget row => row.LibraryId + "=" + row.Target,
        string text => text,
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "<absent>",
    };
}
