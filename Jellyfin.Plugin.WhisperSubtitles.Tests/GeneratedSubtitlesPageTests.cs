using System;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Api;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The section of the configuration page that lists what this plugin wrote and
/// removes what was listed: the paths it asks on, what reaches the page as
/// text, and that a removal posts only what was listed and asks first.
/// </summary>
/// <remarks>
/// The page is read as text and its script is not run, so nothing here presses a
/// button. What is held is the page's side of the pairs the controller holds
/// the other side of: the two paths, the two methods, the shape of the body.
/// The names of the page's own functions are matched, so a rename turns this
/// red rather than passing quietly.
/// </remarks>
public sealed class GeneratedSubtitlesPageTests
{
    private const string ListButton = "WhisperSubtitlesListGenerated";

    private const string RemoveButton = "WhisperSubtitlesRemoveGenerated";

    [Fact]
    public void The_page_lists_on_the_path_the_plugin_claims_with_a_get()
    {
        var page = ConfigurationPageSource.Markup();

        Assert.Contains(
            "generatedPath = '" + GeneratedSubtitlesController.ListingPath.TrimStart('/') + "'",
            page,
            StringComparison.Ordinal);
        Assert.Contains("ApiClient.getJSON(ApiClient.getUrl(WhisperSubtitlesConfig.generatedPath))", FunctionBody(page, "WhisperSubtitlesConfig.listGenerated = function"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_removes_on_the_path_the_plugin_claims_with_a_post_carrying_what_was_listed()
    {
        var page = ConfigurationPageSource.Markup();
        var remove = FunctionBody(page, "WhisperSubtitlesConfig.removeGenerated = function");

        Assert.Contains(
            "generatedRemovalPath = '" + GeneratedSubtitlesController.RemovalPath.TrimStart('/') + "'",
            page,
            StringComparison.Ordinal);
        Assert.Contains("type: 'POST'", remove, StringComparison.Ordinal);
        Assert.Contains("ApiClient.getUrl(WhisperSubtitlesConfig.generatedRemovalPath)", remove, StringComparison.Ordinal);
        Assert.Contains("{ Paths: WhisperSubtitlesConfig.offered }", remove, StringComparison.Ordinal);
    }

    [Fact]
    public void What_is_offered_for_removal_is_what_the_listing_showed_as_unchanged()
    {
        var show = FunctionBody(ConfigurationPageSource.Markup(), "WhisperSubtitlesConfig.showGenerated = function");

        Assert.Contains("WhisperSubtitlesConfig.offered = report.Lines", show, StringComparison.Ordinal);
        Assert.Contains("line.State === 'Unchanged'", show, StringComparison.Ordinal);
    }

    [Fact]
    public void A_removal_asks_first_and_posts_nothing_when_nothing_was_offered()
    {
        var remove = FunctionBody(ConfigurationPageSource.Markup(), "WhisperSubtitlesConfig.removeGenerated = function");

        // The condition as written, and not the word: a guard on the word alone
        // passed a page that had the call parked behind a constant.
        Assert.Contains("if (count === 0 || !window.confirm(", remove, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_path_reaches_the_page_as_text_and_never_as_markup()
    {
        // A path comes out of the operator's library, and a name a person chose
        // reaching the page as markup is the shape SECURITY.md asks to be told
        // about.
        var page = ConfigurationPageSource.Markup();
        var show = FunctionBody(page, "WhisperSubtitlesConfig.showGenerated = function");

        Assert.Contains("item.textContent = path", show, StringComparison.Ordinal);
        Assert.Contains("document.createElement('li')", show, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", show, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", FunctionBody(page, "WhisperSubtitlesConfig.listGenerated = function"), StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", FunctionBody(page, "WhisperSubtitlesConfig.removeGenerated = function"), StringComparison.Ordinal);
    }

    [Fact]
    public void Neither_button_is_the_forms_submit_and_neither_step_saves()
    {
        var page = ConfigurationPageSource.Markup();

        foreach (var button in new[] { ListButton, RemoveButton })
        {
            Assert.Matches(
                new Regex(@"<button[^>]*type=""button""[^>]*id=""" + button + @"""", RegexOptions.None, TimeSpan.FromSeconds(5)),
                page);
            Assert.Matches(
                new Regex("#" + button + @"'\)\s*\.addEventListener\('click'", RegexOptions.None, TimeSpan.FromSeconds(5)),
                page);
        }

        Assert.DoesNotContain("updatePluginConfiguration", FunctionBody(page, "WhisperSubtitlesConfig.listGenerated = function"), StringComparison.Ordinal);
        Assert.DoesNotContain("updatePluginConfiguration", FunctionBody(page, "WhisperSubtitlesConfig.removeGenerated = function"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_remove_button_is_hidden_until_a_listing_has_offered_something()
    {
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(
            new Regex(@"<button[^>]*id=""" + RemoveButton + @"""[^>]*style=""display: none""", RegexOptions.None, TimeSpan.FromSeconds(5)),
            page);
        Assert.Contains(
            "remove.style.display = WhisperSubtitlesConfig.offered.length > 0 ? '' : 'none'",
            FunctionBody(page, "WhisperSubtitlesConfig.showGenerated = function"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The text of one of the page's own functions, from its assignment to the
    /// first close of a function at that indentation.
    /// </summary>
    private static string FunctionBody(string page, string opening)
    {
        var at = page.IndexOf(opening, StringComparison.Ordinal);

        Assert.True(at >= 0, $"the page has no function opening with \"{opening}\"");

        var end = page.IndexOf("\n            };", at, StringComparison.Ordinal);

        Assert.True(end > at, $"the function opening with \"{opening}\" never closes");

        return page[at..end];
    }
}
