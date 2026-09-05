using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Output;

namespace Jellyfin.Plugin.WhisperSubtitles.Api;

/// <summary>
/// What the page is shown when it asks what this plugin wrote: every recorded
/// file with what was found at its path, and the counts the page leads with.
/// </summary>
public sealed class GeneratedSubtitleReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSubtitleReport"/> class.
    /// </summary>
    /// <param name="findings">What the listing found.</param>
    public GeneratedSubtitleReport(GeneratedSubtitleFindings findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        Lines = findings.Findings.Select(finding => new GeneratedSubtitleLine(finding)).ToList();
        UnreadableRecordLines = findings.UnreadableRecordLines;
        Unchanged = findings.Findings.Count(finding => finding.State == GeneratedSubtitleState.Unchanged);
        Edited = findings.Findings.Count(finding => finding.State == GeneratedSubtitleState.Edited);
        Gone = findings.Findings.Count(finding => finding.State == GeneratedSubtitleState.Gone);
    }

    /// <summary>
    /// Gets one line per recorded file, in the record's order.
    /// </summary>
    public IReadOnlyList<GeneratedSubtitleLine> Lines { get; }

    /// <summary>
    /// Gets how many lines of the record could not be read.
    /// </summary>
    public int UnreadableRecordLines { get; }

    /// <summary>
    /// Gets how many files are still exactly as written, which is what a removal would take.
    /// </summary>
    public int Unchanged { get; }

    /// <summary>
    /// Gets how many files differ from what was written and are kept.
    /// </summary>
    public int Edited { get; }

    /// <summary>
    /// Gets how many recorded files are no longer at their path.
    /// </summary>
    public int Gone { get; }
}
