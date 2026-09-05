using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WhisperSubtitles.Output;

/// <summary>
/// What a listing found, over every entry the record could give it.
/// </summary>
public sealed class GeneratedSubtitleFindings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSubtitleFindings"/> class.
    /// </summary>
    /// <param name="findings">One finding per recorded file, in the record's order.</param>
    /// <param name="unreadableRecordLines">How many lines of the record could not be read.</param>
    public GeneratedSubtitleFindings(IReadOnlyList<GeneratedSubtitleFinding> findings, int unreadableRecordLines)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentOutOfRangeException.ThrowIfNegative(unreadableRecordLines);

        Findings = findings;
        UnreadableRecordLines = unreadableRecordLines;
    }

    /// <summary>
    /// Gets one finding per recorded file, in the record's order.
    /// </summary>
    public IReadOnlyList<GeneratedSubtitleFinding> Findings { get; }

    /// <summary>
    /// Gets how many lines of the record could not be read.
    /// </summary>
    public int UnreadableRecordLines { get; }

    /// <summary>
    /// Gets the findings a removal would act on: the files still exactly as written.
    /// </summary>
    public IReadOnlyList<GeneratedSubtitleFinding> Removable =>
        Findings.Where(finding => finding.State == GeneratedSubtitleState.Unchanged).ToList();
}
