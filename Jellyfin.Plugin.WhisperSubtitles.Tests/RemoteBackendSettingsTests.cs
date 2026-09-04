using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The remote backend's three settings as an operator meets them: typed on a page,
/// written to a file the server keeps, read back on every start, with a statement
/// of where the audio goes standing beside them for as long as that backend is the
/// one chosen.
/// </summary>
/// <remarks>
/// <see cref="RemoteWhisperBackendTests"/> judges what the backend does with a URL
/// and a key it is given, and this judges how they get to it, which is a different
/// set of failures: a field that saves and comes back empty, a URL that is not one
/// the backend could post to reaching the file as though it were, a key with the
/// line break a paste put in it, and a URL an operator is asked for while they are
/// configuring a program on this machine.
///
/// THE DISCLOSURE IS THE HALF WITH NO COUNTERPART ON THE LOCAL SIDE. Selecting this
/// backend sends the audio of every selected item to a host somebody typed, and
/// the page says so beside the field, naming the host out of the URL. What is held
/// here is that the statement is inside the container the remote backend's choice
/// shows and hides, that nothing else hides it, that its host is filled from the
/// URL field, and that the key field is read by nothing the page displays. The
/// page is read as text and its script is not run, so a script that filled the
/// host from the wrong field by a route this does not match is not seen; the page
/// under a server that booted is #63.
///
/// Nothing here reaches a network. Whether the host answers, accepts the key or
/// serves the model is the readiness probe in #15.
/// </remarks>
public class RemoteBackendSettingsTests
{
    /// <summary>
    /// The container the page keeps the remote backend's own settings and the
    /// disclosure in.
    /// </summary>
    private const string RemoteSettingsContainer = "WhisperSubtitlesRemoteSettings";

    /// <summary>
    /// The element the disclosure names the host in.
    /// </summary>
    private const string HostElement = "WhisperSubtitlesRemoteHost";

    /// <summary>
    /// The block the disclosure is written in. It is named here because the
    /// backend's own remark names it too, and the two are compared.
    /// </summary>
    private const string DisclosureElement = "WhisperSubtitlesRemoteDisclosure";

    /// <summary>
    /// The issue that owes the disclosure and the log line carrying the same facts.
    /// </summary>
    private const string DisclosureIssue = "#81";

    [Fact]
    public void A_fresh_install_names_no_endpoint_rather_than_guessing_one()
    {
        // The state every server starts in. A default naming any host would be a
        // plugin sending audio to a machine nobody chose.
        var load = ConfigurationValidation.Of(new PluginConfiguration(), processorCount: 4);

        Assert.Empty(load.Complaints);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteBaseUrl);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteApiKey);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteModel);
    }

    [Fact]
    public void A_file_written_before_these_settings_existed_names_no_endpoint()
    {
        // Every configuration this plugin has already written carries none of the
        // three elements, and what this refuses is those files coming back as
        // anything other than an operator who has not typed an endpoint.
        var load = ConfigurationFile.Read(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<PluginConfiguration>"
            + "<SchemaVersion>1</SchemaVersion>"
            + "<Backend>Remote</Backend>"
            + "<TargetLanguage>eng</TargetLanguage>"
            + "<LibraryTargets />"
            + "</PluginConfiguration>");

        Assert.Empty(load.Complaints);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteBaseUrl);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteApiKey);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteModel);
    }

    [Theory]
    [InlineData("https://whisper.example.net", "sk-not-a-real-key", "whisper-1")]
    [InlineData("http://10.0.0.7:9000/v1", "", "large-v3")]
    [InlineData("https://api.example.com/v1/audio/transcriptions", "anything", "whisper-1")]
    public void A_value_somebody_typed_reaches_the_run_as_they_typed_it(string url, string key, string model)
    {
        // The three URL shapes the backend accepts, which is the bound on what is
        // decided here: the same rule the backend applies before it posts, and no
        // second one about the host.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { RemoteBaseUrl = url, RemoteApiKey = key, RemoteModel = model },
            processorCount: 4);

        Assert.Empty(load.Complaints);
        Assert.Equal(url, load.InForce.RemoteBaseUrl);
        Assert.Equal(key, load.InForce.RemoteApiKey);
        Assert.Equal(model, load.InForce.RemoteModel);
    }

    [Theory]
    [InlineData("  https://whisper.example.net  ")]
    [InlineData("\thttps://whisper.example.net\n")]
    public void A_pasted_value_loses_the_whitespace_that_came_with_it(string typed)
    {
        // Every other string this file carries is trimmed, so this is that rule
        // reaching three more fields rather than one of its own. For the key the
        // failure it stops is a request header ending in a line break.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { RemoteBaseUrl = typed, RemoteApiKey = typed, RemoteModel = typed },
            processorCount: 4);

        Assert.Empty(load.Complaints);
        Assert.Equal("https://whisper.example.net", load.InForce.RemoteBaseUrl);
        Assert.Equal("https://whisper.example.net", load.InForce.RemoteApiKey);
        Assert.Equal("https://whisper.example.net", load.InForce.RemoteModel);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.RemoteBaseUrl))]
    [InlineData(nameof(PluginConfiguration.RemoteApiKey))]
    [InlineData(nameof(PluginConfiguration.RemoteModel))]
    public void A_value_with_a_break_in_the_middle_is_refused_naming_the_field(string setting)
    {
        // The trim takes a break off the ends and cannot see one in the middle. A
        // key in this shape is a header the client refuses to send, and the refusal
        // prints the key; a URL in this shape is refused by the URL parser with the
        // URL printed on two lines.
        var configuration = new PluginConfiguration();
        typeof(PluginConfiguration).GetProperty(setting)!.SetValue(configuration, "https://whisper.exa\nmple.net");

        var load = ConfigurationValidation.Of(configuration, processorCount: 4);

        var complaint = Assert.Single(load.Complaints);

        Assert.Equal(setting, complaint.Field);
        Assert.Contains("control character", complaint.Problem, StringComparison.Ordinal);
        Assert.Equal(
            ConfigurationValidation.NoRemoteSettingNamed,
            typeof(SettingsInForce).GetProperty(setting)!.GetValue(load.InForce));
    }

    [Theory]
    [InlineData("whisper.example.net")]
    [InlineData("ftp://whisper.example.net")]
    [InlineData("/v1/audio/transcriptions")]
    public void A_url_the_backend_could_not_post_to_is_refused_with_the_backends_own_sentence(string typed)
    {
        // One rule rather than two. The sentence in the complaint is the one the
        // backend gives for the same value, so what an operator may save and what
        // the backend would refuse to send to cannot come apart.
        var load = ConfigurationValidation.Of(
            new PluginConfiguration { RemoteBaseUrl = typed },
            processorCount: 4);

        Assert.False(new RemoteBackendOptions(typed, null, null).TryGetEndpoint(out _, out var problem));

        var complaint = Assert.Single(load.Complaints);

        Assert.Equal(nameof(PluginConfiguration.RemoteBaseUrl), complaint.Field);
        Assert.Equal(problem, complaint.Problem);
        Assert.Contains("not configured", complaint.InForce, StringComparison.Ordinal);
        Assert.Equal(ConfigurationValidation.NoRemoteSettingNamed, load.InForce.RemoteBaseUrl);
    }

    [Fact]
    public void All_three_survive_the_serializer_the_server_stores_them_with()
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
                RemoteBaseUrl = "https://whisper.example.net/v1",
                RemoteApiKey = "sk-not-a-real-key",
                RemoteModel = "whisper-1",
            });

        using var read = new StringReader(written.ToString());

        // CA5369 asks for the XmlReader overload with DTD processing off. The subject
        // here is the call the server makes, on a string this test wrote a line
        // earlier.
#pragma warning disable CA5369
        var restored = Assert.IsType<PluginConfiguration>(serializer.Deserialize(read));
#pragma warning restore CA5369

        Assert.Equal("https://whisper.example.net/v1", restored.RemoteBaseUrl);
        Assert.Equal("sk-not-a-real-key", restored.RemoteApiKey);
        Assert.Equal("whisper-1", restored.RemoteModel);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.RemoteBaseUrl))]
    [InlineData(nameof(PluginConfiguration.RemoteApiKey))]
    [InlineData(nameof(PluginConfiguration.RemoteModel))]
    public void The_page_reads_and_writes_each_setting(string setting)
    {
        // ConfigurationShellTests compares the two name sets and would catch a setting
        // reaching no page at all. This names which, so a value silently swapped for
        // another on the page is a failure here rather than a set that still matches.
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\b", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Matches(new Regex(@"\bconfig\." + setting + @"\s*=", RegexOptions.None, TimeSpan.FromSeconds(5)), page);
        Assert.Contains("id=\"" + setting + "\"", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(PluginConfiguration.RemoteBaseUrl))]
    [InlineData(nameof(PluginConfiguration.RemoteApiKey))]
    [InlineData(nameof(PluginConfiguration.RemoteModel))]
    public void Each_setting_is_validated_rather_than_read_straight_off_the_file(string setting)
    {
        Assert.Contains(setting, ConfigurationValidation.ValidatedFields);
    }

    [Fact]
    public void The_key_field_does_not_show_what_is_typed_into_it()
    {
        // A secret typed on a dashboard page somebody else can see over a shoulder.
        // The browser is what hides it, and this holds only that the page asks it to.
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(
            new Regex(@"<input[^>]*type=""password""[^>]*id=""RemoteApiKey""", RegexOptions.None, TimeSpan.FromSeconds(5)),
            page);
    }

    [Fact]
    public void The_remote_settings_are_hidden_rather_than_shown_disabled()
    {
        // The page is read as text here and its script is not run, so what this holds
        // is that the fields sit in a container the script hides and that hiding is
        // what it does to them.
        var page = ConfigurationPageSource.Markup();

        Assert.Contains("id=\"" + RemoteSettingsContainer + "\"", page, StringComparison.Ordinal);
        Assert.Contains("#" + RemoteSettingsContainer + "').style.display", page, StringComparison.Ordinal);

        Assert.DoesNotContain("#RemoteBaseUrl').disabled", page, StringComparison.Ordinal);
        Assert.DoesNotContain("#RemoteApiKey').disabled", page, StringComparison.Ordinal);
        Assert.DoesNotContain("#RemoteModel').disabled", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_offers_the_remote_settings_under_the_name_the_plugin_answers_to()
    {
        // The visibility is keyed on a backend name, and a name the page spells its
        // own way is a container that never opens.
        var page = ConfigurationPageSource.Markup();

        Assert.Contains(
            "remoteBackend: '" + RemoteWhisperBackend.BackendName + "'",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_disclosure_sits_inside_the_container_the_remote_choice_shows()
    {
        // Next to the remote backend's fields, and shown and hidden with them, which
        // is what puts it in front of whoever has that backend chosen and nowhere
        // else.
        var page = ConfigurationPageSource.Markup();

        var container = page.IndexOf("id=\"" + RemoteSettingsContainer + "\"", StringComparison.Ordinal);
        var disclosure = page.IndexOf("id=\"WhisperSubtitlesRemoteDisclosure\"", StringComparison.Ordinal);
        var next = page.IndexOf("id=\"ItemsAtOnce\"", StringComparison.Ordinal);

        Assert.True(container >= 0, "the page has no remote settings container");
        Assert.True(disclosure > container, "the disclosure is not after the remote settings container opens");
        Assert.True(disclosure < next, "the disclosure is not before the next setting on the page, so it is outside the remote container");
    }

    [Fact]
    public void The_disclosure_states_the_three_facts()
    {
        // What leaves, where it goes, and what this plugin cannot know. The words are
        // held loosely, because the sentence is for an operator and is allowed to be
        // reworded; what is held is that each of the three is there.
        var disclosure = Disclosure(ConfigurationPageSource.Markup());

        Assert.Contains("audio", disclosure, StringComparison.Ordinal);
        Assert.Contains("leaves this server", disclosure, StringComparison.Ordinal);
        Assert.Contains("id=\"" + HostElement + "\"", disclosure, StringComparison.Ordinal);
        Assert.Contains("cannot know", disclosure, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_dismisses_the_disclosure_but_choosing_another_backend()
    {
        // The one thing that hides the container is the backend choice. A second
        // assignment to its display, or a control inside it, would be a way of
        // leaving the fields in front of an operator with the statement gone.
        var page = ConfigurationPageSource.Markup();

        var hides = Regex.Matches(
            page,
            "#" + RemoteSettingsContainer + @"'\)\.style\.display",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.Single(hides);

        var line = LineHolding(page, hides[0].Index + hides[0].Length);
        Assert.Contains("WhisperSubtitlesConfig.remoteBackend", line, StringComparison.Ordinal);

        var disclosure = Disclosure(page);
        Assert.DoesNotContain("<button", disclosure, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", disclosure, StringComparison.Ordinal);
    }

    [Fact]
    public void The_host_in_the_disclosure_is_read_out_of_the_url_field()
    {
        // The statement names where the audio would go rather than restating that it
        // goes somewhere, and the host comes out of the URL an operator typed rather
        // than out of anything else on the page.
        var page = ConfigurationPageSource.Markup();

        var render = page.IndexOf("WhisperSubtitlesConfig.renderDisclosure = function", StringComparison.Ordinal);
        Assert.True(render >= 0, "the page has no function that renders the disclosure");

        var body = page.Substring(render, page.IndexOf("};", render, StringComparison.Ordinal) - render);

        Assert.Contains("#" + HostElement + "').textContent", body, StringComparison.Ordinal);
        Assert.Contains("hostOf(document.querySelector('#RemoteBaseUrl').value)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoteApiKey", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoteModel", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_key_is_read_by_nothing_the_page_displays()
    {
        // The key goes into the configuration and nowhere else on the page. Every
        // line of the script that names the key field is a line that reads or writes
        // the field itself, and none of them puts a value into anything shown.
        var page = ConfigurationPageSource.Markup();

        var script = page.Substring(page.IndexOf("<script", StringComparison.Ordinal));

        var naming = script
            .Split('\n')
            .Where(line => line.Contains("RemoteApiKey", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(naming);

        foreach (var line in naming)
        {
            Assert.DoesNotContain("textContent", line, StringComparison.Ordinal);
            Assert.DoesNotContain("innerHTML", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Disclosure", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Host", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_page_says_none_of_the_three_is_checked_there()
    {
        // The same sentence the local paths carry, for the same reason: an operator
        // shown no complaint has been told nothing, and the page says so rather than
        // letting the silence read as approval.
        var page = ConfigurationPageSource.Markup();
        var container = page.Substring(page.IndexOf("id=\"" + RemoteSettingsContainer + "\"", StringComparison.Ordinal));
        var text = Regex.Replace(container, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.Contains("None of the three is checked here.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_backend_points_at_the_block_the_page_carries_rather_than_at_an_issue()
    {
        // The remark on the class whose requests carry the audio is where somebody
        // reading that code meets this. It handed the saying-so to an issue while the
        // page already said it, which reads as a plugin that has told an operator
        // nothing yet, and the block it names is compared against the page rather
        // than taken on trust.
        var remark = BackendRemark();

        Assert.Contains(DisclosureElement, remark, StringComparison.Ordinal);
        Assert.Contains(
            "id=\"" + DisclosureElement + "\"",
            ConfigurationPageSource.Markup(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_backend_names_the_disclosure_issue_only_beside_the_half_it_still_owes()
    {
        // That issue is two halves of the same three facts and one of them is built.
        // A paragraph handing it the whole of them is the sentence this leg keeps out
        // of the source, and the half that is left is the log line, so a paragraph
        // naming the issue says so.
        var naming = Paragraphs(BackendRemark())
            .Where(paragraph => paragraph.Contains(DisclosureIssue, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(naming);

        Assert.All(
            naming,
            paragraph => Assert.Contains("log line", paragraph, StringComparison.Ordinal));
    }

    /// <summary>
    /// The remark on the remote backend, out of the checkout rather than out of a
    /// copy beside the assembly, which the neighbouring scanners read the same way.
    /// </summary>
    /// <returns>The remark, with its comment markers taken off.</returns>
    private static string BackendRemark()
    {
        var source = File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!,
            "Jellyfin.Plugin.WhisperSubtitles",
            "Backends",
            "Remote",
            "RemoteWhisperBackend.cs"));

        var start = source.IndexOf("/// <remarks>", StringComparison.Ordinal);
        var end = source.IndexOf("/// </remarks>", StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "the remote backend carries no remark");

        return source.Substring(start, end - start);
    }

    /// <summary>
    /// The paragraphs of a remark, which are what a separator line divides.
    /// </summary>
    /// <param name="remark">The remark.</param>
    /// <returns>One entry per paragraph, as one line each.</returns>
    private static List<string> Paragraphs(string remark) =>
        remark
            .Split('\n')
            .Select(line => line.Trim().TrimStart('/').Trim())
            .Aggregate(
                new List<string> { string.Empty },
                (paragraphs, line) =>
                {
                    if (line.Length == 0)
                    {
                        paragraphs.Add(string.Empty);
                    }
                    else
                    {
                        paragraphs[^1] = (paragraphs[^1] + " " + line).Trim();
                    }

                    return paragraphs;
                })
            .Where(paragraph => paragraph.Length > 0)
            .ToList();

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static string Disclosure(string page)
    {
        var start = page.IndexOf("id=\"WhisperSubtitlesRemoteDisclosure\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "the page has no disclosure element");

        var end = page.IndexOf("</div>", start, StringComparison.Ordinal);

        return page.Substring(start, end - start);
    }

    private static string LineHolding(string page, int index)
    {
        var start = page.LastIndexOf('\n', index) + 1;
        var end = page.IndexOf(';', index);

        return page.Substring(start, end - start);
    }
}
