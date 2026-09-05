using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Audio;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// The two steps of removing what this plugin generated: list what it would
/// remove, and remove only what is still exactly what was listed.
/// </summary>
/// <remarks>
/// Everything here walks the record and never the tree. A file is never a
/// candidate because of its name; it is a candidate because this plugin wrote it
/// and said so at the time, which is the rule #43 states and what stops a
/// subtitle a person made from ever being on the list.
///
/// A file that has changed since the record was written is reported and kept,
/// because an edited transcription is somebody's work. The comparison is the size
/// and the SHA-256 the record holds against the bytes on disk now, and it is made
/// twice: once to list, and again at the moment of deletion, so a file edited
/// between the two steps is kept as well. The listing step deletes nothing, and
/// the removal step deletes nothing the listing did not offer.
///
/// A file the record names and the disk no longer holds is reported as gone
/// rather than dropped, because the record is append-only and a reader of the
/// listing should see the same entries a reader of the file does.
/// </remarks>
public static class GeneratedSubtitleListing
{
    /// <summary>
    /// Lists every file the record names, with what became of it. Deletes nothing.
    /// </summary>
    /// <param name="record">The record of what this plugin published.</param>
    /// <param name="digest">The seam a file's bytes are read through.</param>
    /// <param name="cancellationToken">Stops the listing.</param>
    /// <returns>One finding per recorded file, in the record's order.</returns>
    public static async Task<GeneratedSubtitleFindings> ListAsync(
        IPublishedSubtitleRecord record,
        IFileDigest digest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(digest);

        var reading = await record.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<GeneratedSubtitleFinding>(reading.Entries.Count);

        foreach (var entry in reading.Entries)
        {
            findings.Add(await Judge(entry, digest, cancellationToken).ConfigureAwait(false));
        }

        return new GeneratedSubtitleFindings(findings, reading.UnreadableLines);
    }

    /// <summary>
    /// Removes the files a listing offered, and only those still exactly as listed.
    /// </summary>
    /// <param name="listed">What the listing step offered for removal.</param>
    /// <param name="digest">The seam a file's bytes are read through.</param>
    /// <param name="removal">The seam a file is taken off the disk through.</param>
    /// <param name="cancellationToken">Stops the removal between files, never inside one.</param>
    /// <returns>What was removed, what was kept because it changed, and what was already gone.</returns>
    /// <remarks>
    /// Only findings the listing marked unchanged are looked at, and each one is
    /// read again before it goes: the answer an operator confirmed was about the
    /// bytes at listing time, and a file that differs now is not the file they
    /// confirmed. A file that is gone by now is counted rather than failed on,
    /// because gone is the outcome that was asked for.
    /// </remarks>
    public static async Task<GeneratedSubtitleRemoval> RemoveAsync(
        IReadOnlyList<GeneratedSubtitleFinding> listed,
        IFileDigest digest,
        IFileRemoval removal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listed);
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentNullException.ThrowIfNull(removal);

        var removed = new List<string>();
        var keptBecauseChanged = new List<string>();
        var alreadyGone = new List<string>();

        foreach (var finding in listed.Where(finding => finding.State == GeneratedSubtitleState.Unchanged))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = await digest.DigestAsync(finding.Entry.Path, cancellationToken).ConfigureAwait(false);

            if (now is null)
            {
                alreadyGone.Add(finding.Entry.Path);
            }
            else if (!now.Matches(finding.Entry))
            {
                keptBecauseChanged.Add(finding.Entry.Path);
            }
            else
            {
                removal.Delete(finding.Entry.Path);
                removed.Add(finding.Entry.Path);
            }
        }

        return new GeneratedSubtitleRemoval(removed, keptBecauseChanged, alreadyGone);
    }

    private static async Task<GeneratedSubtitleFinding> Judge(
        PublishedSubtitle entry,
        IFileDigest digest,
        CancellationToken cancellationToken)
    {
        var now = await digest.DigestAsync(entry.Path, cancellationToken).ConfigureAwait(false);

        if (now is null)
        {
            return new GeneratedSubtitleFinding(entry, GeneratedSubtitleState.Gone, null);
        }

        return new GeneratedSubtitleFinding(
            entry,
            now.Matches(entry) ? GeneratedSubtitleState.Unchanged : GeneratedSubtitleState.Edited,
            now);
    }
}
