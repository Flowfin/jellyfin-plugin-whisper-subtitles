using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
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
    //
    // The carriage return is optional and that is the whole of it. The page is
    // tracked text under `* text=auto`, so git stores a line feed and the
    // checkout decides what the file on disk ends its lines with. In .NET `$`
    // does not match before a carriage return, so without this the page parses to
    // nothing at all on a clone that checked it out the way Windows prefers,
    // while every route the repository runs is green. What that looks like is
    // worse than a plain failure: a page whose entries have all vanished reports
    // as a page missing every entry, which reads as documentation that fell
    // behind rather than as a check that cannot read it.
    /// <summary>
    /// The file the remote backend lives in, which the paragraph about attaching a
    /// key has to name so a reporter can go and look rather than take it on trust.
    /// </summary>
    private const string BackendThatTakesAKey =
        "Jellyfin.Plugin.WhisperSubtitles/Backends/Remote/RemoteWhisperBackend.cs";

    /// <summary>
    /// The issue that landed the task and nothing it does, which is therefore not
    /// the issue an absence on this page belongs to.
    /// </summary>
    private const string TaskShellIssue = "#17";

    /// <summary>
    /// The issue that holds the joining, which is the absence this page is about.
    /// </summary>
    private const string JoiningIssue = "#183";

    /// <summary>
    /// The issue that holds the configuration page, which is therefore not the
    /// issue an absence about a readiness report belongs to.
    /// </summary>
    private const string ConfigurationPageIssue = "#36";

    /// <summary>
    /// The issue that holds the readiness report on that page, which is the
    /// absence a reader of this page is actually meeting.
    /// </summary>
    private const string ReadinessReportIssue = "#15";

    /// <summary>
    /// The issue that holds the rule being checked once for the whole plugin, which
    /// is the absence the key paragraph rests on.
    /// </summary>
    private const string LoggingRuleIssue = "#73";

    /// <summary>
    /// The sentence that paragraph gives as the reason the half a reporter is not
    /// covered by is not covered yet.
    /// </summary>
    private const string NothingLogsYet = "nothing in this plugin logs yet";

    /// <summary>
    /// One line ending, and the space two lines of one paragraph are joined with.
    /// Named rather than escaped so this file carries no line ending inside a
    /// literal, which is the same care the comment below takes for the same page.
    /// </summary>
    private const char NewLine = (char)10;

    /// <summary>
    /// The other one.
    /// </summary>
    private const char Space = (char)32;

    private static readonly Regex _reasonHeading = new(
        @"^## The reason (?<name>[A-Za-z]+)\r?$",
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
        var found = Names(Page());

        // A duplicated entry passes both directions of the comparison above while
        // leaving a reader with two answers to one question, so it is refused
        // here rather than being deduplicated into invisibility.
        Assert.Equal(found.Count, found.Distinct(StringComparer.Ordinal).Count());

        return found;
    }

    [Fact]
    public void The_page_reads_the_same_whatever_the_checkout_did_to_its_line_endings()
    {
        // Both forms, from the same bytes, rather than a claim about the
        // expression. A clone on one platform holds the first and a clone on the
        // other holds the second, and neither of them is wrong: `.gitattributes`
        // stores a line feed and lets the checkout decide. What has to be true is
        // that the answer does not move.
        var asLineFeeds = Page().Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        var fromLineFeeds = Names(asLineFeeds);
        var fromCarriageReturns = Names(asCarriageReturns);

        Assert.NotEmpty(fromLineFeeds);
        Assert.Equal(fromLineFeeds, fromCarriageReturns);
    }

    /// <summary>
    /// The page tells a reporter what never to attach, and the reason it gives has
    /// to be true of this tree. It said no backend here takes a configured key.
    /// </summary>
    /// <remarks>
    /// This is the page somebody reads with a log file open and a public issue form
    /// in front of them, so a wrong reason on it costs more than a wrong reason
    /// anywhere else in these documents. The sentence was written before the remote
    /// backend landed and named the issue that landed it, and a reader taking it at
    /// face value concludes that any key in front of them came from somewhere other
    /// than this plugin.
    ///
    /// The tree side is read off the type rather than off a search, because the
    /// property either exists on the options this backend is constructed with or it
    /// does not.
    ///
    /// WHAT THIS DOES NOT DO. It reads one direction. A page still saying a backend
    /// takes a key after the property is removed would need the property gone to be
    /// exercised, and that is a build failure here rather than a red test, so that
    /// arm is stated and not proved. It matches the words the old denial was written
    /// in rather than its meaning, so the same claim in other words passes. And it
    /// has no opinion about the rest of the paragraph.
    /// </remarks>
    [Fact]
    public void The_page_does_not_tell_a_reporter_that_no_backend_takes_a_key()
    {
        var takesAKey = typeof(RemoteBackendOptions).GetProperty("ApiKey");
        var paragraph = ParagraphSaying("Never attach a configured key");

        Assert.NotNull(takesAKey);

        Assert.False(
            paragraph.Contains("No backend in the tree takes", StringComparison.OrdinalIgnoreCase),
            $"docs/troubleshooting.md tells a reporter no backend here takes a configured key, and {nameof(RemoteBackendOptions)}.{takesAKey!.Name} is what the remote backend sends as a bearer header. A reporter who believes the page concludes a key in front of them is somebody else's: {paragraph}");

        Assert.True(
            paragraph.Contains(BackendThatTakesAKey, StringComparison.Ordinal),
            $"docs/troubleshooting.md says a backend here takes a key and does not say which. The reason is the part a reporter checks, so it names the file: {paragraph}");
    }

    /// <summary>
    /// The paragraph listing what is not built yet named the scheduled task, which
    /// is in this assembly, and named the issue that landed it.
    /// </summary>
    /// <remarks>
    /// #17 lands the task and nothing it does, in its own first sentence, so a
    /// reader sent there for the absence this page is about meets an issue whose
    /// conditions are the key, the name, the trigger list and a run that reports
    /// nothing is configured. What is missing is the run, and #183 is where the
    /// joining is held.
    ///
    /// The tree side is the task's own key, which is what a server finds it by, and
    /// it is read off the type rather than off a search on purpose. A search for the
    /// interface would put this file into its own result set, which is the trap
    /// `ReadmeClaimsTests` states in its remarks and which it caught this leg in
    /// while it was being written.
    ///
    /// WHAT THIS DOES NOT DO. The tree side is a type in this assembly, which is
    /// true for as long as this compiles, so the direction where the task really is
    /// absent is not exercised. It reads one paragraph. And it judges a number
    /// rather than a sentence: a paragraph naming the right issues around a claim
    /// that is wrong for another reason passes.
    /// </remarks>
    [Fact]
    public void The_paragraph_about_what_is_missing_names_the_run_and_not_the_task_shell()
    {
        var paragraph = ParagraphSaying("is not built yet");

        Assert.False(
            string.IsNullOrWhiteSpace(SubtitleGenerationTask.TaskKey),
            "the task this leg reads the tree side from no longer carries the key a server finds it by");

        Assert.False(
            paragraph.Contains(TaskShellIssue, StringComparison.Ordinal),
            $"docs/troubleshooting.md lists what is not built and names {TaskShellIssue}, which landed the task this assembly holds. The absence is the run: {paragraph}");

        Assert.True(
            paragraph.Contains(JoiningIssue, StringComparison.Ordinal),
            $"docs/troubleshooting.md says the run is missing and names nothing holding it, so a reader cannot follow it up: {paragraph}");
    }

    /// <summary>
    /// The paragraph listing what is not built yet named the configuration page,
    /// which this plugin registers and an operator chooses a backend on.
    /// </summary>
    /// <remarks>
    /// This is the same accident one line down from the one above, arriving the
    /// day the page landed. The entry for BackendNotReady sends an operator to the
    /// configuration page to read what the backend says about itself, and the
    /// paragraph at the top told them that page is not built. A reader who
    /// believes it does not open the page, and the page is where the choice they
    /// are being asked about is made.
    ///
    /// What is genuinely absent is narrower and it is this page's own subject: the
    /// page shows no readiness report, which is the clause #15 is open on.
    ///
    /// WHAT THIS DOES NOT DO. The tree side is the line the page saves the setting
    /// with, so it reads whether a choice can be made rather than whether an
    /// operator can reach the page in a dashboard, which nothing here boots. It
    /// reads one paragraph, and it judges a number rather than a sentence: a
    /// paragraph naming the right issues around a claim that is wrong for another
    /// reason passes.
    /// </remarks>
    [Fact]
    public void The_paragraph_about_what_is_missing_names_the_readiness_report_and_not_the_page()
    {
        var paragraph = ParagraphSaying("is not built yet");

        ConfigurationPageSource.RefuseUnlessAnOperatorChoosesTheBackendOnIt();

        Assert.False(
            paragraph.Contains(ConfigurationPageIssue, StringComparison.Ordinal),
            $"docs/troubleshooting.md lists what is not built and names {ConfigurationPageIssue}, which landed the page this plugin registers and an operator chooses a backend on. The absence is the readiness report on it: {paragraph}");

        Assert.True(
            paragraph.Contains(ReadinessReportIssue, StringComparison.Ordinal),
            $"docs/troubleshooting.md says the readiness report is missing and names nothing holding it, so a reader cannot follow it up: {paragraph}");
    }

    /// <summary>
    /// The key paragraph tells a reporter that the half of the rule covering a
    /// logger is owed because nothing here logs, and `docs/logging.md` makes the
    /// same claim while this page's copy of it was read by nothing.
    /// </summary>
    /// <remarks>
    /// `LoggingPageTests` holds that sentence on the page it is about, in both
    /// directions. This page says it a second time, to a different reader and for a
    /// different purpose: the one here is what somebody about to send a report is
    /// deciding on, and it is the reason they are told the backend half is all that
    /// is asserted. Nothing read it.
    ///
    /// The direction that costs the most is the day a logger arrives. The change
    /// that adds one is told about `docs/logging.md` by the leg over there and has
    /// no reason to open this page, so the repair gets made on one of the two and
    /// this one goes on telling a reporter that no log line can carry their key.
    /// Both pages are read against the one search in <see cref="PluginLoggerSites"/>
    /// rather than against two, so they cannot be judged by slightly different
    /// questions.
    ///
    /// The other direction is the sentence quietly going while nothing logs, which
    /// leaves the paragraph saying a half is owed without saying why, and the issue
    /// reference is asked for beside it so a reader can follow the absence up.
    ///
    /// WHAT THIS DOES NOT DO. It reads one paragraph and it matches a phrase rather
    /// than a meaning, so the same claim in other words passes and a rewording of
    /// this one turns it red and has to be made here as well. The tree side counts a
    /// file naming the type, in a comment as readily as in code, and it says nothing
    /// about whether a line is ever written. And it has no opinion about the rest of
    /// the paragraph: the backend half it credits `RemoteWhisperBackendTests` with
    /// is the leg above rather than this one.
    /// </remarks>
    [Fact]
    public void The_page_says_nothing_here_logs_exactly_while_nothing_does()
    {
        var paragraph = ParagraphSaying("The rule that key carries");
        var naming = PluginLoggerSites.All();

        if (naming.Count == 0)
        {
            Assert.True(
                paragraph.Contains(NothingLogsYet, StringComparison.Ordinal),
                $"nothing under the plugin project names {PluginLoggerSites.LoggerType} and docs/troubleshooting.md no longer says so, so a reporter is told a half of the rule is owed and not why: {paragraph}");

            Assert.True(
                paragraph.Contains(LoggingRuleIssue, StringComparison.Ordinal),
                $"docs/troubleshooting.md says the whole-plugin half of the key rule is not built and names nothing holding it, so a reporter cannot follow it up: {paragraph}");
        }
        else
        {
            Assert.False(
                paragraph.Contains(NothingLogsYet, StringComparison.Ordinal),
                $"docs/troubleshooting.md still says \"{NothingLogsYet}\" and the plugin names {PluginLoggerSites.LoggerType} in {string.Join(", ", naming)}. A reporter reading that sentence concludes no log line can carry the key they configured, which is the one thing this section exists to stop them concluding.");
        }
    }

    /// <summary>
    /// The paragraph of the page containing a phrase, with its line breaks turned
    /// into spaces so a sentence spanning two of them reads as one.
    /// </summary>
    /// <param name="phrase">A phrase only the wanted paragraph carries.</param>
    /// <returns>The paragraph.</returns>
    private static string ParagraphSaying(string phrase)
    {
        var paragraphs = new List<string>();
        var current = new List<string>();

        foreach (var raw in Page().Split(NewLine))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    paragraphs.Add(string.Join(Space, current));
                    current.Clear();
                }
            }
            else
            {
                current.Add(line);
            }
        }

        if (current.Count > 0)
        {
            paragraphs.Add(string.Join(Space, current));
        }

        var matching = paragraphs
            .Where(block => block.Contains(phrase, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            matching.Count == 1,
            $"docs/troubleshooting.md has {matching.Count} paragraphs saying it and this reads one. A phrase that stopped being unique leaves whichever paragraph it picked unread.");

        return matching[0];
    }

    private static List<string> Names(string page) =>
        _reasonHeading.Matches(page)
            .Cast<Match>()
            .Select(m => m.Groups["name"].Value)
            .ToList();

    private static string Page()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "troubleshooting.md");

        Assert.True(File.Exists(path), $"the troubleshooting page was not copied next to the test assembly, looked in {path}");

        return File.ReadAllText(path);
    }
}
