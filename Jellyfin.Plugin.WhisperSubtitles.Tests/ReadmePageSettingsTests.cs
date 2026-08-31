using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The README tells a reader which settings the configuration page carries, and
/// that list is compared against the page rather than trusted.
/// </summary>
/// <remarks>
/// THE FAILURE IS ALREADY IN THIS PAGE'S HISTORY AND IT WENT UNNOTICED FOR DAYS.
/// The step said the local tool's executable path and model path were "not on it"
/// and named #36 as where they were held. Both had been on the page since
/// 2026-08-29, put there by #36 itself, so the document describing the page was
/// contradicted by the page in the same tree, and the change that contradicted it
/// had no reason to open a file at the root.
///
/// That is the shape this refuses rather than the one sentence. A setting arriving
/// on the page and a setting leaving it are both caught, because the comparison
/// runs in both directions: a name the page addresses that the README does not
/// carry, and a name the README carries that the page does not address.
///
/// WHAT IT CANNOT SEE. Whether the prose around the list is right. A README naming
/// exactly the right settings under a sentence drawing the wrong conclusion from
/// them passes here, which is the same bound <c>ReadmeClaimsTests</c> states about
/// its own pastes. It also says nothing about what a setting DOES, so a page that
/// renders a field and saves it nowhere is not this reader's subject;
/// <c>ConfigurationShellTests</c> holds the page against the configuration class.
/// </remarks>
public sealed class ReadmePageSettingsTests
{
    /// <summary>
    /// The sentence the list hangs from, matched literally.
    /// </summary>
    /// <remarks>
    /// An anchor rather than a position, so the step can be reworded around it and
    /// can move up or down the page. Deleting the anchor is not a way past this:
    /// a README carrying no such sentence is refused by name below, so the repair
    /// for a step that no longer wants to list settings is to argue that here
    /// rather than to let the reader find nothing and pass.
    /// </remarks>
    private const string Anchor = "What the page carries today is";

    /// <summary>
    /// The page the list is about.
    /// </summary>
    private const string PageSource =
        "Jellyfin.Plugin.WhisperSubtitles/Configuration/configPage.html";

    /// <summary>
    /// A page whose settings are fixed, so a fixture leg is about the README it
    /// varies and never about the tree.
    /// </summary>
    private static IReadOnlyCollection<string> Fixed { get; } = new[]
    {
        "Backend", "TargetLanguage", "LocalToolPath", "LocalModelPath",
    };

    [Fact]
    public void The_readme_names_exactly_the_settings_the_page_carries()
    {
        Assert.Empty(Complaints(Readme(), SettingsThePageCarries(Page())));
    }

    [Fact]
    public void The_reader_finds_the_settings_the_page_actually_addresses()
    {
        // Guards the comparison rather than the page. A reader that came back with
        // nothing would make the leg above pass against a README naming nothing,
        // and the two paths this file exists over are named rather than counted, so
        // a page that stopped addressing them is this leg and not a quieter one.
        var carried = SettingsThePageCarries(Page());

        Assert.Contains("LocalToolPath", carried);
        Assert.Contains("LocalModelPath", carried);
        Assert.Contains("Backend", carried);
    }

    [Fact]
    public void The_reader_finds_the_sentence_the_readme_carries()
    {
        // The other half of the same guard, on the other document.
        Assert.NotEmpty(NamesTheReadmeLists(Readme())!);
    }

    [Fact]
    public void A_readme_naming_exactly_what_the_page_carries_is_accepted()
    {
        Assert.Empty(Complaints(Fixture("clean"), Fixed));
    }

    [Fact]
    public void A_readme_that_leaves_a_setting_out_is_refused()
    {
        var complaints = Complaints(Fixture("leaves-a-setting-out"), Fixed);

        Assert.Contains(complaints, complaint => complaint.Contains("LocalModelPath", StringComparison.Ordinal));
    }

    [Fact]
    public void A_readme_naming_a_setting_the_page_does_not_carry_is_refused()
    {
        var complaints = Complaints(Fixture("names-a-setting-the-page-has-not"), Fixed);

        Assert.Contains(complaints, complaint => complaint.Contains("RemoteEndpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void A_readme_that_speaks_of_the_settings_and_names_none_is_refused()
    {
        // Refused by the same direction as the leg above rather than by a branch of
        // its own. A sentence naming nothing leaves every setting the page carries
        // unnamed, so it is the previous case at its limit; a branch written for it
        // separately could not be watched failing, because the comparison reaches
        // the fixture first.
        Assert.NotEmpty(Complaints(Fixture("names-none-at-all"), Fixed));
    }

    [Fact]
    public void A_readme_carrying_no_such_sentence_is_refused_rather_than_read_as_an_empty_list()
    {
        // The difference between a document that says nothing and a document that
        // says something wrong is the whole of what this leg is for. Without it,
        // deleting the sentence is the cheapest way to turn this file green.
        Assert.NotEmpty(Complaints(Fixture("no-sentence-at-all"), Fixed));
    }

    [Fact]
    public void No_fixture_is_the_README_this_suite_also_reads()
    {
        // A fixture that drifted into being a copy of the real file would make
        // every leg above a second reading of the tree rather than a near miss.
        var readme = Readme();

        foreach (var name in new[]
                 {
                     "clean",
                     "leaves-a-setting-out",
                     "names-a-setting-the-page-has-not",
                     "names-none-at-all",
                     "no-sentence-at-all",
                 })
        {
            Assert.NotEqual(readme, Fixture(name));
        }
    }

    /// <summary>
    /// What the README's list and the page disagree about, in both directions.
    /// </summary>
    /// <param name="readme">The README text.</param>
    /// <param name="carried">The settings the page addresses.</param>
    /// <returns>One complaint per disagreement, and one where there is no list.</returns>
    private static List<string> Complaints(string readme, IReadOnlyCollection<string> carried)
    {
        var listed = NamesTheReadmeLists(readme);

        if (listed is null)
        {
            return
            [
                $"README.md carries no sentence beginning \"{Anchor}\", so nothing here compares it against {PageSource}.",
            ];
        }

        var complaints = new List<string>();

        foreach (var setting in carried.Except(listed, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            complaints.Add($"{PageSource} addresses config.{setting} and README.md does not name it.");
        }

        foreach (var setting in listed.Except(carried, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            complaints.Add($"README.md names {setting} and {PageSource} addresses no such setting.");
        }

        return complaints;
    }

    /// <summary>
    /// The settings the configuration page addresses through the API.
    /// </summary>
    /// <remarks>
    /// The same shape <c>ConfigurationShellTests</c> reads the page with, because
    /// what the page can address is what it writes after <c>config.</c> and nothing
    /// else. A field rendered under a heading and never read into the configuration
    /// is not a setting the page carries, and this reader is right to miss it.
    /// </remarks>
    /// <param name="page">The page markup and script.</param>
    /// <returns>Each setting name, once.</returns>
    private static HashSet<string> SettingsThePageCarries(string page) =>
        Regex.Matches(page, @"\bconfig\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The names the README lists after its anchor sentence.
    /// </summary>
    /// <remarks>
    /// The list ends where the sentence does, so the prose after it saying what is
    /// NOT on the page is outside the reader's reach. Only bare identifiers count:
    /// a backticked path or command carries a character an identifier does not, so
    /// it drops out rather than becoming a complaint about a setting nobody
    /// claimed.
    ///
    /// A README with no such sentence returns null rather than an empty set, and
    /// the two are kept apart on purpose: a document that has stopped describing
    /// the page and one that describes it wrongly want different repairs, and an
    /// empty set would report them as one. A sentence that names nothing is the
    /// second of those, and the comparison refuses it without a branch of its own.
    /// </remarks>
    /// <param name="readme">The README text.</param>
    /// <returns>Each name once, an empty set where the sentence names none, or null where there is no such sentence.</returns>
    private static HashSet<string>? NamesTheReadmeLists(string readme)
    {
        // Whitespace is folded first because the sentence wraps in the file and a
        // literal search would miss it for that alone, which is a reader that
        // passes on every README and says so to nobody.
        var text = Regex.Replace(readme, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5));

        var start = text.IndexOf(Anchor, StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        // The list ends where the sentence does. Anything after it is prose about
        // what is NOT on the page, and reading a backticked word out of that would
        // turn a correct disclosure into a complaint about a setting nobody
        // claimed.
        var rest = text[start..];
        var stop = rest.IndexOf(". ", StringComparison.Ordinal);
        var sentence = stop < 0 ? rest : rest[..stop];

        return Regex.Matches(sentence, @"`([A-Za-z][A-Za-z0-9]*)`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Readme() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));

    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), PageSource.Replace('/', Path.DirectorySeparatorChar)));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "readme-page-settings");

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
