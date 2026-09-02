using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The release checklist names, per condition, the thing that answers it, and this
/// refuses an item that names nothing.
/// </summary>
/// <remarks>
/// A checklist is read once a year, at the worst moment this repository has, and it
/// is believed. That is the whole reason it is worth a reader: an item saying a
/// condition is met, with nothing behind it, is stronger than the same sentence
/// anywhere else, because the person reading it has already decided to publish.
///
/// So the page keeps two states apart at every item, and this holds the shape of
/// both. An item that says a run decides it names a command or a status check whose
/// verdict is the answer. An item that says nothing decides it yet names the issue
/// where a route is owed, so a reader who thinks the condition matters can argue
/// with that issue rather than with this page. An item in neither state reads as an
/// assurance, which is the direction the page exists against.
///
/// The last leg is a different failure and the one nobody would find by reading this
/// page. A condition arrives here from somewhere else: <c>docs/limits.md</c> already
/// says that the state of every limit is re-read at the first release and that the
/// checklist is where that condition belongs. A page that hands a condition over and
/// a checklist that never grew the item are both correct on their own, and the
/// condition is simply not checked at the release. So a page under <c>docs/</c> that
/// speaks of the release checklist has to be named by an item on it.
///
/// WHAT THIS DOES NOT DO, and the first bound is the largest. It does not judge
/// whether an item's answer is TRUE, or whether the command it names would return
/// what the item claims. That is a reading of two things this cannot compare, and
/// the review is where a wrong answer is caught. What it refuses is an item that
/// named nothing to compare against.
///
/// It does not reach the tracker, by the rule its neighbours keep, so an item filed
/// as waiting on an issue that closed yesterday stays green until a person moves it.
///
/// It does not reach the publish run either, and that is the clause of #62 this
/// class is not. Nothing here makes a release refuse to publish while an item is
/// unanswered: <c>.github/workflows/publish.yaml</c> reads no part of this page, and
/// the closing section says so in the page's own words.
///
/// It reads what is inside backticks and nothing inside a fenced block, so a file
/// named in plain prose is invisible to the resolution legs. Those legs carry their
/// own guard below rather than being trusted to have iterated over anything.
///
/// Each leg carries a fixture it has to refuse, under
/// <c>Fixtures/release-checklist/</c>, so the proof it bites is in the tree rather
/// than in the memory of whoever last broke the page on purpose. One fixture per
/// leg, because a leg is proven by a case that trips it AND NO OTHER, and the
/// neighbour that breaks nothing has to stay accepted or a reader refusing every
/// item would pass every leg.
/// </remarks>
public class ReleaseChecklistTests
{
    /// <summary>
    /// The one section of the page that is about the list rather than a condition in it.
    /// </summary>
    private const string Closing = "When an item has no answer";

    /// <summary>
    /// The page itself, which speaks of the release checklist because it is one.
    /// </summary>
    private const string ThePage = "docs/release-checklist.md";

    private static readonly Regex _heading = new(
        @"^## (?<title>.+?)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The machine-read vocabulary, and the whole of it. Narrowing or widening this is
    // a change to the page rather than to the expression.
    private static readonly Regex _decidedByARun = new(
        @"Decided by a run",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _decidedByNothing = new(
        @"Nothing decides this yet",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The figure the closing section states about the items nothing decides yet.
    /// </summary>
    /// <remarks>
    /// The page writes it as a count of the items above rather than as a bare word,
    /// so the expression anchors on what the figure is a count OF. A sentence
    /// elsewhere on the page carrying a number is not this figure and is not read as
    /// one, because the closing section is the only place it is looked for. Both
    /// verbs are accepted because a page with one such item writes the singular, and
    /// a reader that took only the plural would call that page silent. The gaps are
    /// whitespace rather than a space, because the sentence is wrapped and a reader
    /// that required a space would call a page silent for where its line broke.
    /// </remarks>
    private static readonly Regex _itemsWithNoRoute = new(
        @"(?<count>[A-Za-z]+)\s+of\s+the\s+items\s+above\s+ha(?:ve|s)\s+no\s+route",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The number words this page may write its figure in.
    /// </summary>
    /// <remarks>
    /// Bounded on purpose and short of every word English has. A figure the table
    /// does not hold is unreadable rather than nought, so a page spelling it in a
    /// word nothing here knows is refused by the leg that requires one instead of
    /// being compared against a number nobody wrote. The ceiling is well above the
    /// number of conditions a release checklist could carry and be read at all.
    /// </remarks>
    private static readonly Dictionary<string, int> _figures = new(StringComparer.Ordinal)
    {
        ["None"] = 0,
        ["none"] = 0,
        ["One"] = 1,
        ["one"] = 1,
        ["Two"] = 2,
        ["two"] = 2,
        ["Three"] = 3,
        ["three"] = 3,
        ["Four"] = 4,
        ["four"] = 4,
        ["Five"] = 5,
        ["five"] = 5,
        ["Six"] = 6,
        ["six"] = 6,
        ["Seven"] = 7,
        ["seven"] = 7,
        ["Eight"] = 8,
        ["eight"] = 8,
        ["Nine"] = 9,
        ["nine"] = 9,
        ["Ten"] = 10,
        ["ten"] = 10,
        ["Eleven"] = 11,
        ["eleven"] = 11,
        ["Twelve"] = 12,
        ["twelve"] = 12,
    };

    /// <summary>
    /// A status check named in prose: the context in backticks, on one line, then the
    /// word it is the name of. The same shape <see cref="NamedChecksTests"/> resolves
    /// against the jobs this tree declares, so an item satisfying this leg with an
    /// invented name is refused there rather than here.
    /// </summary>
    private static readonly Regex _checkNamed = new(
        @"`([^`\r\n]+)`\s+check",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A fenced block, which is how the page writes a command somebody runs.
    /// </summary>
    private static readonly Regex _fenced = new(
        @"^```[^\r\n]*\r?$(?<body>.*?)^```[^\r\n]*\r?$",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _backticked = new(
        @"`([^`\r\n]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _issue = new(
        @"#[0-9]+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // A name is treated as a path when it ends in an extension this tree writes files
    // with. The archive extension the page quotes is deliberately not one of them:
    // `.zip` is what a release carries and not a file anybody could resolve here.
    private static readonly Regex _pathShaped = new(
        @"^[A-Za-z0-9_.\-]+(/[A-Za-z0-9_.\-]+)*\.(cs|sh|md|txt|yaml|yml|props|targets|html|json)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _suiteShaped = new(
        @"^[A-Za-z0-9_]+Tests$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A page speaking of this list, in either spelling: the words, or the file name
    /// somebody points a reader at.
    /// </summary>
    private static readonly Regex _handsAConditionHere = new(
        @"release[ -]checklist",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    public static TheoryData<string> EveryItem =>
        new(Items(Page()).Select(item => item.Title).ToArray());

    public static TheoryData<string> EveryItemARunDecides =>
        new(Items(Page()).Where(item => _decidedByARun.IsMatch(item.Body)).Select(item => item.Title).ToArray());

    public static TheoryData<string> EveryItemNothingDecidesYet =>
        new(Items(Page()).Where(item => _decidedByNothing.IsMatch(item.Body)).Select(item => item.Title).ToArray());

    [Fact]
    public void The_reader_finds_the_items_and_stops_before_the_section_that_is_about_them()
    {
        // Guards every leg below. A reader that found no items would report a page
        // whose every condition names something, whatever the page said, and it would
        // do it in green. The closing section is excluded by being last rather than by
        // name alone, so an item added after it is refused here instead of being
        // silently dropped out of the population.
        var sections = Sections(Page());
        var items = Items(Page());

        Assert.True(items.Count > 1, $"the reader found {items.Count} items on the release checklist");
        Assert.Equal(Closing, sections[^1].Title);
        Assert.DoesNotContain(items, item => item.Title.Equals(Closing, StringComparison.Ordinal));
        Assert.Contains(items, item => item.Title.Contains("merge gate", StringComparison.Ordinal));
    }

    [Fact]
    public void Both_states_are_on_the_page_rather_than_one_of_them_answering_for_every_item()
    {
        // The two legs keyed on a state walk whichever items carry it, so a page that
        // lost one state entirely would leave one of them iterating over nothing. It
        // is also the claim the page makes about this repository: some conditions are
        // answered by a run today and some are answered by nobody.
        var items = Items(Page());

        Assert.Contains(items, item => _decidedByARun.IsMatch(item.Body));
        Assert.Contains(items, item => _decidedByNothing.IsMatch(item.Body));
    }

    [Fact]
    public void The_figure_the_closing_section_states_is_the_number_of_items_nothing_decides_yet()
    {
        // The page says how many of its conditions have no route at all, and a person
        // cutting a release reads that figure before deciding whether the incompleteness
        // is the one this repository already knows about. It was kept by hand until now,
        // and this page has been wrong about a figure it stated about itself twice.
        var stated = FigureStated(Page());
        var counted = Items(Page()).Count(item => _decidedByNothing.IsMatch(item.Body));

        Assert.True(
            stated is not null,
            "the closing section of the release checklist states no figure for the items nothing decides yet, so the count a releaser reads there is a word rather than a number this reader could compare");
        Assert.True(
            stated == counted,
            $"the release checklist says {stated} of its items have no route that answers them and {counted} of them say nothing decides them yet");
    }

    [Fact]
    public void The_reader_refuses_a_closing_section_whose_figure_is_not_the_number_of_such_items()
    {
        // The failure the leg above exists against: an item gains a route, or one is
        // added with none, and the sentence a releaser reads goes on stating the figure
        // it was right about on the day somebody typed it.
        var page = Fixture("a-figure-that-is-not-the-number-of-items");

        Assert.NotEqual(
            Items(page).Count(item => _decidedByNothing.IsMatch(item.Body)),
            FigureStated(page));
        Assert.All(
            Items(page),
            item => Assert.True(
                State(item.Body) is not null && !(_decidedByARun.IsMatch(item.Body) && _decidedByNothing.IsMatch(item.Body)),
                "the fixture has to trip this leg and no other"));
    }

    [Fact]
    public void The_reader_refuses_a_closing_section_that_speaks_of_such_items_and_states_no_figure()
    {
        // The other direction, and the cheaper mistake: the sentence is rewritten, the
        // figure falls out of it, and a leg comparing two numbers has one of them.
        var page = Fixture("a-closing-section-that-states-no-figure");

        Assert.Null(FigureStated(page));
        Assert.All(
            Items(page),
            item => Assert.True(
                State(item.Body) is not null && !(_decidedByARun.IsMatch(item.Body) && _decidedByNothing.IsMatch(item.Body)),
                "the fixture has to trip this leg and no other"));
    }

    [Theory]
    [MemberData(nameof(EveryItem))]
    public void Every_item_is_filed_under_one_of_the_two_states_the_page_keeps_apart(string title)
    {
        var item = Item(title);

        Assert.True(
            State(item.Body) is not null,
            $"the item \"{title}\" on the release checklist says neither that a run decides it nor that nothing decides it yet, so a reader cannot tell a condition a machine answers from one somebody has to answer by hand");
    }

    [Theory]
    [MemberData(nameof(EveryItem))]
    public void No_item_claims_both_states_at_once(string title)
    {
        var item = Item(title);

        Assert.False(
            _decidedByARun.IsMatch(item.Body) && _decidedByNothing.IsMatch(item.Body),
            $"the item \"{title}\" on the release checklist carries both states, so whichever leg reads it first decides what the item promised");
    }

    [Theory]
    [MemberData(nameof(EveryItemNothingDecidesYet))]
    public void Every_item_nothing_decides_yet_names_the_issue_where_a_route_is_owed(string title)
    {
        var item = Item(title);

        Assert.True(
            _issue.IsMatch(item.Body),
            $"the item \"{title}\" on the release checklist says nothing decides it yet and names no issue, so the condition is one nobody is holding and a reader who disagrees has nothing to argue with");
    }

    [Theory]
    [MemberData(nameof(EveryItemARunDecides))]
    public void Every_item_a_run_decides_names_the_command_or_the_check_that_decides_it(string title)
    {
        var item = Item(title);

        Assert.True(
            NamesSomethingThatAnswers(item.Body),
            $"the item \"{title}\" on the release checklist says a run decides it and names neither a command in a fenced block nor a status check, so the recorded result it promises comes from nowhere");
    }

    [Fact]
    public void Every_file_the_page_points_a_reader_at_is_a_file_this_tree_has()
    {
        var missing = Backticked(WithoutFences(Page()))
            .Where(name => _pathShaped.IsMatch(name))
            .Where(name => !Resolves(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"the release checklist points a reader at {string.Join(", ", missing)}, and this tree has no such file. At a release that is somebody looking for the evidence of a condition and not finding it.");
    }

    [Fact]
    public void Every_suite_the_page_says_holds_a_condition_is_one_this_assembly_runs()
    {
        var running = ClassesThisSuiteRunsTestsIn();
        var missing = Backticked(WithoutFences(Page()))
            .Where(name => _suiteShaped.IsMatch(name))
            .Where(name => !running.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"the release checklist says {string.Join(", ", missing)} holds a condition and this assembly runs no tests in a class by that name");
    }

    [Fact]
    public void The_page_names_files_and_suites_rather_than_leaving_the_two_legs_above_iterating_over_nothing()
    {
        // The other half of guarding the resolution legs. If the page named nothing of
        // either shape they would walk an empty list and pass without resolving a
        // single name, and the day the last one was deleted would look exactly like
        // today.
        var named = Backticked(WithoutFences(Page()));

        Assert.Contains(named, name => _pathShaped.IsMatch(name));
        Assert.Contains(named, name => _suiteShaped.IsMatch(name));
    }

    [Fact]
    public void Every_page_that_hands_a_condition_to_this_list_is_named_by_an_item_on_it()
    {
        var unnamed = PagesThisListDoesNotName(Page(), PagesUnderDocs());

        Assert.True(
            unnamed.Count == 0,
            $"{string.Join(", ", unnamed)} speaks of the release checklist and no item on it names that page, so a condition was handed over and the list never grew the item. A release is then cut without it and nothing says so.");
    }

    [Fact]
    public void The_scanner_can_see_the_pages_it_judges()
    {
        // Without this the leg above passes on a tree whose pages moved out of its
        // subject, and a reader that found no page at all would report that every
        // condition handed to this list arrived.
        var pages = PagesUnderDocs();

        Assert.NotEmpty(pages);
        Assert.Contains(pages, page => page.Name.Equals("docs/limits.md", StringComparison.Ordinal));
        Assert.DoesNotContain(pages, page => page.Name.Equals(ThePage, StringComparison.Ordinal));
    }

    [Fact]
    public void The_reader_refuses_an_item_in_neither_state()
    {
        var item = Assert.Single(Items(Fixture("an-item-in-neither-state")));

        Assert.Null(State(item.Body));
        Assert.True(
            _issue.IsMatch(item.Body) && NamesSomethingThatAnswers(item.Body),
            "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_an_item_carrying_both_states_at_once()
    {
        var item = Assert.Single(Items(Fixture("an-item-in-both-states")));

        Assert.True(_decidedByARun.IsMatch(item.Body) && _decidedByNothing.IsMatch(item.Body));
        Assert.True(
            _issue.IsMatch(item.Body) && NamesSomethingThatAnswers(item.Body),
            "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_an_item_nothing_decides_that_names_no_issue()
    {
        var item = Assert.Single(Items(Fixture("an-item-nothing-decides-that-names-no-issue")));

        Assert.DoesNotMatch(_issue, item.Body);
        Assert.True(_decidedByNothing.IsMatch(item.Body), "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_an_item_a_run_decides_that_names_neither_a_command_nor_a_check()
    {
        // The shape this page is written against. It reads exactly like the item beside
        // it and the recorded result it promises comes from nowhere.
        var item = Assert.Single(Items(Fixture("an-item-that-names-nothing-that-answers-it")));

        Assert.False(NamesSomethingThatAnswers(item.Body));
        Assert.True(
            _decidedByARun.IsMatch(item.Body) && _issue.IsMatch(item.Body),
            "the fixture has to trip this leg and no other");
    }

    [Fact]
    public void The_reader_refuses_a_page_that_points_at_a_file_this_tree_does_not_have()
    {
        var named = Backticked(WithoutFences(Fixture("a-path-that-is-not-there")))
            .Where(name => _pathShaped.IsMatch(name))
            .ToList();

        Assert.NotEmpty(named);
        Assert.DoesNotContain(named, Resolves);
        Assert.All(
            Items(Fixture("a-path-that-is-not-there")),
            item => Assert.True(
                State(item.Body) is not null && NamesSomethingThatAnswers(item.Body),
                "the fixture has to trip this leg and no other"));
    }

    [Fact]
    public void The_reader_refuses_a_list_that_leaves_out_a_page_handing_it_a_condition()
    {
        // The failure nobody would find by reading either file. Both are correct on
        // their own and the condition is checked at no release.
        var handed = new[] { ("docs/somewhere-else.md", Fixture("speaks-of-the-release-checklist")) };

        Assert.Equal(
            ["docs/somewhere-else.md"],
            PagesThisListDoesNotName(Fixture("clean"), handed));
    }

    [Fact]
    public void A_page_this_list_does_name_is_accepted()
    {
        // The other direction of the leg above, and the state the tree is in today:
        // `docs/limits.md` hands a condition here and an item names it.
        var handed = new[] { ("docs/limits.md", Fixture("speaks-of-the-release-checklist")) };

        Assert.Empty(PagesThisListDoesNotName(Fixture("clean"), handed));
    }

    [Fact]
    public void A_page_that_says_nothing_about_this_list_is_not_asked_to_be_named()
    {
        // Without this the leg would demand an item for every page under docs/, which
        // is a rule about documentation rather than about conditions.
        var quiet = new[] { ("docs/somewhere-else.md", Fixture("says-nothing-about-the-release-checklist")) };

        Assert.Empty(PagesThisListDoesNotName(Fixture("clean"), quiet));
    }

    [Fact]
    public void The_reader_refuses_a_page_with_no_items_left_in_it()
    {
        // The fixture for the guard rather than for a rule: a page whose items stopped
        // being sections the reader recognises reads as a list with nothing in it,
        // which is the shape that passes every other leg for free.
        Assert.Empty(Items(Fixture("no-items-at-all")));
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        // Without this a reader that refused every item would pass every leg above.
        var items = Items(Fixture("clean"));
        var running = ClassesThisSuiteRunsTestsIn();

        Assert.Equal(2, items.Count);
        Assert.Equal(
            items.Count(item => _decidedByNothing.IsMatch(item.Body)),
            FigureStated(Fixture("clean")));
        Assert.All(items, item => Assert.True(State(item.Body) is not null));
        Assert.All(items, item => Assert.False(_decidedByARun.IsMatch(item.Body) && _decidedByNothing.IsMatch(item.Body)));
        Assert.All(
            items.Where(item => _decidedByNothing.IsMatch(item.Body)),
            item => Assert.Matches(_issue, item.Body));
        Assert.All(
            items.Where(item => _decidedByARun.IsMatch(item.Body)),
            item => Assert.True(NamesSomethingThatAnswers(item.Body)));
        Assert.All(
            Backticked(WithoutFences(Fixture("clean"))).Where(name => _pathShaped.IsMatch(name)),
            name => Assert.True(Resolves(name), $"the neighbour points at {name}, which this tree does not have"));
        Assert.All(
            Backticked(WithoutFences(Fixture("clean"))).Where(name => _suiteShaped.IsMatch(name)),
            name => Assert.Contains(name, running));
    }

    [Fact]
    public void The_page_reads_the_same_whatever_the_checkout_did_to_its_line_endings()
    {
        // Both forms, from the same bytes, rather than a claim about the expressions.
        // The page is tracked text under `* text=auto`, so git stores a line feed and
        // the checkout decides what the file on disk ends its lines with. A reader that
        // parsed to nothing on one of the two would report a list missing every item,
        // which reads as documentation that fell behind rather than as a check that
        // cannot read it.
        var asLineFeeds = Page().Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        var fromLineFeeds = Read(asLineFeeds);
        var fromCarriageReturns = Read(asCarriageReturns);

        Assert.NotEmpty(fromLineFeeds);
        Assert.Equal(fromLineFeeds, fromCarriageReturns);

        static List<string> Read(string page) =>
            Items(page)
                .Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.Title}: {State(item.Body)}, answered by something it names: {NamesSomethingThatAnswers(item.Body)}"))
                .ToList();
    }

    [Fact]
    public void No_fixture_is_a_document_anything_else_reads()
    {
        // The extension is the whole of what keeps these out of the way of a
        // documentation check that walks the tree for markdown, and a fixture that
        // acquired a plain one would be a second release checklist saying things about
        // this repository that are deliberately untrue. The README beside them is the
        // one document in that directory that is true, so it is named rather than
        // matched by a pattern that would also let a fixture through.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, path => Assert.EndsWith(".md.fixture", path, StringComparison.Ordinal));
    }

    /// <summary>
    /// The pages that hand a condition to this list and are named by no item on it.
    /// </summary>
    /// <param name="checklist">The checklist text.</param>
    /// <param name="pages">The pages to judge, by the name a reader would write.</param>
    /// <returns>The names, ordered so a failure names them the same way twice.</returns>
    private static List<string> PagesThisListDoesNotName(
        string checklist,
        IEnumerable<(string Name, string Text)> pages)
    {
        var named = Backticked(WithoutFences(checklist)).ToHashSet(StringComparer.Ordinal);

        return pages
            .Where(page => _handsAConditionHere.IsMatch(page.Text))
            .Where(page => !named.Contains(page.Name))
            .Select(page => page.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Whether an item names something whose verdict could be the answer.
    /// </summary>
    /// <param name="body">The item body.</param>
    /// <returns>True where a command or a status check is named.</returns>
    private static bool NamesSomethingThatAnswers(string body) =>
        _fenced.IsMatch(body) || _checkNamed.IsMatch(WithoutFences(body));

    /// <summary>
    /// Which state an item is in, or nothing where it is in neither.
    /// </summary>
    /// <param name="body">The item body.</param>
    /// <returns>The state, or null.</returns>
    private static string? State(string body)
    {
        if (_decidedByARun.IsMatch(body))
        {
            return "decided by a run";
        }

        return _decidedByNothing.IsMatch(body) ? "decided by nothing yet" : null;
    }

    /// <summary>
    /// The figure the closing section states, or nothing where it states none this
    /// reader can turn into a number.
    /// </summary>
    /// <remarks>
    /// Read out of the closing section rather than out of the page, because the
    /// closing section is the one part that is about the list instead of a condition
    /// in it, and a figure anywhere else is about something else.
    /// </remarks>
    /// <param name="page">The page text.</param>
    /// <returns>The figure, or null.</returns>
    private static int? FigureStated(string page)
    {
        var closing = Sections(page).LastOrDefault(section => section.Title.Equals(Closing, StringComparison.Ordinal));

        if (closing is null)
        {
            return null;
        }

        var stated = _itemsWithNoRoute.Match(closing.Body);

        return stated.Success && _figures.TryGetValue(stated.Groups["count"].Value, out var figure)
            ? figure
            : null;
    }

    private static Section Item(string title) =>
        Items(Page()).Single(item => item.Title.Equals(title, StringComparison.Ordinal));

    private static List<string> Backticked(string body) =>
        _backticked.Matches(body).Select(match => match.Groups[1].Value).ToList();

    /// <summary>
    /// The text with every fenced block removed.
    /// </summary>
    /// <remarks>
    /// A command is not a claim about this tree. It quotes paths that do not exist
    /// here on purpose, a placeholder for the commit being released among them, and a
    /// resolution leg reading inside a fence would refuse the page for saying what a
    /// person types.
    /// </remarks>
    /// <param name="text">The text to strip.</param>
    /// <returns>The text outside every fence.</returns>
    private static string WithoutFences(string text) =>
        _fenced.Replace(text, "\n");

    /// <summary>
    /// Whether a name the page writes as a path is a file somebody can open.
    /// </summary>
    /// <param name="named">The name as the page writes it.</param>
    /// <returns>True where the tree has such a file.</returns>
    private static bool Resolves(string named) =>
        File.Exists(Path.Combine(RepositoryRoot(), named));

    /// <summary>
    /// The classes this assembly would run a test in, by name.
    /// </summary>
    /// <remarks>
    /// From the loaded assembly rather than from the source files beside it, because
    /// what the page claims is that the coverage RUNS, and a class excluded from the
    /// compilation still has a file. TheoryAttribute derives from FactAttribute, so
    /// both shapes are found by asking for the one.
    /// </remarks>
    /// <returns>The class names.</returns>
    private static HashSet<string> ClassesThisSuiteRunsTestsIn() =>
        typeof(ReleaseChecklistTests).Assembly
            .GetTypes()
            .Where(type => type.GetMethods().Any(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Every page under docs/ except this list itself, by the name a reader writes.
    /// </summary>
    /// <returns>The name and the text of each.</returns>
    private static List<(string Name, string Text)> PagesUnderDocs() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "docs"), "*.md")
            .Select(path => (
                Name: string.Create(CultureInfo.InvariantCulture, $"docs/{Path.GetFileName(path)}"),
                Text: File.ReadAllText(path)))
            .Where(page => !page.Name.Equals(ThePage, StringComparison.Ordinal))
            .OrderBy(page => page.Name, StringComparer.Ordinal)
            .ToList();

    private static List<Section> Items(string page)
    {
        var sections = Sections(page);

        // The closing section is about the list rather than a condition in it, so it
        // carries no state and names no answer. It is dropped by position and the leg
        // above holds the position, rather than being dropped wherever its title turns
        // up, so a page that grew an item after it fails loudly.
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
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "release-checklist");

    /// <summary>
    /// The release checklist, read out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason its neighbours
    /// give: sources are not copied beside the assembly, and a path walked upwards
    /// from the assembly depends on the configuration and the framework it was built
    /// for. It is also the route that lets the paths the page names be resolved
    /// against the tree at all, which a copy beside the assembly could not do.
    /// </remarks>
    /// <returns>The page text.</returns>
    private static string Page() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "release-checklist.md"));

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private sealed record Section(string Title, string Body);
}
