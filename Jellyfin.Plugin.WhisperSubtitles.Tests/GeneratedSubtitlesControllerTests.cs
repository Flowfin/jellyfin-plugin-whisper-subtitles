using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Api;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The two routes the configuration page lists and removes what this plugin
/// wrote through, judged the way the server meets them: what each declares, who
/// may ask, and what a removal does with the paths it is posted.
/// </summary>
/// <remarks>
/// The hostile case <c>docs/untrusted-input.md</c> names this class for is a
/// posted path the record does not name. It is never read, let alone removed,
/// because the route lists the record again and takes the intersection; the
/// paths an operator posts decide nothing on their own.
///
/// No server is booted here. Whether a booted server answers both paths is read
/// by the scan in <c>booted-server.yml</c> against the claim record, and the
/// record is held to this type's attributes by <c>ClaimRecordTests</c>.
/// </remarks>
public sealed class GeneratedSubtitlesControllerTests
{
    private static readonly DateTimeOffset _moment = new(2026, 9, 5, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_paths_it_declares_are_the_ones_the_record_and_the_page_spell()
    {
        var prefix = Assert.Single(typeof(GeneratedSubtitlesController).GetCustomAttributes<RouteAttribute>()).Template;

        Assert.Equal(
            GeneratedSubtitlesController.ListingPath,
            "/" + prefix + "/" + Assert.Single(Action(nameof(GeneratedSubtitlesController.ListAsync)).GetCustomAttributes<HttpMethodAttribute>()).Template);
        Assert.Equal(
            GeneratedSubtitlesController.RemovalPath,
            "/" + prefix + "/" + Assert.Single(Action(nameof(GeneratedSubtitlesController.RemoveAsync)).GetCustomAttributes<HttpMethodAttribute>()).Template);
    }

    [Fact]
    public void The_listing_is_a_get_and_the_removal_is_a_post()
    {
        // A removal on a GET is a removal a link can make. The listing changes
        // nothing and answers a GET.
        Assert.Equal(new[] { "GET" }, Assert.Single(Action(nameof(GeneratedSubtitlesController.ListAsync)).GetCustomAttributes<HttpMethodAttribute>()).HttpMethods);
        Assert.Equal(new[] { "POST" }, Assert.Single(Action(nameof(GeneratedSubtitlesController.RemoveAsync)).GetCustomAttributes<HttpMethodAttribute>()).HttpMethods);
    }

    [Fact]
    public void Only_an_elevated_session_may_ask_either()
    {
        var policy = Assert.Single(typeof(GeneratedSubtitlesController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(Policies.RequiresElevation, policy.Policy);
        Assert.Empty(Action(nameof(GeneratedSubtitlesController.ListAsync)).GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(Action(nameof(GeneratedSubtitlesController.RemoveAsync)).GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task The_listing_reports_every_recorded_file_with_its_state_and_removes_nothing()
    {
        var unchanged = Entry("/media/a.srt");
        var edited = Entry("/media/b.srt");
        var gone = Entry("/media/c.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(unchanged).With(edited).With(gone);
        record.UnreadableLines = 1;
        var digest = StubFileDigest.Empty().Holding(unchanged).HoldingSomethingElseAt(edited);
        var removal = new StubFileRemoval();
        var controller = new GeneratedSubtitlesController(record, digest, removal);

        var answer = await controller.ListAsync(CancellationToken.None).ConfigureAwait(true);

        var report = Assert.IsType<GeneratedSubtitleReport>(Assert.IsType<OkObjectResult>(answer.Result).Value);
        Assert.Equal(3, report.Lines.Count);
        Assert.Equal(new[] { "Unchanged", "Edited", "Gone" }, report.Lines.Select(line => line.State));
        Assert.Equal(1, report.Unchanged);
        Assert.Equal(1, report.Edited);
        Assert.Equal(1, report.Gone);
        Assert.Equal(1, report.UnreadableRecordLines);
        Assert.Empty(removal.Removed);
    }

    [Fact]
    public async Task A_removal_takes_only_what_the_record_names_the_listing_finds_unchanged_and_the_operator_posted()
    {
        // The hostile case: a posted path the record does not name, beside a
        // recorded file the operator did not confirm and one that has been edited.
        // One file goes.
        var confirmed = Entry("/media/a.srt");
        var notConfirmed = Entry("/media/b.srt");
        var edited = Entry("/media/c.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(confirmed).With(notConfirmed).With(edited);
        var digest = StubFileDigest.Empty().Holding(confirmed).Holding(notConfirmed).HoldingSomethingElseAt(edited);
        var removal = new StubFileRemoval();
        var controller = new GeneratedSubtitlesController(record, digest, removal);

        var answer = await controller.RemoveAsync(
            new GeneratedSubtitleRemovalRequest { Paths = [confirmed.Path, edited.Path, "/media/somebody-elses.srt"] },
            CancellationToken.None).ConfigureAwait(true);

        var outcome = Assert.IsType<GeneratedSubtitleRemoval>(Assert.IsType<OkObjectResult>(answer.Result).Value);
        Assert.Equal(new[] { confirmed.Path }, outcome.Removed);
        Assert.Equal(new[] { confirmed.Path }, removal.Removed);
        Assert.DoesNotContain("/media/somebody-elses.srt", digest.Read);
    }

    [Fact]
    public async Task A_removal_with_no_body_removes_nothing()
    {
        var recorded = Entry("/media/a.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(recorded);
        var digest = StubFileDigest.Empty().Holding(recorded);
        var removal = new StubFileRemoval();
        var controller = new GeneratedSubtitlesController(record, digest, removal);

        var answer = await controller.RemoveAsync(null, CancellationToken.None).ConfigureAwait(true);

        var outcome = Assert.IsType<GeneratedSubtitleRemoval>(Assert.IsType<OkObjectResult>(answer.Result).Value);
        Assert.Empty(outcome.Removed);
        Assert.Empty(removal.Removed);
    }

    [Fact]
    public void The_seams_are_required()
    {
        var record = StubPublishedSubtitleRecord.Empty();
        var digest = StubFileDigest.Empty();
        var removal = new StubFileRemoval();

        Assert.Throws<ArgumentNullException>(() => new GeneratedSubtitlesController(null!, digest, removal));
        Assert.Throws<ArgumentNullException>(() => new GeneratedSubtitlesController(record, null!, removal));
        Assert.Throws<ArgumentNullException>(() => new GeneratedSubtitlesController(record, digest, null!));
    }

    private static MethodInfo Action(string name) =>
        typeof(GeneratedSubtitlesController).GetMethod(name)!;

    private static PublishedSubtitle Entry(string path) =>
        new(Guid.NewGuid(), path, 2048, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), _moment);
}
