using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The two steps of removing what this plugin generated, driven through doubles
/// for the record, the digest and the removal, so the disk can be changed between
/// the two steps on cue.
/// </summary>
/// <remarks>
/// The four conditions of #43 are here by name: the listing step deletes nothing,
/// a file the record does not name is never removed, a modified file is reported
/// and kept, and the count listed equals the count removed when nothing changed
/// between the steps. What is not here is the page that offers the two steps to
/// an operator, which is the surface half of that issue.
/// </remarks>
public sealed class GeneratedSubtitleListingTests
{
    private static readonly DateTimeOffset _moment = new(2026, 9, 5, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_listing_reads_every_recorded_file_and_takes_no_removal_to_delete_with()
    {
        var a = Entry("/media/a.srt");
        var b = Entry("/media/b.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(a).With(b);
        var digest = StubFileDigest.Empty().Holding(a).Holding(b);

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(new[] { a.Path, b.Path }, digest.Read);
        Assert.Equal(2, findings.Removable.Count);

        // The listing step performs no deletion, and the shape says so: nothing it
        // is handed can take a file off a disk.
        var listing = typeof(GeneratedSubtitleListing).GetMethod(nameof(GeneratedSubtitleListing.ListAsync))!;
        Assert.DoesNotContain(listing.GetParameters(), parameter => parameter.ParameterType == typeof(IFileRemoval));
    }

    [Fact]
    public async Task A_file_the_record_does_not_name_is_never_removed()
    {
        var recorded = Entry("/media/a.srt");
        var somebodys = Entry("/media/b.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(recorded);
        var digest = StubFileDigest.Empty().Holding(recorded).Holding(somebodys);
        var removal = new StubFileRemoval();

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);
        var removed = await GeneratedSubtitleListing.RemoveAsync(findings.Removable, digest, removal, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(new[] { recorded.Path }, removal.Removed);
        Assert.Equal(new[] { recorded.Path }, removed.Removed);
        Assert.DoesNotContain(somebodys.Path, digest.Read);
    }

    [Fact]
    public async Task A_modified_file_is_reported_and_kept()
    {
        var edited = Entry("/media/a.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(edited);
        var digest = StubFileDigest.Empty().HoldingSomethingElseAt(edited);
        var removal = new StubFileRemoval();

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal(GeneratedSubtitleState.Edited, finding.State);
        Assert.NotNull(finding.Now);
        Assert.Empty(findings.Removable);

        var removed = await GeneratedSubtitleListing.RemoveAsync(findings.Findings, digest, removal, CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(removal.Removed);
        Assert.Empty(removed.Removed);
    }

    [Fact]
    public async Task A_file_that_is_gone_is_reported_as_gone_rather_than_dropped()
    {
        var gone = Entry("/media/a.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(gone);

        var findings = await GeneratedSubtitleListing.ListAsync(record, StubFileDigest.Empty(), CancellationToken.None).ConfigureAwait(true);

        var finding = Assert.Single(findings.Findings);
        Assert.Equal(GeneratedSubtitleState.Gone, finding.State);
        Assert.Null(finding.Now);
    }

    [Fact]
    public async Task The_count_listed_equals_the_count_removed_when_nothing_changed_between_the_steps()
    {
        var entries = new[] { Entry("/media/a.srt"), Entry("/media/b.srt"), Entry("/media/c.srt") };
        var record = StubPublishedSubtitleRecord.Empty();
        var digest = StubFileDigest.Empty();
        foreach (var entry in entries)
        {
            record.With(entry);
            digest.Holding(entry);
        }

        var removal = new StubFileRemoval();

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);
        var removed = await GeneratedSubtitleListing.RemoveAsync(findings.Removable, digest, removal, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3, findings.Removable.Count);
        Assert.Equal(findings.Removable.Count, removed.Removed.Count);
        Assert.Equal(findings.Removable.Select(finding => finding.Entry.Path), removal.Removed);
        Assert.Empty(removed.KeptBecauseChanged);
        Assert.Empty(removed.AlreadyGone);
    }

    [Fact]
    public async Task A_file_edited_between_the_two_steps_is_kept()
    {
        // The case the second reading exists for: the operator confirmed the bytes
        // as listed, and these are not those bytes.
        var a = Entry("/media/a.srt");
        var b = Entry("/media/b.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(a).With(b);
        var digest = StubFileDigest.Empty().Holding(a).Holding(b);
        var removal = new StubFileRemoval();

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);
        digest.HoldingSomethingElseAt(a);
        var removed = await GeneratedSubtitleListing.RemoveAsync(findings.Removable, digest, removal, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(new[] { b.Path }, removal.Removed);
        Assert.Equal(new[] { a.Path }, removed.KeptBecauseChanged);
    }

    [Fact]
    public async Task A_file_gone_between_the_two_steps_is_counted_rather_than_failed_on()
    {
        var a = Entry("/media/a.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(a);
        var digest = StubFileDigest.Empty().Holding(a);
        var removal = new StubFileRemoval();

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);
        digest.Without(a.Path);
        var removed = await GeneratedSubtitleListing.RemoveAsync(findings.Removable, digest, removal, CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(removal.Removed);
        Assert.Equal(new[] { a.Path }, removed.AlreadyGone);
    }

    [Fact]
    public async Task Findings_handed_to_the_removal_that_were_not_offered_are_not_acted_on()
    {
        // A caller passing every finding rather than the removable ones gets the
        // same answer: only what the listing marked unchanged is read again.
        var edited = Entry("/media/a.srt");
        var gone = Entry("/media/b.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(edited).With(gone);
        var digest = StubFileDigest.Empty().HoldingSomethingElseAt(edited);
        var removal = new StubFileRemoval();

        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);
        digest.Read.Clear();
        var removed = await GeneratedSubtitleListing.RemoveAsync(findings.Findings, digest, removal, CancellationToken.None).ConfigureAwait(true);

        Assert.Empty(digest.Read);
        Assert.Empty(removal.Removed);
        Assert.Empty(removed.Removed);
    }

    [Fact]
    public async Task Unreadable_record_lines_are_carried_into_the_findings()
    {
        var record = StubPublishedSubtitleRecord.Empty();
        record.UnreadableLines = 2;

        var findings = await GeneratedSubtitleListing.ListAsync(record, StubFileDigest.Empty(), CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(2, findings.UnreadableRecordLines);
        Assert.Empty(findings.Findings);
    }

    [Fact]
    public async Task A_removal_that_is_stopped_removes_nothing_more()
    {
        var a = Entry("/media/a.srt");
        var record = StubPublishedSubtitleRecord.Empty().With(a);
        var digest = StubFileDigest.Empty().Holding(a);
        var removal = new StubFileRemoval();
        var findings = await GeneratedSubtitleListing.ListAsync(record, digest, CancellationToken.None).ConfigureAwait(true);

        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GeneratedSubtitleListing.RemoveAsync(findings.Removable, digest, removal, stopped.Token)).ConfigureAwait(true);

        Assert.Empty(removal.Removed);
    }

    [Fact]
    public void A_digest_matches_an_entry_on_size_and_bytes_and_not_on_case()
    {
        var entry = Entry("/media/a.srt");

        Assert.True(new FileDigest(entry.SizeInBytes, entry.Sha256.ToUpperInvariant()).Matches(entry));
        Assert.False(new FileDigest(entry.SizeInBytes + 1, entry.Sha256).Matches(entry));
        Assert.False(new FileDigest(entry.SizeInBytes, new string('b', 64)).Matches(entry));
    }

    private static PublishedSubtitle Entry(string path) =>
        new(Guid.NewGuid(), path, 2048, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), _moment);
}
