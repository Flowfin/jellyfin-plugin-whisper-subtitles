using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The local backend's two paths as an operator meets them: typed on a page,
/// written to a file the server keeps, read back on every start.
/// </summary>
/// <remarks>
/// <see cref="LocalWhisperBackendTests"/> judges what the backend does with a path
/// it is given, and this judges how a path gets to it, which is a different set of
/// failures. A field that saves and comes back empty, a setting on the page under a
/// name the configuration does not carry, a value that reaches a process launch with
/// the line break a paste put in it, and a model path an operator is asked for while
/// they are configuring an endpoint somewhere else, are none of them visible to a
/// test of the backend.
///
/// Nothing here looks at a disk. Whether a file is at either path is the readiness
/// probe in #15, and a test asserting it from this side would be asking that
/// question in the second of two places.
/// </remarks>
public class LocalBackendSettingsTests
{
    /// <summary>
    /// The container the page keeps the local backend's own settings in.
    /// </summary>
    private const string LocalSettingsContainer = "WhisperSubtitlesLocalSettings";

    [Fact]
    public void A_fresh_install_names_no_path_rather_than_guessing_one()
    {
        // The state every server starts in. A default naming a usual location would
        // be a plugin launching whatever sits there on a machine where nobody chose
        // it, and the value it would launch is a program.
        var load = ConfigurationValidation.Of(new PluginConfiguration(), processorCount: 4);

        Assert.Empty(load.Complaints);
        Assert.Equal(ConfigurationValidation.NoPathNamed, load.InForce.LocalToolPath);
        Assert.Equal(ConfigurationValidation.NoPathNamed, load.InForce.LocalModelPath);
    }

    [Fact]
    public void A_file_written_before_these_settings_existed_names_no_path()
    {
        // Every configuration this plugin has already written carries neither
        // element, and what this refuses is those files coming back as anything other
        // than an operator who has not typed a path.
        var load = ConfigurationFile.Read(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<PluginConfiguration>"
            + "<SchemaVersion>1</SchemaVersion>"
            + "<Backend>Local</Backend>"
            + "<TargetLanguage>eng</TargetLanguage>"
            + "<LibraryTargets />"
            + "</PluginConfiguration>");

        Assert.Empty(load.Complaints);
        Assert.Equal(ConfigurationValidation.NoPathNamed, load.InForce.LocalToolPath);
        Assert.Equal(ConfigurationValidation.NoPathNamed, load.InForce.LocalModelPath);
    }

    [Theory]
    [InlineData("/usr/local/bin/whisper-cli", "/srv/models/ggml-small.bin")]
    [InlineData("C:\\Tools\\whisper\\main.exe", "C:\\Tools\\whisper\\ggml-tiny.bin")]
    [InlineData("whisper-cli", "ggml-tiny.bin")]
    public void A_path_somebody_typed_reaches_the_run_as_they_typed_it(string tool, string model)
    {
        // The whole point of the setting being a setting, and the bound on what is
        // decided here. A bare name is accepted along with an absolute one: whether
        // either resolves to a file is a question about a disk and is the probe's,
        // and refusing one here would be this plugin being right about a path it
        // never looked at.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { LocalToolPath = tool, LocalModelPath = model },
            processorCount: 4);

        Assert.Empty(load.Complaints);
        Assert.Equal(tool, load.InForce.LocalToolPath);
        Assert.Equal(model, load.InForce.LocalModelPath);
    }

    [Theory]
    [InlineData("  /usr/local/bin/whisper-cli  ")]
    [InlineData("\t/usr/local/bin/whisper-cli\n")]
    public void A_pasted_path_loses_the_whitespace_that_came_with_it(string typed)
    {
        // The failure this trim exists against, and it is the nasty kind: the launch
        // fails on a value the operator is then shown, and what they are shown reads
        // exactly like the path they meant. Every other string this file carries is
        // trimmed, so this is that rule reaching two more fields rather than one of
        // its own.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { LocalToolPath = typed, LocalModelPath = typed },
            processorCount: 4);

        Assert.Empty(load.Complaints);
        Assert.Equal("/usr/local/bin/whisper-cli", load.InForce.LocalToolPath);
        Assert.Equal("/usr/local/bin/whisper-cli", load.InForce.LocalModelPath);
    }

    [Theory]
    [InlineData("/usr/local/bin/whi\nsper-cli")]
    [InlineData("/usr/local/bin/whi\u0000sper-cli")]
    public void A_path_with_a_break_in_the_middle_is_refused_rather_than_handed_on(string typed)
    {
        // The trim above takes a break off the ends and cannot see one in the middle.
        // A value in this shape comes from a paste that wrapped or from the file being
        // edited by hand, no path typed on the page holds one, and what it produces is
        // a process launch failing against a path printed on two lines.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { LocalToolPath = typed },
            processorCount: 4);

        Assert.Equal(ConfigurationValidation.NoPathNamed, load.InForce.LocalToolPath);

        var complaint = Assert.Single(load.Complaints);

        Assert.Equal(nameof(PluginConfiguration.LocalToolPath), complaint.Field);
        Assert.Contains("control character", complaint.Problem, StringComparison.Ordinal);
        Assert.Contains("not configured", complaint.InForce, StringComparison.Ordinal);
    }

    [Fact]
    public void The_complaint_says_which_of_the_two_paths_was_refused()
    {
        // One rule reads both fields, so the name has to be carried in rather than
        // written at the rule. A complaint naming the wrong path sends an operator to
        // repair a field that is fine.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { LocalModelPath = "/srv/mod\nels/ggml-small.bin" },
            processorCount: 4);

        var complaint = Assert.Single(load.Complaints);

        Assert.Equal(nameof(PluginConfiguration.LocalModelPath), complaint.Field);
    }

    [Fact]
    public void Both_paths_survive_the_serializer_the_server_stores_them_with()
    {
        // The server persists this type with XmlSerializer, and a value that does not
        // survive the round trip is a saved setting an operator watches come back
        // empty with nothing in any log.
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var written = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(
            written,
            new PluginConfiguration
            {
                LocalToolPath = "/usr/local/bin/whisper-cli",
                LocalModelPath = "/srv/models/ggml-small.bin",
            });

        using var read = new StringReader(written.ToString());

        // CA5369 asks for the XmlReader overload with DTD processing off. The subject
        // here is the call the server makes, on a string this test wrote a line
        // earlier.
#pragma warning disable CA5369
        var restored = Assert.IsType<PluginConfiguration>(serializer.Deserialize(read));
#pragma warning restore CA5369

        Assert.Equal("/usr/local/bin/whisper-cli", restored.LocalToolPath);
        Assert.Equal("/srv/models/ggml-small.bin", restored.LocalModelPath);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.LocalToolPath))]
    [InlineData(nameof(PluginConfiguration.LocalModelPath))]
    public void The_page_reads_and_writes_each_path(string setting)
    {
        // ConfigurationShellTests compares the two name sets and would catch a setting
        // reaching no page at all. This names which, so a path silently swapped for
        // the other one on the page is a failure here rather than a set that still
        // matches.
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\b", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\s*=", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Contains("id=\"" + setting + "\"", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.LocalToolPath))]
    [InlineData(nameof(PluginConfiguration.LocalModelPath))]
    public void Each_path_is_validated_rather_than_read_straight_off_the_file(string setting)
    {
        // The list the validation publishes is what a run is entitled to assume has
        // been through a rule. A property added to the file and left off that list is
        // one every reader treats as checked while nothing checked it.
        Assert.Contains(setting, ConfigurationValidation.ValidatedFields);
    }

    [Fact]
    public void The_local_settings_are_hidden_rather_than_shown_disabled()
    {
        // The page is read as text here and its script is not run, so what this holds
        // is that the fields sit in a container the script hides and that hiding is
        // what it does to them. A page that disabled them instead, or that left them
        // standing while an endpoint is configured, is caught; a script that hides the
        // wrong element is not, and the page under a booted server is #63.
        var page = ConfigurationPageSource.Markup();

        Assert.Contains("id=\"" + LocalSettingsContainer + "\"", page, StringComparison.Ordinal);
        Assert.Contains("#" + LocalSettingsContainer + "').style.display", page, StringComparison.Ordinal);

        Assert.DoesNotContain("#LocalToolPath').disabled", page, StringComparison.Ordinal);
        Assert.DoesNotContain("#LocalModelPath').disabled", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_offers_the_local_settings_under_the_name_the_plugin_answers_to()
    {
        // The visibility is keyed on a backend name, and a name the page spells its
        // own way is a container that never opens. It is compared against the constant the
        // backend answers to rather than against the markup beside it, because two
        // halves of one page agree with each other while both drift from the code.
        var page = ConfigurationPageSource.Markup();

        Assert.Contains(
            "localBackend: '" + LocalWhisperBackend.BackendName + "'",
            page,
            StringComparison.Ordinal);
    }
}
