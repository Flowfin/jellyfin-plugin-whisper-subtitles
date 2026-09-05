using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The configuration page is the one surface an operator reads, it is a shell that
/// grows, and several changes edit it by hand. A page nothing formats is where a
/// review stops being able to see what moved: a reindentation and a change of
/// behaviour arrive in the same diff looking the same, and a reviewer who cannot
/// tell them apart reads neither.
/// </summary>
/// <remarks>
/// The tool is this file. That is the choice this change makes and the reason it
/// makes it: the obvious formatter for markup and script is a Node runtime, and
/// this tree builds with one SDK, pins what it restores in a lock file and audits
/// what its workflows run. A second runtime is a second dependency graph, a second
/// thing to pin and a second thing to keep current, bought for one file of a
/// hundred and sixty lines. The rules below run on what is already installed, each
/// one is a function a test calls with a page, and each carries a fixture it has to
/// refuse, so proving one bites costs a fixture rather than a page somebody breaks
/// on purpose and remembers to put back.
///
/// It needs no new required check either. The suite is what the mainline already
/// refuses a merge without, so a page that stops satisfying these rules turns the
/// run red where every other property of this tree is decided. What else is required is
/// the set #54 made of the ruleset, and it is not touched here.
///
/// WHAT THIS DOES NOT DO, and none of the three is an oversight.
///
/// It does not produce a canonical layout. A formatter rewrites a file and this
/// refuses one, so two arrangements that both satisfy every rule below are both
/// accepted, and a page rearranged inside those bounds is a diff a reader still has
/// to read. What is refused is the list of shapes named in the rules, which is the
/// drift a hand edit actually produces.
///
/// It does not read the markup as a tree. Nothing here knows that a div is open, so
/// it cannot say that a line's indentation matches its depth; it says that
/// indentation moves one level at a time, in steps of four spaces, which is the
/// same property from below and needs no parser to be true.
///
/// It does not judge what a line ends with. The page is tracked text under
/// <c>* text=auto</c>, so git stores a line feed and the checkout decides what the
/// file on disk carries, and a rule about carriage returns would refuse the page on
/// a Windows clone and pass on a Linux one for a reason that has nothing to do with
/// the page. Every rule below reads the text with that difference already removed.
/// </remarks>
public class ConfigurationPageFormatTests
{
    /// <summary>
    /// One step of indentation. Four spaces, which is what the page already carries
    /// and what the rest of this tree indents with.
    /// </summary>
    private const int Level = 4;

    /// <summary>
    /// What a page has to satisfy, one name per rule, each with a fixture below that
    /// trips it and no other.
    /// </summary>
    private static readonly string[] _rules =
    [
        "spaces-not-tabs",
        "four-space-indent",
        "one-level-at-a-time",
        "no-trailing-space",
        "one-blank-line-at-most",
        "the-script-body-sits-under-its-tag",
        "one-newline-at-the-end"
    ];

    public static TheoryData<string> EveryPage =>
        new(Pages().Select(Path.GetFileName).ToArray()!);

    public static TheoryData<string, string> EveryRuleAndItsFixture =>
        new()
        {
            { "spaces-not-tabs", "a-tab-in-the-indentation" },
            { "four-space-indent", "an-indent-that-is-not-a-level" },
            { "one-level-at-a-time", "a-jump-of-two-levels" },
            { "no-trailing-space", "a-space-at-the-end-of-a-line" },
            { "one-blank-line-at-most", "two-blank-lines-in-a-row" },
            { "the-script-body-sits-under-its-tag", "a-script-body-beside-its-tag" },
            { "one-newline-at-the-end", "no-newline-at-the-end" }
        };

    [Fact]
    public void The_check_can_see_the_pages_it_judges()
    {
        // Guards every leg below. A check that found no pages would report a
        // repository whose configuration page is formatted, whatever the page
        // actually looked like, and it would do it in green.
        var pages = Pages();

        Assert.NotEmpty(pages);
        Assert.Contains("configPage.html", pages.Select(Path.GetFileName));
    }

    [Fact]
    public void Every_rule_has_a_fixture_and_every_fixture_has_a_rule()
    {
        // The pairing is data in two places, so a rule added without a fixture would
        // otherwise be a rule nothing proves, sitting in a class whose whole point is
        // that each one is proven.
        var paired = EveryRuleAndItsFixture.Select(row => (string)row[0]).ToArray();

        Assert.Equal(_rules.OrderBy(name => name, StringComparer.Ordinal), paired.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(EveryPage))]
    public void Every_page_this_plugin_ships_is_formatted(string fileName)
    {
        var text = File.ReadAllText(Path.Combine(ConfigurationDirectory(), fileName));

        foreach (var rule in _rules)
        {
            var broken = Violations(rule, text);

            Assert.True(
                broken.Count == 0,
                string.Create(CultureInfo.InvariantCulture, $"{fileName} breaks {rule}: {string.Join("; ", broken)}"));
        }
    }

    [Theory]
    [MemberData(nameof(EveryRuleAndItsFixture))]
    public void Each_rule_refuses_its_own_fixture_and_no_other(string rule, string fixtureName)
    {
        // A rule is proven by a case that trips it AND NO OTHER. A fixture breaking
        // two rules cannot tell a check that refuses the right thing from one that
        // refuses everything it is shown.
        var fixture = Fixture(fixtureName);

        Assert.NotEmpty(Violations(rule, fixture));

        foreach (var other in _rules.Where(name => !string.Equals(name, rule, StringComparison.Ordinal)))
        {
            Assert.True(
                Violations(other, fixture).Count == 0,
                string.Create(CultureInfo.InvariantCulture, $"{fixtureName} also trips {other}, so neither rule is proven by it"));
        }
    }

    [Fact]
    public void The_neighbour_that_breaks_no_rule_is_accepted()
    {
        // Without this a check that refused every page would pass every leg above.
        var clean = Fixture("clean");

        foreach (var rule in _rules)
        {
            var broken = Violations(rule, clean);

            Assert.True(
                broken.Count == 0,
                string.Create(CultureInfo.InvariantCulture, $"the clean neighbour trips {rule}: {string.Join("; ", broken)}"));
        }
    }

    [Fact]
    public void The_clean_neighbour_is_the_shape_the_rules_are_about()
    {
        // A neighbour with no script block and no nesting would be accepted by rules
        // that never looked at anything, so what it has to contain is asserted rather
        // than assumed.
        var clean = Fixture("clean");

        Assert.Contains("<script", clean, StringComparison.Ordinal);
        Assert.Contains("</script>", clean, StringComparison.Ordinal);
        Assert.Contains("\n                ", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void No_fixture_is_a_page_this_plugin_could_ship()
    {
        // A fixture that acquired a .html extension inside the plugin would be an
        // embedded page that is deliberately misformatted. The extension is the whole
        // of what keeps these apart, so it is checked rather than trusted.
        var fixtures = Directory.GetFiles(FixtureDirectory());

        Assert.NotEmpty(fixtures);
        Assert.DoesNotContain(fixtures, path => Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Pages(), page => page.StartsWith(FixtureDirectory(), StringComparison.Ordinal));
    }

    /// <summary>
    /// What one rule found in one page, one entry per line it objects to.
    /// </summary>
    /// <param name="rule">Which rule to apply.</param>
    /// <param name="text">The page, as a clone checked it out.</param>
    /// <returns>An empty list where the rule held.</returns>
    private static List<string> Violations(string rule, string text)
    {
        var normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var endsWithNewline = normalised.EndsWith('\n');
        var lines = normalised.Split('\n');

        if (endsWithNewline)
        {
            lines = lines[..^1];
        }

        return rule switch
        {
            "spaces-not-tabs" => Each(lines, line => line.Contains('\t', StringComparison.Ordinal), "is indented with a tab, so how deep it sits depends on the reader"),
            "four-space-indent" => Each(lines, line => !Blank(line) && Indent(line) % Level != 0, "is indented by something that is not a whole number of levels"),
            "one-level-at-a-time" => Stepping(lines),
            "no-trailing-space" => Each(lines, line => line.Length > 0 && char.IsWhiteSpace(line[^1]), "ends in whitespace, which a diff shows and a reader cannot see"),
            "one-blank-line-at-most" => Spacing(lines),
            "the-script-body-sits-under-its-tag" => Script(lines),
            "one-newline-at-the-end" => endsWithNewline ? [] : ["the last line has no line terminator"],
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "There is no such rule.")
        };
    }

    private static List<string> Each(string[] lines, Func<string, bool> objects, string why) =>
        lines
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => objects(entry.Line))
            .Select(entry => string.Create(CultureInfo.InvariantCulture, $"line {entry.Number} {why}"))
            .ToList();

    private static List<string> Stepping(string[] lines)
    {
        // Indentation that only ever grows one level at a time is the property a
        // reader uses to see what is inside what. Nothing here knows which element is
        // open, so this is that property read from below: a line four deeper than the
        // one before it has entered one thing, and a line eight deeper has entered
        // something nobody wrote an opening for.
        var found = new List<string>();
        var previous = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            if (Blank(lines[index]))
            {
                continue;
            }

            var indent = Indent(lines[index]);

            if (indent - previous > Level)
            {
                found.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {index + 1} is {indent - previous} spaces deeper than the line above it, which is more than one level"));
            }

            previous = indent;
        }

        return found;
    }

    private static List<string> Spacing(string[] lines)
    {
        var found = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!Blank(lines[index]))
            {
                continue;
            }

            if (index == 0)
            {
                found.Add("line 1 is blank, so the page starts with nothing");
            }
            else if (index == lines.Length - 1)
            {
                found.Add(string.Create(CultureInfo.InvariantCulture, $"line {index + 1} is a blank line at the end of the page"));
            }
            else if (Blank(lines[index - 1]))
            {
                found.Add(string.Create(CultureInfo.InvariantCulture, $"line {index + 1} is the second blank line in a row"));
            }
        }

        return found;
    }

    private static List<string> Script(string[] lines)
    {
        // The seam between the markup and the script is where a hand edit goes wrong,
        // because the two halves are indented by different habits and the script tag
        // is where one habit stops. A body that sits beside its tag rather than under
        // it reads as markup, and the closing tag is what says where it ended.
        var found = new List<string>();
        var opened = -1;

        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].TrimStart();

            if (opened < 0 && trimmed.StartsWith("<script", StringComparison.Ordinal))
            {
                opened = index;
                continue;
            }

            if (opened < 0)
            {
                continue;
            }

            if (trimmed.StartsWith("</script", StringComparison.Ordinal))
            {
                if (Indent(lines[index]) != Indent(lines[opened]))
                {
                    found.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"line {index + 1} closes a script block opened on line {opened + 1} at a different indentation"));
                }

                opened = -1;
                continue;
            }

            if (!Blank(lines[index]) && Indent(lines[index]) <= Indent(lines[opened]))
            {
                found.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {index + 1} is script sitting beside the tag on line {opened + 1} rather than under it"));
            }
        }

        if (opened >= 0)
        {
            found.Add(string.Create(CultureInfo.InvariantCulture, $"the script block opened on line {opened + 1} is never closed"));
        }

        return found;
    }

    private static bool Blank(string line) => line.AsSpan().Trim().IsEmpty;

    /// <summary>
    /// How deep a line sits, in spaces.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The width of its leading whitespace, counting a tab as one level.</returns>
    /// <remarks>
    /// A tab is counted rather than refused here, so that a line indented with tabs
    /// answers to the rule about tabs and to no other. Measuring it as nothing would
    /// make the same line trip the rules about levels and steps as well, and then
    /// none of the three would be proven by it.
    /// </remarks>
    private static int Indent(string line)
    {
        var width = 0;

        foreach (var character in line)
        {
            if (character == ' ')
            {
                width++;
            }
            else if (character == '\t')
            {
                width += Level - (width % Level);
            }
            else
            {
                break;
            }
        }

        return width;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".html.fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(ProjectDirectory(), "Fixtures", "page-format");

    private static List<string> Pages() =>
        Directory.GetFiles(ConfigurationDirectory(), "*.html", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Where the pages the plugin embeds are.
    /// </summary>
    /// <remarks>
    /// The tracked file rather than the embedded copy, because what this judges is
    /// the thing a contributor edits and a reviewer reads a diff of. The path is
    /// walked from the compiler's record of where this source sits, for the reason
    /// the determinism scan beside it gives: sources are not copied next to the
    /// assembly, and a path walked up from the assembly depends on the configuration
    /// and the framework it was built for.
    /// </remarks>
    private static string ConfigurationDirectory() =>
        Path.Combine(
            Path.GetDirectoryName(ProjectDirectory())!,
            "Jellyfin.Plugin.WhisperSubtitles",
            "Configuration");

    private static string ProjectDirectory() => Path.GetDirectoryName(ThisFile())!;

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
