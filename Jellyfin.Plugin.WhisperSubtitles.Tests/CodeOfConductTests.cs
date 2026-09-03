using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The code of conduct promises no channel this repository does not have, and this
/// refuses a page that names one.
/// </summary>
/// <remarks>
/// A code of conduct's enforcement section is where a contact gets written, and
/// the file was held back on #85 for exactly that line: a contact is a standing
/// promise to read and answer, and this tree held no such contact to name. The
/// decision on #8 settled it in the negative, no second named mailbox beside the
/// advisory route, so the page says where a report goes without naming one.
///
/// What this refuses is the edit that undoes that quietly. A mailbox typed into the
/// page later, by somebody filling what reads like a blank, turns a page that
/// promises nothing it lacks into one that promises a reader somebody has to be.
/// The page also has to say the decision was taken rather than merely omit the
/// contact, because a page that names no channel and gives no reason reads as a
/// page nobody finished, and a reader who takes it for one adds the line this
/// refuses.
///
/// WHAT THIS DOES NOT DO. It reads one shape, an address with an at sign and a
/// domain, so a channel written as prose, a link to a form, or a handle on some
/// other service is invisible to it. It does not read the decision, which is on the
/// tracker and outside what this suite reaches, so a page quoting a decision that
/// has since been reversed passes. And it says nothing about whether the page is
/// any good: what is expected and what happens to a report are judgements a reader
/// makes, and no reading of the bytes makes them.
/// </remarks>
public sealed class CodeOfConductTests
{
    private static readonly Regex _mailbox = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_names_no_mailbox()
    {
        var page = Page();

        Assert.False(
            string.IsNullOrWhiteSpace(page),
            "CODE_OF_CONDUCT.md is empty, so this leg would pass for a page that promises nothing because it says nothing");

        var found = _mailbox.Matches(page);
        Assert.True(
            found.Count == 0,
            found.Count == 0
                ? string.Empty
                : $"CODE_OF_CONDUCT.md names a mailbox, {found[0].Value}, and #8 decided that no second named mailbox is promised beside the advisory route");
    }

    [Fact]
    public void The_page_says_the_absence_is_a_decision_and_where_the_channel_it_defers_to_is_named()
    {
        var page = Page();

        Assert.Contains("#8", page, StringComparison.Ordinal);
        Assert.Contains("`SECURITY.md`", page, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_naming_a_mailbox_is_refused()
    {
        var page = Fixture("names-a-mailbox");

        var found = Assert.Single(_mailbox.Matches(page));
        Assert.Equal("conduct@example.invalid", found.Value);
    }

    [Fact]
    public void A_page_that_names_no_channel_and_gives_no_decision_is_refused()
    {
        var page = Fixture("names-no-decision");

        Assert.Empty(_mailbox.Matches(page));
        Assert.DoesNotContain("#8", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_neighbour_the_fixtures_are_cut_from_passes_both_legs()
    {
        // The two fixtures differ from the real page in one respect each, and the
        // real page is the neighbour: it names no mailbox and it names the
        // decision. Without this the two legs above would pass for a reader that
        // refuses every page.
        var page = Page();

        Assert.Empty(_mailbox.Matches(page));
        Assert.Contains("#8", page, StringComparison.Ordinal);
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "code-of-conduct", name + ".md.fixture"));

    // The file a clone checked out, from the compiler's record of this file's
    // path, for the reason the neighbouring classes give: sources are not copied
    // beside the assembly.
    private static string Page() =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!, "CODE_OF_CONDUCT.md"));

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
