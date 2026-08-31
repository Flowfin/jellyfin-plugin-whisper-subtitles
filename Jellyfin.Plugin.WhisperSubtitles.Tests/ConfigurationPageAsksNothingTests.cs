using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The configuration page tells an operator that the two local paths are not checked
/// here, because nothing on the page asks the backend whether they are any good. That
/// is a claim about the page itself, and this refuses a page that has stopped
/// matching it in either direction.
/// </summary>
/// <remarks>
/// The sentence is what stands between a typed path and a false promise. An operator
/// who types a tool path and a model path and is shown no complaint has been told
/// nothing, and the page says so rather than letting the silence read as approval.
/// The readiness probe that would answer is #15, and the surface that would show its
/// answer is the issue this page belongs to.
///
/// Two changes each break it, and neither would think to open this page's prose. The
/// loud one is the page acquiring a call that asks the server something: the sentence
/// then denies a check the page makes, which is worse than no sentence, because a
/// reader who has been told nothing is asked stops looking for the answer. The quiet
/// one is the sentence being dropped while the page still asks nothing, which leaves
/// two unvalidated paths looking validated.
///
/// WHAT IT COMPARES. The calls the page makes on the server, against the record
/// below, and the presence of the sentence against whether that set has grown. The
/// record is three calls: the library list the per-library rows are built from, and
/// the two that read and write this plugin's own configuration. None of the three
/// asks anything about a backend, which is why the sentence is true while they are
/// the whole set.
///
/// WHAT THIS DOES NOT DO. It does not judge what a call does. A fourth call added
/// tomorrow is refused here whether or not it asks about readiness, and the repair is
/// to record it and to re-read the sentence beside it rather than to widen this. That
/// is deliberate: a vocabulary that tried to recognise a readiness ask would have to
/// know the shape of a route this plugin has not got, and it would pass the first one
/// written in a shape it did not expect.
///
/// It reads the page's text, so a call assembled from parts, or reached through a
/// name this does not match, is invisible to it. <c>RouteClaimsTests</c> holds the
/// other side of the same question - that this plugin answers no path at all - so a
/// readiness route added to the plugin is refused there while the page's own silence
/// is refused here.
///
/// Each leg carries a fixture it has to refuse, under
/// <c>Fixtures/page-asks-nothing/</c>, so the proof it bites is in the tree rather
/// than in the memory of whoever last broke the page on purpose.
/// </remarks>
public class ConfigurationPageAsksNothingTests
{
    /// <summary>
    /// The sentence the page states its own standing with, and the whole of what is
    /// machine-read out of its prose.
    /// </summary>
    private const string AsksNothing = "nothing on this page asks it yet";

    /// <summary>
    /// The calls this page makes on the server, and the record that none of them asks
    /// about a backend. The library list is what the per-library rows are built from,
    /// and the other two read and write this plugin's own configuration.
    /// </summary>
    private static readonly string[] Recorded =
    [
        "getPluginConfiguration",
        "getVirtualFolders",
        "updatePluginConfiguration"
    ];

    /// <summary>
    /// A call on the server's client, as the page writes one.
    /// </summary>
    private static readonly Regex Call = new(
        @"ApiClient\.(?<call>[A-Za-z0-9_]+)\s*\(",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Runs of whitespace, so the sentence is read the same way whether the page wraps
    /// it or keeps it on one line. A rewrap is a change to the markup and never to what
    /// the page says.
    /// </summary>
    private static readonly Regex Whitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_asks_the_server_nothing_new_and_says_so()
    {
        var disagreement = Disagreement(Page());

        Assert.True(
            disagreement.Count == 0,
            $"the configuration page and what it says about itself have come apart: {string.Join("; ", disagreement)}. An operator types two paths there and is shown no complaint, and that sentence is the only thing telling them the silence is not approval.");
    }

    [Fact]
    public void The_reader_finds_the_calls_it_judges()
    {
        // Without this the comparison above passes on a page whose calls stopped being
        // recognised, by finding none and reporting a page that asks nothing whatever
        // the page asked.
        var calls = CallsThePageMakes(Page());

        Assert.True(
            calls.Count >= 3,
            $"the configuration page makes {calls.Count} recognised call(s) on the server and this was written against three: {string.Join(", ", calls)}. A shape that stopped being recognised leaves this reporting a silent page whatever the page does.");
    }

    [Fact]
    public void A_page_that_asks_nothing_new_and_says_so_is_accepted()
    {
        // The neighbour that has to stay accepted. Without it a reader complaining about
        // every page would satisfy each refusal leg below and say nothing about the real
        // one.
        Assert.Empty(Disagreement(Fixture("clean")));
    }

    [Fact]
    public void A_page_that_asks_something_new_and_still_denies_it_is_refused()
    {
        // The loud direction. The sentence then denies a check the page makes, and a
        // reader who has been told nothing is asked stops looking for the answer.
        Assert.Equal(
            ["the page says \"nothing on this page asks it yet\" and calls ApiClient.getJSON"],
            Disagreement(Fixture("asks-something-new")));
    }

    [Fact]
    public void A_page_that_asks_nothing_new_and_stopped_saying_so_is_refused()
    {
        // The quiet direction, and the one nobody would notice. Two unvalidated paths
        // look validated the moment the sentence goes.
        Assert.Equal(
            ["the page asks the server for nothing beyond this plugin's own configuration and no longer says so"],
            Disagreement(Fixture("dropped-the-denial")));
    }

    [Fact]
    public void A_page_that_lost_a_recorded_call_is_refused_rather_than_read_as_a_quieter_one()
    {
        // A record that only ever grows would accept a page that stopped reading its own
        // configuration, and read that as a page asking less. The record is a set rather
        // than a floor.
        Assert.Equal(
            ["this records ApiClient.getVirtualFolders and the page makes no such call"],
            Disagreement(Fixture("lost-a-recorded-call")));
    }

    [Fact]
    public void No_fixture_is_a_page_anything_else_reads()
    {
        // Every fixture here is a configuration page that is deliberately wrong, and each
        // is kept under an extension nothing that walks this plugin's pages opens.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !path.EndsWith("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".html.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    /// <summary>
    /// Everything the page and the record disagree about.
    /// </summary>
    /// <param name="page">The page's markup.</param>
    /// <returns>The complaints, ordered so a failure names them the same way twice.</returns>
    private static List<string> Disagreement(string page)
    {
        var calls = CallsThePageMakes(page);
        var unrecorded = calls.Except(Recorded, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var says = Whitespace.Replace(page, " ").Contains(AsksNothing, StringComparison.Ordinal);
        var complaints = new List<string>();

        if (says && unrecorded.Count > 0)
        {
            complaints.AddRange(unrecorded.Select(call =>
                $"the page says \"{AsksNothing}\" and calls ApiClient.{call}"));
        }

        if (!says && unrecorded.Count == 0)
        {
            complaints.Add(
                "the page asks the server for nothing beyond this plugin's own configuration and no longer says so");
        }

        complaints.AddRange(Recorded
            .Except(calls, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(call => $"this records ApiClient.{call} and the page makes no such call"));

        return complaints;
    }

    /// <summary>
    /// The distinct calls a page makes on the server's client.
    /// </summary>
    /// <param name="page">The page's markup.</param>
    /// <returns>The call names, ordered.</returns>
    private static List<string> CallsThePageMakes(string page) =>
        Call.Matches(page)
            .Select(match => match.Groups["call"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string Page() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "Jellyfin.Plugin.WhisperSubtitles",
            "Configuration",
            "configPage.html"));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".html.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(ProjectDirectory(), "Fixtures", "page-asks-nothing");

    private static string ProjectDirectory() =>
        Path.GetDirectoryName(ThisFile())!;

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(ProjectDirectory())!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
