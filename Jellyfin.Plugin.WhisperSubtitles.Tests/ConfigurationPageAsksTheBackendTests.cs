using System;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Api;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The configuration page asks the chosen backend whether it is ready, about the
/// settings as they stand on the page, on the one path this plugin claims, and
/// shows the answer as text. This refuses the page coming apart from any of that.
/// </summary>
/// <remarks>
/// Until #15 the page told an operator that nothing on it asked the backend
/// anything, and <c>ConfigurationPageAsksNothingTests</c> held that sentence
/// against the calls the page made. The page asks now, so that class is gone and
/// this stands where it stood: the same subject, the page's own claim about what
/// it asks the server, with the claim the other way round.
///
/// WHAT IT COMPARES. The path the page posts to, against the path the controller
/// declares, so the page and the plugin cannot spell the route differently. The
/// method, against the one the controller answers. That the ask and the save read
/// the backend settings out of one function, so what is asked about is what would
/// be saved. That the answer reaches the page as text and never as markup, because
/// the sentence names a path an operator typed. And that asking does not save,
/// which is the button's type and the absence of the save call from the ask.
///
/// WHAT THIS DOES NOT DO. The page is read as text and its script is not run, so
/// nothing here presses the button, and a server answering the route is #63's
/// boot. It matches the names the page uses for its own functions, so a page that
/// renamed them turns this red rather than passing quietly, which is the safe
/// direction. And it says nothing about whether the sentence the server answers
/// with is true, which the probes' own tests hold.
/// </remarks>
public sealed class ConfigurationPageAsksTheBackendTests
{
    private const string Denial = "nothing on this page asks it yet";

    private const string AnswerElement = "WhisperSubtitlesReadinessAnswer";

    private const string Button = "WhisperSubtitlesAskReadiness";

    private const string Typed = "WhisperSubtitlesConfig.backendSettingsTyped(";

    private const string Shown = "WhisperSubtitlesConfig.showReadiness(";

    private static readonly Regex _whitespace = new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    [Fact]
    public void The_page_asks_on_the_path_the_plugin_claims()
    {
        var page = ConfigurationPageSource.Markup();

        Assert.Contains(
            "readinessPath = '" + ReadinessController.ReadinessPath.TrimStart('/') + "'",
            page,
            StringComparison.Ordinal);
        Assert.Contains("ApiClient.getUrl(WhisperSubtitlesConfig.readinessPath)", Ask(page), StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_posts_which_is_the_method_the_route_answers()
    {
        // The controller's side of the pair is held in ReadinessControllerTests. A
        // page asking with a GET would be a key in a URL and a route that answers
        // 405 to the one page that asks it.
        Assert.Contains("type: 'POST'", Ask(ConfigurationPageSource.Markup()), StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_no_longer_says_that_nothing_on_it_asks()
    {
        // The sentence that used to be true. A page that asks and still carries it
        // denies a check it makes, and a reader told nothing is asked stops looking
        // for the answer.
        var text = _whitespace.Replace(ConfigurationPageSource.Markup(), " ");

        Assert.DoesNotContain(Denial, text, StringComparison.Ordinal);
    }

    [Fact]
    public void What_the_tool_said_about_itself_is_shown_beside_a_ready_answer()
    {
        // The sentence the server built is appended as it is, so a version and a
        // description reach the operator labelled the way the probe labelled them.
        var ask = Ask(ConfigurationPageSource.Markup());

        Assert.Contains("(report.Tool ? ' ' + report.Tool : '')", ask, StringComparison.Ordinal);
    }

    [Fact]
    public void Asking_and_saving_read_the_backend_settings_out_of_one_function()
    {
        // Two readings of the same fields can disagree, and the page would then show
        // an answer about values it does not save. One function, called from both.
        var page = ConfigurationPageSource.Markup();

        Assert.Contains(Typed, Ask(page), StringComparison.Ordinal);
        Assert.Contains(Typed, Save(page), StringComparison.Ordinal);
    }

    [Fact]
    public void The_answer_reaches_the_page_as_text_and_never_as_markup()
    {
        var page = ConfigurationPageSource.Markup();
        var show = FunctionBody(page, "WhisperSubtitlesConfig.showReadiness = function");

        Assert.Contains("#" + AnswerElement + "').textContent", show, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", show, StringComparison.Ordinal);
        Assert.DoesNotContain(AnswerElement + "').innerHTML", page, StringComparison.Ordinal);

        // The ask shows through that one function and writes to the page itself
        // nowhere.
        Assert.Contains(Shown, Ask(page), StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", Ask(page), StringComparison.Ordinal);
        Assert.DoesNotContain("textContent", Ask(page), StringComparison.Ordinal);
    }

    [Fact]
    public void Asking_does_not_save()
    {
        // The button is not the form's submit, and the ask reads the configuration
        // and writes it nowhere. The neighbour is the save, which still saves.
        var page = ConfigurationPageSource.Markup();

        Assert.Matches(
            new Regex(@"<button[^>]*type=""button""[^>]*id=""" + Button + @"""", RegexOptions.None, TimeSpan.FromSeconds(5)),
            page);
        Assert.DoesNotContain("updatePluginConfiguration", Ask(page), StringComparison.Ordinal);
        Assert.Contains("updatePluginConfiguration", Save(page), StringComparison.Ordinal);
    }

    [Fact]
    public void The_button_asks_and_the_answer_has_somewhere_to_land()
    {
        var page = ConfigurationPageSource.Markup();

        Assert.Contains("id=\"" + AnswerElement + "\"", page, StringComparison.Ordinal);
        Assert.Matches(
            new Regex("#" + Button + @"'\)\s*\.addEventListener\('click'", RegexOptions.None, TimeSpan.FromSeconds(5)),
            page);
        Assert.Contains("WhisperSubtitlesConfig.askReadiness()", page, StringComparison.Ordinal);
    }

    private static string Ask(string page) =>
        FunctionBody(page, "WhisperSubtitlesConfig.askReadiness = function");

    private static string Save(string page)
    {
        var at = page.IndexOf(".addEventListener('submit'", StringComparison.Ordinal);

        Assert.True(at >= 0, "the page has no submit handler to read the save out of");

        return page[at..];
    }

    /// <summary>
    /// The text of one of the page's own functions, from its assignment to the
    /// first close of a function at that indentation.
    /// </summary>
    private static string FunctionBody(string page, string opening)
    {
        var at = page.IndexOf(opening, StringComparison.Ordinal);

        Assert.True(at >= 0, $"the page has no function opening with \"{opening}\"");

        var end = page.IndexOf("};", at, StringComparison.Ordinal);

        Assert.True(end > at, $"the function opening with \"{opening}\" never closes");

        return page[at..end];
    }
}
