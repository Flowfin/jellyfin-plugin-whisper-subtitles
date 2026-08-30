using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The limits page tells a reader what this plugin will not do, and it keeps two
/// states apart at every entry: a limit something in the tree holds today, and a
/// limit that is a decision taken and not yet built. Nothing made either half true.
/// </summary>
/// <remarks>
/// The failure it is written against runs in two directions and only one of them
/// is loud. An entry filed as unbuilt when the tree has since built it understates
/// what is held, which costs the review a lookup. An entry stating an unbuilt limit
/// with no marker at all reads as an assurance about a running server, and that is
/// the direction the page exists against.
///
/// The second has already happened. On 2026-08-13 the page said stale temporary
/// audio is swept before the next run begins, in the present tense, in the one
/// section carrying no state marker, while nothing called the sweep. It was found
/// by reading every marker against the tree by hand. This is that reading, run by
/// the suite instead.
///
/// The legs are not counted here, because a count in a remark drifts against the
/// class it describes and this one has already moved twice. Each is a different
/// accident:
///
/// An entry in neither state is the shape above. An entry naming no issue is the
/// page's own opening promise broken, that a reader who disagrees can argue with
/// the decision rather than with the sentence, and it is what the issue behind this
/// page asks for in so many words. An entry naming a file this tree does not have
/// sends a reader looking for evidence that moved. An entry naming a suite this
/// assembly does not run is the same accident one step quieter, because a renamed
/// test class still leaves the page reading as though the coverage were somewhere.
///
/// The last two are the state question asked at a finer grain than a heading, and
/// they exist because the first leg is answered once per section. TWO entries on
/// this page are lists rather than single claims, and they name the same three
/// kinds: what this plugin puts on a disk, and what removing it does and does not
/// take away. A section carrying a marker for one kind satisfies the first leg for
/// all three, so a kind stating an unbuilt thing in the present tense passes on a
/// neighbour's marker.
///
/// That has now happened four times and a person found every one of them. Twice in
/// the list of what is written, which the first of these two legs closed. Twice in
/// the uninstall section afterwards, in the same sentence: it said removal takes
/// away a record of what the plugin produced, when nothing writes one, and that
/// temporary audio never survives a run, when what a process that died mid-run left
/// behind is collected by nothing. Both had already been corrected one section up
/// and neither was visible here, because that section's only marker sat at its end
/// and was about a third kind.
///
/// The kinds are read from <see cref="WriteLocationsTests"/> for both, which already
/// resolves each phrase against its own section, rather than being listed again
/// here.
///
/// WHAT THIS DOES NOT DO, and none of it is an oversight.
///
/// It does not judge whether a marker is TRUE. Whether the thing named actually
/// holds the limit is a reading of two things this cannot compare, and the review
/// is where a wrong answer is caught. What it refuses is an entry that named
/// nothing to compare against.
///
/// It does not reach the tracker. Whether an issue an entry names is open, closed
/// or about something else is an answer on the network, and this suite reaches
/// none by the rule its neighbours enforce. So an entry filed as unbuilt whose
/// issue closed yesterday stays green here until a person moves it.
///
/// It reads what is inside backticks and nothing else. A limit whose evidence is
/// named in plain prose is invisible to the last two legs, which is why they carry
/// their own guard below rather than being trusted to have iterated over anything.
///
/// Each leg carries a fixture it has to refuse, under <c>Fixtures/limits-page/</c>,
/// so the proof it bites is in the tree rather than in the memory of whoever last
/// broke the page on purpose. One fixture per leg, because a leg is proven by a
/// case that trips it AND NO OTHER, and the neighbour that breaks nothing has to
/// stay accepted or a reader refusing every entry would pass every leg.
/// </remarks>
public class LimitsPageTests
{
    /// <summary>
    /// The one section of the page that is about the list rather than a limit in it.
    /// </summary>
    private const string Closing = "When this list is checked against the code";

    /// <summary>
    /// The one entry whose evidence is what another file says rather than that it
    /// exists. Named here because the leg that reads it has to find it by title.
    /// </summary>
    private const string Accuracy = "It promises nothing about accuracy";

    /// <summary>
    /// The entries whose limit is the branch of a question #8 holds that nobody has
    /// ruled out, by title, because the leg that reads them cannot find them any
    /// other way.
    /// </summary>
    /// <remarks>
    /// A list rather than a derivation, and it is the price of refusing the quiet
    /// half of this. Which of a page's limits rests on an unanswered question is a
    /// reading of the tracker, and this suite reaches none, so the alternative to
    /// writing them down is a leg that only ever sees the entries that already say
    /// so. The cost is the usual one: a fourth entry resting on that issue is
    /// invisible here until somebody adds it, and the review is where that is
    /// caught. A title reworded turns this red rather than passing quietly, which
    /// is the direction worth having.
    /// </remarks>
    private static readonly string[] _restingOnAnOpenQuestion =
    [
        "It carries no model and no inference runtime",
        "It does not run the transcription inside the server process",
        "What it writes, and where",
    ];

    private static readonly Regex _heading = new(
        @"^## (?<title>.+?)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The machine-read vocabulary, and the whole of it. Both spellings of the
    // second are on the page today, one with the comma and one with the word, and
    // a reader that took only one of them would report the other entry as filed
    // under nothing. Narrowing this is a change to the page rather than to the
    // expression.
    private static readonly Regex _state = new(
        @"[Hh]eld today|[Dd]ecided(,| and) not yet built",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _backticked = new(
        @"`([^`\r\n]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _issue = new(
        @"#[0-9]+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The issue that collects the decisions this plan needs and has not had, and
    // the digits are held apart from #80 and its neighbours rather than matched as
    // a prefix. An entry naming it rests on a question nobody has answered.
    private static readonly Regex _decisionsIssue = new(
        @"#8(?![0-9])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The vocabulary that says a question has not been answered, and the whole of
    // it. The page writes one of these four today, in two sentences; the other
    // three are the wordings a writer reaches for next and are here so that the
    // leg refuses a missing disclosure rather than a missing turn of phrase.
    // Widening it is a change to this expression and narrowing it is a change to
    // the page.
    private static readonly Regex _openQuestion = new(
        @"\bis open\b|\bstill open\b|\bopen question\b|\bunanswered\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // A name is treated as a path when it ends in an extension this tree writes
    // files with. The subtitle extension the page quotes is deliberately not one
    // of them: `.srt` is the format a reader is being told about and not a file
    // anybody could resolve.
    private static readonly Regex _pathShaped = new(
        @"^[A-Za-z0-9_.\-]+(/[A-Za-z0-9_.\-]+)*\.(cs|sh|md|yaml|yml|props|targets|html|json)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _suiteShaped = new(
        @"^[A-Za-z0-9_]+Tests$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // A blank line, which is the only thing separating one kind from the next in the
    // entry that lists them. A paragraph is the finest unit this page writes, so it
    // is the finest one a state can be asked of without the question becoming a rule
    // about sentences.
    private static readonly Regex _paragraphBreak = new(
        @"\r?\n[ \t]*\r?\n",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    public static TheoryData<string> EveryEntry =>
        new(Entries(Page()).Select(entry => entry.Title).ToArray());

    public static TheoryData<string> EveryEntryRestingOnAnOpenQuestion =>
        new(_restingOnAnOpenQuestion);

    public static TheoryData<string> EveryKindTheWriteListNames =>
        new(WriteLocationsTests.KindsAsTheListNamesThem.ToArray());

    public static TheoryData<string> EveryKindTheWayOutNames =>
        new(WriteLocationsTests.KindsAsTheWayOutNamesThem.ToArray());

    [Fact]
    public void The_reader_finds_the_entries_and_stops_before_the_section_that_is_about_them()
    {
        // Guards every leg below. A reader that found no entries would report a
        // page whose every claim resolves, whatever the page said, and it would do
        // it in green. The closing section is excluded by being last rather than
        // by name alone, so an entry added after it is refused here instead of
        // being silently dropped out of the population.
        var sections = Sections(Page());
        var entries = Entries(Page());

        Assert.True(entries.Count > 1, $"the reader found {entries.Count} entries on the limits page");
        Assert.Equal(Closing, sections[^1].Title);
        Assert.DoesNotContain(entries, entry => entry.Title.Equals(Closing, StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Title.Contains("does not translate", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_entry_is_filed_under_one_of_the_two_states_the_page_keeps_apart(string title)
    {
        var entry = Entry(title);

        Assert.True(
            _state.IsMatch(entry.Body),
            $"the entry \"{title}\" on the limits page says neither that a limit is held today nor that it is decided and not yet built, so a reader cannot tell a promise about a running server from a promise about a later one");
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_entry_names_the_issue_a_reader_who_disagrees_can_argue_with(string title)
    {
        var entry = Entry(title);

        Assert.True(
            _issue.IsMatch(entry.Body),
            $"the entry \"{title}\" on the limits page names no issue, so it reads as an opinion rather than as a decision somebody took and can be argued with");
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_file_an_entry_points_a_reader_at_is_a_file_this_tree_has(string title)
    {
        foreach (var named in Backticked(Entry(title).Body).Where(name => _pathShaped.IsMatch(name)))
        {
            Assert.True(
                Resolves(named),
                $"the entry \"{title}\" on the limits page points a reader at {named}, and neither the root of this tree nor the plugin project has such a file");
        }
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_suite_an_entry_says_refuses_a_breach_is_one_this_assembly_runs(string title)
    {
        var running = ClassesThisSuiteRunsTestsIn();

        foreach (var named in Backticked(Entry(title).Body).Where(name => _suiteShaped.IsMatch(name)))
        {
            Assert.True(
                running.Contains(named),
                $"the entry \"{title}\" on the limits page says {named} holds it and this assembly runs no tests in a class by that name");
        }
    }

    [Theory]
    [MemberData(nameof(EveryKindTheWriteListNames))]
    public void Every_kind_the_write_list_names_carries_a_state_of_its_own(string phrase)
    {
        var paragraph = ParagraphNaming(Entry(WriteLocationsTests.ListTitle).Body, phrase);

        Assert.True(
            _state.IsMatch(paragraph),
            $"the kind the limits page introduces with \"{phrase}\" says neither that it is held today nor that it is decided and not yet built, and the entry around it passes the state leg on a marker belonging to a different kind");
    }

    [Fact]
    public void The_reader_refuses_a_kind_that_leans_on_a_neighbours_state()
    {
        // The accident this leg is for, and the one the leg above cannot see. The
        // fixture's entry carries both spellings of a state, so it passes the leg
        // that asks the question once per heading, and one of its three kinds says
        // nothing about which state it is in.
        var entry = Assert.Single(Entries(Fixture("a-kind-with-no-state-of-its-own")));
        var kinds = WriteLocationsTests.KindsAsTheListNamesThem;

        Assert.True(
            _state.IsMatch(entry.Body) && _issue.IsMatch(entry.Body),
            "the fixture has to trip this leg and no other");

        var unstated = kinds
            .Where(phrase => !_state.IsMatch(ParagraphNaming(entry.Body, phrase)))
            .ToList();

        Assert.Equal(["The subtitle file"], unstated);
    }

    [Theory]
    [MemberData(nameof(EveryKindTheWayOutNames))]
    public void Every_kind_the_way_out_names_carries_a_state_of_its_own(string phrase)
    {
        var paragraph = ParagraphNaming(Entry(WriteLocationsTests.UninstallTitle).Body, phrase);

        Assert.True(
            _state.IsMatch(paragraph),
            $"the kind the uninstall entry speaks of as \"{phrase}\" says neither that it is held today nor that it is decided and not yet built, and the entry around it passes the state leg on a marker belonging to a different kind");
    }

    [Fact]
    public void The_reader_refuses_a_kind_on_the_way_out_that_leans_on_a_neighbours_state()
    {
        // The same accident as the leg above it, in the section that had it last.
        // The fixture's uninstall entry carries a state and an issue, so the leg
        // that asks the question once per heading passes it, and the kind that says
        // what happens to the configuration says nothing about which state that is.
        var entry = Entries(Fixture("a-way-out-kind-with-no-state-of-its-own"))
            .Single(section => section.Title.Equals(WriteLocationsTests.UninstallTitle, StringComparison.Ordinal));
        var kinds = WriteLocationsTests.KindsAsTheWayOutNamesThem;

        Assert.True(
            _state.IsMatch(entry.Body) && _issue.IsMatch(entry.Body),
            "the fixture has to trip this leg and no other");

        var unstated = kinds
            .Where(phrase => !_state.IsMatch(ParagraphNaming(entry.Body, phrase)))
            .ToList();

        Assert.Equal(["the server removes plugin data"], unstated);
    }

    [Fact]
    public void The_page_names_files_and_suites_rather_than_leaving_the_two_legs_above_iterating_over_nothing()
    {
        // The other half of guarding the resolution legs. If no entry named
        // anything of either shape they would walk an empty list and pass without
        // resolving a single name, and the day the last one was deleted would look
        // exactly like today.
        var named = Entries(Page()).SelectMany(entry => Backticked(entry.Body)).ToList();

        Assert.Contains(named, name => _pathShaped.IsMatch(name));
        Assert.Contains(named, name => _suiteShaped.IsMatch(name));
    }

    [Fact]
    public void The_reader_refuses_an_entry_in_neither_state()
    {
        var entry = Assert.Single(Entries(Fixture("an-entry-in-neither-state")));

        Assert.DoesNotMatch(_state, entry.Body);
        Assert.True(_issue.IsMatch(entry.Body), "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_names_no_issue()
    {
        var entry = Assert.Single(Entries(Fixture("an-entry-that-names-no-issue")));

        Assert.DoesNotMatch(_issue, entry.Body);
        Assert.True(_state.IsMatch(entry.Body), "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_points_at_a_file_this_tree_does_not_have()
    {
        var entry = Assert.Single(Entries(Fixture("a-path-that-is-not-there")));
        var named = Backticked(entry.Body).Where(name => _pathShaped.IsMatch(name)).ToList();

        Assert.NotEmpty(named);
        Assert.DoesNotContain(named, Resolves);
        Assert.True(_state.IsMatch(entry.Body) && _issue.IsMatch(entry.Body), "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_names_a_suite_this_assembly_does_not_run()
    {
        var entry = Assert.Single(Entries(Fixture("a-class-the-suite-does-not-run")));
        var named = Backticked(entry.Body).Where(name => _suiteShaped.IsMatch(name)).ToList();

        Assert.NotEmpty(named);
        Assert.DoesNotContain(named, name => ClassesThisSuiteRunsTestsIn().Contains(name));
        Assert.True(_state.IsMatch(entry.Body) && _issue.IsMatch(entry.Body), "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_a_page_with_no_entries_left_in_it()
    {
        // The fixture for the guard rather than for a rule: a page whose entries
        // stopped being sections the reader recognises reads as a page with
        // nothing in it, which is the shape that passes every other leg for free.
        Assert.Empty(Entries(Fixture("no-entries-at-all")));
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        // Without this a reader that refused every entry would pass every leg
        // above.
        var entries = Entries(Fixture("clean"));
        var running = ClassesThisSuiteRunsTestsIn();

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Matches(_state, entry.Body));
        Assert.All(entries, entry => Assert.Matches(_issue, entry.Body));
        Assert.All(
            entries.SelectMany(entry => Backticked(entry.Body)).Where(name => _pathShaped.IsMatch(name)),
            name => Assert.True(Resolves(name), $"the neighbour points at {name}, which this tree does not have"));
        Assert.All(
            entries.SelectMany(entry => Backticked(entry.Body)).Where(name => _suiteShaped.IsMatch(name)),
            name => Assert.Contains(name, running));
    }

    [Theory]
    [MemberData(nameof(EveryEntryRestingOnAnOpenQuestion))]
    public void Every_entry_resting_on_a_question_the_decisions_issue_holds_names_it(string title)
    {
        var entry = Entries(Page())
            .SingleOrDefault(section => section.Title.Equals(title, StringComparison.Ordinal));

        Assert.True(
            entry is not null,
            $"the limits page carries no entry titled \"{title}\", so this leg read nothing");

        Assert.True(
            ParagraphsNaming(entry!.Body, _decisionsIssue).Count > 0,
            $"the entry \"{title}\" on the limits page rests on a question #8 holds and names no issue for it, so a reader is told what the plugin does today and not that the answer is somebody's to take");
    }

    [Theory]
    [MemberData(nameof(EveryEntry))]
    public void Every_paragraph_naming_the_decisions_issue_says_that_question_is_open(string title)
    {
        foreach (var paragraph in ParagraphsNaming(Entry(title).Body, _decisionsIssue))
        {
            Assert.True(
                _openQuestion.IsMatch(paragraph),
                $"the entry \"{title}\" on the limits page names #8 without saying that the question there is open, so a limit resting on a decision nobody has taken reads as one somebody took");
        }
    }

    [Fact]
    public void The_page_names_the_decisions_issue_rather_than_leaving_the_leg_above_iterating_over_nothing()
    {
        // The same guard the resolution legs carry. Every entry that stopped
        // naming #8 would leave the leg above walking an empty list at every
        // heading, and the day the last of them went would look exactly like
        // today.
        var naming = Entries(Page())
            .SelectMany(entry => ParagraphsNaming(entry.Body, _decisionsIssue))
            .ToList();

        Assert.NotEmpty(naming);
    }

    [Fact]
    public void The_reader_refuses_an_entry_that_names_the_decisions_issue_as_though_it_were_answered()
    {
        // The accident this is for is the one the page had until #57 was read
        // entry by entry: the out-of-process limit rested on the first question of
        // #8 and named only the issue that built the branch the plan assumes, so a
        // reader met a decision where there is an assumption. The fixture is the
        // louder half of the same shape, where the number is there and the word
        // that says it is unanswered is not.
        var entry = Assert.Single(Entries(Fixture("a-question-named-as-though-it-were-answered")));
        var naming = ParagraphsNaming(entry.Body, _decisionsIssue);

        Assert.NotEmpty(naming);
        Assert.DoesNotContain(naming, paragraph => _openQuestion.IsMatch(paragraph));
        Assert.True(_state.IsMatch(entry.Body) && _issue.IsMatch(entry.Body), "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_page_reads_the_same_whatever_the_checkout_did_to_its_line_endings()
    {
        // Both forms, from the same bytes, rather than a claim about the
        // expressions. The page is tracked text under `* text=auto`, so git stores
        // a line feed and the checkout decides what the file on disk ends its
        // lines with. A reader that parsed to nothing on one of the two would
        // report a page missing every entry, which reads as documentation that
        // fell behind rather than as a check that cannot read it.
        var asLineFeeds = Page().Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        var fromLineFeeds = Entries(asLineFeeds).Select(entry => entry.Title).ToList();
        var fromCarriageReturns = Entries(asCarriageReturns).Select(entry => entry.Title).ToList();

        Assert.NotEmpty(fromLineFeeds);
        Assert.Equal(fromLineFeeds, fromCarriageReturns);
    }

    [Fact]
    public void No_fixture_is_a_document_anything_else_reads()
    {
        // The extension is the whole of what keeps these out of the way of a
        // documentation check that walks the tree for markdown, and a fixture that
        // acquired a plain one would be a second limits page saying things about
        // this repository that are deliberately untrue. The README beside them is
        // the one document in that directory that is true, so it is named rather
        // than matched by a pattern that would also let a fixture through.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, path => Assert.EndsWith(".md.fixture", path, StringComparison.Ordinal));
    }

    /// <summary>
    /// The accuracy entry rests on a sentence in another file, and this reads that
    /// file rather than the page's account of it.
    /// </summary>
    /// <remarks>
    /// It is the only claim on this page whose evidence is WORDING somebody can
    /// change without opening the page. Every other entry points at a source file
    /// or a suite, and the two resolution legs above ask whether those names exist,
    /// which is enough for evidence that is evidence by existing. The manifest
    /// description is shipped to an operator and is the text a catalogue shows, so
    /// it is edited for reasons that have nothing to do with this page, and the
    /// accident this refuses is the promise leaving build.yaml while the page goes
    /// on saying the manifest carries one.
    ///
    /// What it compares is one claim and not the paragraph around it. A description
    /// carrying the promise inside a sentence that says something else passes, and
    /// so does a page drawing a wrong conclusion from wording that is really there.
    /// It has no opinion about the other half of the entry either: that the marking
    /// was decided in the issue named beside it is a tracker fact, and this suite
    /// reaches no tracker.
    /// </remarks>
    [Fact]
    public void The_manifest_carries_the_promise_the_accuracy_entry_says_it_carries()
    {
        var entry = Entries(Page())
            .SingleOrDefault(section => section.Title.Equals(Accuracy, StringComparison.Ordinal));

        Assert.True(
            entry is not null,
            $"the limits page carries no entry titled \"{Accuracy}\", so this leg compared nothing");

        Assert.Contains(
            "build.yaml",
            Backticked(entry!.Body),
            StringComparer.Ordinal);

        var description = ManifestDescription();

        Assert.Contains(
            "promises nothing about its accuracy",
            description,
            StringComparison.Ordinal);

        Assert.Contains(
            "does not measure",
            description,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Guards the reader above rather than the manifest, for the reason
    /// <c>PluginIdentityTests</c> guards its own: a reader that quietly stopped
    /// finding the block would make the leg above pass by comparing nothing.
    /// </summary>
    [Fact]
    public void The_description_reader_returns_the_block_and_stops_at_the_next_key()
    {
        var description = ManifestDescription();

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.DoesNotContain("category:", description, StringComparison.Ordinal);
        Assert.DoesNotContain("artifacts:", description, StringComparison.Ordinal);
    }

    /// <summary>
    /// The manifest's description, which is a folded block scalar and so is outside
    /// what <c>PluginIdentityTests</c>'s line reader declares it can do.
    /// </summary>
    /// <remarks>
    /// Still a line reader and not a YAML parser. It takes the indented lines that
    /// follow the key and joins them the way a folded block folds, which is what
    /// this manifest's description is written as. It reads the checkout rather than
    /// the copy beside the assembly, so it is the same bytes the page beside it is
    /// read from.
    /// </remarks>
    private static string ManifestDescription()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), "build.yaml"));
        var start = Array.FindIndex(lines, line => line.StartsWith("description:", StringComparison.Ordinal));

        Assert.True(
            start >= 0,
            "build.yaml declares no description, so there is nothing for the accuracy entry to rest on");

        return string.Join(
            " ",
            lines.Skip(start + 1)
                .TakeWhile(line => line.Length > 0 && char.IsWhiteSpace(line[0]))
                .Select(line => line.Trim()));
    }

    private static Section Entry(string title) =>
        Entries(Page()).Single(entry => entry.Title.Equals(title, StringComparison.Ordinal));

    /// <summary>
    /// The one paragraph of an entry that names a kind, as one line.
    /// </summary>
    /// <remarks>
    /// Exactly one, and the assertion is part of the rule rather than a convenience.
    /// A list that named a kind in two places would let a marker in either of them
    /// answer for both, which is the hole this leg exists to close one level up. A
    /// kind named nowhere is refused here too, because the phrase is the same one
    /// <see cref="WriteLocationsTests"/> resolves against the page, and a leg walking
    /// past a phrase that has been reworded would report every kind as stated.
    ///
    /// Flattened for the reason its neighbour gives: the page names its third kind
    /// across a wrap, so a reader comparing the text as written would answer to where
    /// somebody's editor broke the line.
    /// </remarks>
    private static string ParagraphNaming(string body, string phrase)
    {
        var naming = _paragraphBreak.Split(body)
            .Select(Flattened)
            .Where(paragraph => paragraph.Contains(phrase, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            naming.Count == 1,
            $"{naming.Count} paragraphs of this entry name the kind written as \"{phrase}\", and a state can only be asked of one");

        return naming[0];
    }

    /// <summary>
    /// Every paragraph of an entry that names a given issue, each as one line.
    /// </summary>
    /// <remarks>
    /// All of them rather than exactly one, which is where this differs from
    /// <see cref="ParagraphNaming"/>. A kind is named once by the rule that leg
    /// holds; an issue can be named twice in one entry for two different reasons,
    /// and a reader that took the first would let the second stand unread.
    ///
    /// Flattened for the reason its neighbour gives. Both halves of the question
    /// asked of these paragraphs, the number and the words saying it is
    /// unanswered, are the kind of thing somebody's editor breaks a line between.
    /// </remarks>
    private static List<string> ParagraphsNaming(string body, Regex naming) =>
        _paragraphBreak.Split(body)
            .Select(Flattened)
            .Where(paragraph => naming.IsMatch(paragraph))
            .ToList();

    private static string Flattened(string paragraph) =>
        string.Join(' ', paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static List<string> Backticked(string body) =>
        _backticked.Matches(body).Select(match => match.Groups[1].Value).ToList();

    /// <summary>
    /// Whether a name the page writes as a path is a file somebody can open.
    /// </summary>
    /// <remarks>
    /// Two roots, because the page writes two kinds of path and both are correct
    /// where they are used. Anything outside the plugin project is written from the
    /// root of the tree, and a source file inside it is written from the project
    /// directory, which is how a reader of that entry would look for it.
    /// </remarks>
    private static bool Resolves(string named) =>
        File.Exists(Path.Combine(RepositoryRoot(), named))
        || File.Exists(Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.WhisperSubtitles", named));

    /// <summary>
    /// The classes this assembly would run a test in, by name.
    /// </summary>
    /// <remarks>
    /// From the loaded assembly rather than from the source files beside it,
    /// because what the page claims is that the coverage RUNS, and a class excluded
    /// from the compilation still has a file. TheoryAttribute derives from
    /// FactAttribute, so both shapes are found by asking for the one.
    /// </remarks>
    private static HashSet<string> ClassesThisSuiteRunsTestsIn() =>
        typeof(LimitsPageTests).Assembly
            .GetTypes()
            .Where(type => type.GetMethods().Any(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static List<Section> Entries(string page)
    {
        var sections = Sections(page);

        // The closing section is about the list rather than a limit in it, so it
        // carries no state and names no evidence. It is dropped by position and
        // the leg above holds the position, rather than being dropped wherever its
        // title turns up, so a page that grew an entry after it fails loudly.
        return sections.Count > 0 && sections[^1].Title.Equals(Closing, StringComparison.Ordinal)
            ? sections[..^1]
            : sections;
    }

    private static List<Section> Sections(string page)
    {
        var headings = _heading.Matches(page).ToList();
        var sections = new List<Section>();

        for (var i = 0; i < headings.Count; i++)
        {
            var start = headings[i].Index + headings[i].Length;
            var end = i + 1 < headings.Count ? headings[i + 1].Index : page.Length;

            sections.Add(new Section(headings[i].Groups["title"].Value, page[start..end]));
        }

        return sections;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".md.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "limits-page");

    /// <summary>
    /// The limits page, read out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason its
    /// neighbours give: sources are not copied beside the assembly, and a path
    /// walked upwards from the assembly depends on the configuration and the
    /// framework it was built for. It is also the route that lets the paths the
    /// page names be resolved against the tree at all, which a copy beside the
    /// assembly could not do.
    /// </remarks>
    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "limits.md"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private sealed record Section(string Title, string Body);
}
