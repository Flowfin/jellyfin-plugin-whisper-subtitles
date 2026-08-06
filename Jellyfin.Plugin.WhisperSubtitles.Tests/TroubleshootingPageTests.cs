using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The troubleshooting page is only worth having while it matches the code. A
/// page that has fallen behind is worse than no page: an operator holding a
/// reason the page does not mention concludes the reason is undocumented and
/// stops looking, and an entry describing a reason that no longer exists sends
/// somebody after a setting that cannot be the cause.
///
/// So the correspondence is asserted rather than remembered, and in both
/// directions, because the two drift apart by different accidents. A reason
/// added to the enum with no entry is the forgetful direction. An entry left
/// behind when a reason is renamed is the tidy-up direction, and it is the one
/// nobody notices, because everything still builds.
/// </summary>
public class TroubleshootingPageTests
{
    // The page is prose and its headings are for people, so exactly one heading
    // shape is machine-read and it is not the plain "## Name" a writer would
    // reach for by accident. A section about something that is not a reason can
    // then be added freely without this test having an opinion about it.
    private static readonly Regex _reasonHeading = new(
        @"^## The reason (?<name>[A-Za-z]+)$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [Fact]
    public void Every_reason_in_the_code_has_an_entry_on_the_page()
    {
        var documented = DocumentedReasons();

        foreach (var reason in Enum.GetNames<TranscriptionFailureReason>())
        {
            Assert.True(
                documented.Contains(reason),
                $"{reason} is a value of TranscriptionFailureReason with no entry in docs/troubleshooting.md, so a run can report a reason the page does not explain");
        }
    }

    [Fact]
    public void Every_entry_on_the_page_describes_a_reason_that_still_exists()
    {
        var declared = Enum.GetNames<TranscriptionFailureReason>();

        foreach (var documented in DocumentedReasons())
        {
            Assert.True(
                declared.Contains(documented, StringComparer.Ordinal),
                $"docs/troubleshooting.md has an entry for {documented}, which is not a value of TranscriptionFailureReason, so the page describes something no run can report");
        }
    }

    [Fact]
    public void Each_entry_names_an_action_rather_than_only_restating_the_reason()
    {
        // What separates an entry that helps from one that says the reason again
        // in longer words is that it tells the reader what to do. That judgement
        // cannot be made by a machine; the presence of the part carrying it can.
        var page = Page();
        var entries = _reasonHeading.Matches(page);

        Assert.NotEmpty(entries);

        foreach (var entry in entries.Cast<Match>())
        {
            var next = entries.Cast<Match>().FirstOrDefault(m => m.Index > entry.Index);
            var end = next is null ? page.Length : next.Index;
            var body = page[entry.Index..end];

            Assert.True(
                body.Contains("### What to do", StringComparison.Ordinal),
                $"the entry for {entry.Groups["name"].Value} in docs/troubleshooting.md has no \"What to do\" part, so it explains the reason without naming an action");
        }
    }

    private static List<string> DocumentedReasons()
    {
        var found = _reasonHeading.Matches(Page())
            .Cast<Match>()
            .Select(m => m.Groups["name"].Value)
            .ToList();

        // A duplicated entry passes both directions of the comparison above while
        // leaving a reader with two answers to one question, so it is refused
        // here rather than being deduplicated into invisibility.
        Assert.Equal(found.Count, found.Distinct(StringComparer.Ordinal).Count());

        return found;
    }

    private static string Page()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "troubleshooting.md");

        Assert.True(File.Exists(path), $"the troubleshooting page was not copied next to the test assembly, looked in {path}");

        return File.ReadAllText(path);
    }
}
