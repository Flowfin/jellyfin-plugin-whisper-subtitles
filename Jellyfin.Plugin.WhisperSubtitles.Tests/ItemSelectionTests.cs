using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Selection decides what a run costs, so what is asserted here is the exact list
/// and the exact total rather than that something plausible came back.
/// </summary>
public class ItemSelectionTests
{
    private static readonly Guid _enabledLibrary = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _otherLibrary = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset _epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_item_in_a_library_the_operator_did_not_enable_is_not_a_candidate()
    {
        var wanted = Item("in", library: _enabledLibrary);
        var elsewhere = Item("out", library: _otherLibrary);

        Assert.Equal(new[] { wanted }, Select(new[] { wanted, elsewhere }).Candidates);
    }

    [Fact]
    public void An_item_of_a_type_out_of_scope_is_not_a_candidate()
    {
        var wanted = Item("episode", kind: "Episode");
        var unwanted = Item("song", kind: "Audio");

        var result = Select(new[] { wanted, unwanted }, Options(kinds: new[] { "Episode" }));

        Assert.Equal(new[] { wanted }, result.Candidates);
    }

    [Fact]
    public void The_type_is_matched_the_way_the_server_spells_it_rather_than_exactly()
    {
        // The scope is configuration a person edits and the kind comes from the
        // server, so the two arrive spelled differently often enough to matter.
        var item = Item("episode", kind: "Episode");

        Assert.Single(Select(new[] { item }, Options(kinds: new[] { "episode" })).Candidates);
    }

    [Fact]
    public void An_item_with_no_audio_stream_is_not_a_candidate()
    {
        var wanted = Item("has audio", hasAudio: true);
        var silent = Item("no audio", hasAudio: false);

        Assert.Equal(new[] { wanted }, Select(new[] { wanted, silent }).Candidates);
    }

    [Fact]
    public void An_item_that_already_has_a_subtitle_in_the_target_language_is_not_a_candidate()
    {
        // From any source. An embedded track, a downloaded file and a file this
        // plugin wrote last week are the same fact to an operator watching it.
        var wanted = Item("none", subtitles: Array.Empty<string>());
        var embedded = Item("embedded", subtitles: new[] { "en" });
        var otherLanguage = Item("french only", subtitles: new[] { "fr" });

        var result = Select(new[] { wanted, embedded, otherLanguage });

        // Same DateAdded, so the tie is broken by name, which is the documented order.
        Assert.Equal(new[] { "french only", "none" }, result.Candidates.Select(c => c.Name));
    }

    [Fact]
    public void The_subtitle_language_is_compared_ignoring_case_and_space()
    {
        var item = Item("padded", subtitles: new[] { "  EN " });

        Assert.Empty(Select(new[] { item }).Candidates);
    }

    [Fact]
    public void An_item_longer_than_the_bound_the_operator_set_is_not_a_candidate()
    {
        var shortEnough = Item("short", duration: TimeSpan.FromMinutes(20));
        var tooLong = Item("long", duration: TimeSpan.FromMinutes(200));

        var result = Select(
            new[] { shortEnough, tooLong },
            Options(maximumDuration: TimeSpan.FromMinutes(20)));

        Assert.Equal(new[] { shortEnough }, result.Candidates);
    }

    [Fact]
    public void An_item_added_before_the_date_the_operator_set_is_not_a_candidate()
    {
        var old = Item("old", added: _epoch);
        var recent = Item("recent", added: _epoch.AddDays(10));

        var result = Select(new[] { old, recent }, Options(addedSince: _epoch.AddDays(5)));

        Assert.Equal(new[] { recent }, result.Candidates);
    }

    [Fact]
    public void An_item_exactly_on_a_bound_is_inside_it()
    {
        // Both bounds are inclusive, and both are the one-character mistake:
        // an item exactly as long as the maximum, and one added at the very moment
        // the operator typed, are the cases where a run and the estimate an
        // operator read disagree by one item.
        var exactlyAsLong = Item("exact length", duration: TimeSpan.FromMinutes(30));
        var addedExactlyThen = Item("exact moment", added: _epoch.AddDays(5));

        Assert.Single(Select(new[] { exactlyAsLong }, Options(maximumDuration: TimeSpan.FromMinutes(30))).Candidates);
        Assert.Single(Select(new[] { addedExactlyThen }, Options(addedSince: _epoch.AddDays(5))).Candidates);
    }

    [Fact]
    public void Two_bounds_at_once_are_both_applied()
    {
        // The interaction rather than each filter alone: an item excluded by one
        // and admitted by the other must stay excluded, in both directions.
        var passesBoth = Item("both", duration: TimeSpan.FromMinutes(10), added: _epoch.AddDays(10));
        var tooLongButRecent = Item("long recent", duration: TimeSpan.FromMinutes(90), added: _epoch.AddDays(10));
        var shortButOld = Item("short old", duration: TimeSpan.FromMinutes(10), added: _epoch);

        var result = Select(
            new[] { passesBoth, tooLongButRecent, shortButOld },
            Options(maximumDuration: TimeSpan.FromMinutes(30), addedSince: _epoch.AddDays(5)));

        Assert.Equal(new[] { passesBoth }, result.Candidates);
    }

    [Fact]
    public void A_subtitle_in_the_target_language_beats_every_other_bound()
    {
        var subtitled = Item("subtitled", duration: TimeSpan.FromMinutes(1), added: _epoch.AddDays(10), subtitles: new[] { "en" });

        var result = Select(
            new[] { subtitled },
            Options(maximumDuration: TimeSpan.FromHours(9), addedSince: _epoch));

        Assert.Empty(result.Candidates);
    }

    // One leg per spelling of "no target language" rather than a loop over the
    // three, so a spelling that stops failing closed is named by the run instead
    // of hiding behind whichever one broke first.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_target_language_selects_nothing(string? blank)
    {
        // Fail closed. "Does this item already have a subtitle in the language we
        // are about to write" has no answer without one, and the alternative is
        // transcribing a library into a language nobody asked for.
        var item = Item("anything");

        var result = Select(new[] { item }, Options(target: blank));

        Assert.Empty(result.Candidates);
        Assert.Equal(TimeSpan.Zero, result.TotalDuration);
    }

    [Fact]
    public void The_order_is_the_same_whatever_order_the_library_arrived_in()
    {
        // A run and the estimate an operator was shown before it have to be about
        // the same list, and the server does not promise an order.
        var items = Enumerable.Range(0, 50)
            .Select(i => Item(
                "item " + i.ToString("00", CultureInfo.InvariantCulture),
                added: _epoch.AddDays(i % 7)))
            .ToList();

        var forwards = Select(items).Candidates.Select(c => c.Id);
        var backwards = Select(items.AsEnumerable().Reverse().ToList()).Candidates.Select(c => c.Id);

        Assert.Equal(forwards, backwards);
    }

    [Fact]
    public void The_total_duration_is_the_candidates_added_up_and_not_the_library()
    {
        var result = Select(new[]
        {
            Item("a", duration: TimeSpan.FromMinutes(30)),
            Item("b", duration: TimeSpan.FromMinutes(45)),
            Item("excluded by subtitle", duration: TimeSpan.FromHours(5), subtitles: new[] { "en" }),
            Item("excluded by audio", duration: TimeSpan.FromHours(5), hasAudio: false)
        });

        Assert.Equal(TimeSpan.FromMinutes(75), result.TotalDuration);
    }

    [Fact]
    public void A_fabricated_library_of_three_hundred_items_gives_an_exact_answer()
    {
        // The issue asks for a few hundred items asserted exactly, which is also
        // the only size at which an off-by-one in a filter is visible as a number
        // rather than as a list somebody eyeballed.
        var library = new List<ItemDescription>();

        for (var i = 0; i < 300; i++)
        {
            library.Add(Item(
                "item " + i.ToString("000", CultureInfo.InvariantCulture),
                library: i % 5 == 0 ? _otherLibrary : _enabledLibrary,
                kind: i % 7 == 0 ? "Audio" : "Episode",
                duration: TimeSpan.FromMinutes(10),
                hasAudio: i % 11 != 0,
                subtitles: i % 13 == 0 ? new[] { "en" } : Array.Empty<string>(),
                added: _epoch.AddDays(i)));
        }

        var expected = library
            .Where(i => i.LibraryId == _enabledLibrary)
            .Where(i => i.Kind == "Episode")
            .Where(i => i.HasAudioStream)
            .Where(i => i.SubtitleLanguages.Count == 0)
            .OrderBy(i => i.DateAdded)
            .ToList();

        var result = Select(library, Options(kinds: new[] { "Episode" }));

        Assert.Equal(expected.Select(i => i.Id), result.Candidates.Select(c => c.Id));
        Assert.Equal(173, expected.Count);
        Assert.Equal(173, result.Candidates.Count);
        Assert.Equal(TimeSpan.FromMinutes(1730), result.TotalDuration);
    }

    private static SelectionResult Select(IReadOnlyList<ItemDescription> items, SelectionOptions? options = null) =>
        ItemSelection.Select(items, options ?? Options());

    private static SelectionOptions Options(
        IReadOnlyList<string>? kinds = null,
        string? target = "en",
        TimeSpan? maximumDuration = null,
        DateTimeOffset? addedSince = null) =>
        new(
            new[] { _enabledLibrary },
            kinds ?? new[] { "Episode", "Movie" },
            target,
            maximumDuration,
            addedSince);

    private static ItemDescription Item(
        string name,
        Guid? library = null,
        string kind = "Episode",
        TimeSpan? duration = null,
        bool hasAudio = true,
        IReadOnlyList<string>? subtitles = null,
        DateTimeOffset? added = null) =>
        new(
            DeterministicId(name),
            name,
            library ?? _enabledLibrary,
            kind,
            duration ?? TimeSpan.FromMinutes(42),
            hasAudio,
            subtitles ?? Array.Empty<string>(),
            added ?? _epoch);

    /// <summary>
    /// An identifier derived from the name, so a fixture is reproducible and two
    /// runs of the suite compare the same items.
    /// </summary>
    private static Guid DeterministicId(string name)
    {
        var bytes = new byte[16];
        var source = System.Text.Encoding.UTF8.GetBytes(name);

        for (var i = 0; i < source.Length; i++)
        {
            bytes[i % 16] ^= source[i];
        }

        return new Guid(bytes);
    }
}
