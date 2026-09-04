using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The backend guide tells an operator what stands in front of them when they choose
/// the remote backend, and this refuses that sentence where the configuration page
/// disagrees with it in either direction.
/// </summary>
/// <remarks>
/// The failure it is written against had already happened. The guide said the
/// disclosure this plugin owes before the remote backend can be switched on "is not
/// written yet, so an operator choosing Remote today is choosing it with less in
/// front of them than this repository intends to put there". The disclosure landed on
/// the configuration page on 2026-09-03, held by five legs of
/// <c>RemoteBackendSettingsTests</c>, and the sentence survived it: a reader was told
/// the statement was missing while it was on the page they were about to open. That
/// is worse than a page that never made the claim, because a denial is read as a
/// reading.
///
/// Nothing here could have caught it. <c>GuidePasteTests</c> re-runs the searches this
/// page quotes and states its own bound, which is that it compares lines and has no
/// opinion about the prose beside them, and the guide quoted no search over the
/// configuration page at all. A paste cannot see a claim about something the page
/// never mentions.
///
/// So the two directions are held separately. A disclosure arriving while the guide
/// says nothing about one is this class; a paste that stops reproducing under a
/// sentence is <c>GuidePasteTests</c>; and a guide that keeps the sentence after
/// deleting the paste is this class again, because that suite's floor is a count and
/// one search fewer still clears it.
///
/// WHAT THIS DOES NOT DO. It compares the presence of the element against a mention of
/// it, so it says nothing about whether the prose around the mention is true - a
/// paragraph naming the block while describing it wrongly passes here, and the review
/// is where that is caught. What it reads on the tree side is the block's identifier
/// in the page an assembly carries, so a disclosure rewritten under another identifier
/// reads here as one that has gone, which is the direction that fails towards somebody
/// opening the guide again. It has no opinion about the log line the same three facts
/// are owed in, which is the half of #81 that no configuration page can carry.
/// </remarks>
public class GuideRemoteDisclosureTests
{
    /// <summary>
    /// The page this reads, relative to the repository root.
    /// </summary>
    private const string PageName = "docs/choosing-a-backend.md";

    /// <summary>
    /// The block on the configuration page that states where the audio goes, as the
    /// page identifies it.
    /// </summary>
    private const string DisclosureElement = "id=\"WhisperSubtitlesRemoteDisclosure\"";

    /// <summary>
    /// The identifier alone, which is what the guide names when it points a reader at
    /// that block.
    /// </summary>
    private const string DisclosureName = "WhisperSubtitlesRemoteDisclosure";

    [Fact]
    public void The_guide_points_at_the_disclosure_the_configuration_page_carries()
    {
        // The direction that went wrong: the statement arrives on the page and the
        // guide goes on telling a reader it is missing. Whoever adds one is turned
        // back to this page, which is where the sentence that has to move lives.
        ConfigurationPageSource.RefuseUnlessAnOperatorChoosesTheBackendOnIt();

        if (!ConfigurationPageSource.Markup().Contains(DisclosureElement, StringComparison.Ordinal))
        {
            return;
        }

        Assert.True(
            Page().Contains(DisclosureName, StringComparison.Ordinal),
            $"the configuration page carries {DisclosureElement} and {PageName} names it nowhere, so the guide is describing what an operator choosing the remote backend meets without having read it.");
    }

    [Fact]
    public void The_guide_points_at_no_disclosure_the_configuration_page_has_lost()
    {
        // The other direction. The guide is the page an operator is sent to before
        // they decide, so a promise here that the page no longer keeps is the same
        // defect pointed the other way.
        ConfigurationPageSource.RefuseUnlessAnOperatorChoosesTheBackendOnIt();

        if (!Page().Contains(DisclosureName, StringComparison.Ordinal))
        {
            return;
        }

        Assert.True(
            ConfigurationPageSource.Markup().Contains(DisclosureElement, StringComparison.Ordinal),
            $"{PageName} names {DisclosureName} and the configuration page this plugin registers carries no such block, so the guide promises a statement an operator will not meet.");
    }

    /// <summary>
    /// The page, read out of the checkout rather than out of a copy beside the
    /// assembly, for the reason its neighbours in this suite give: the thing the
    /// claim is about is the file a reader opens.
    /// </summary>
    /// <returns>The whole page.</returns>
    private static string Page() => File.ReadAllText(Path.Combine(RepositoryRoot(), PageName));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
