using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Two sections of <c>docs/limits.md</c> are about where this plugin puts things.
/// One lists what it writes and where, and the other says what removing the plugin
/// leaves behind. This reads both against the code that does the writing.
/// </summary>
/// <remarks>
/// The accident it is written against is quiet in both directions and neither has
/// anything else refusing it.
///
/// A write reaching a location nobody added to the list leaves a page that reads as
/// complete while an operator finds the file months later, which is the sentence the
/// issue behind this page opens with. A kind on the list that the uninstall section
/// says nothing about leaves the same operator with no answer about what removing the
/// plugin does to it, and the uninstall section written from memory is the shape that
/// produces.
///
/// So the comparison runs four ways. Every plugin source that writes is a writer of
/// a kind the list names. Every file this map names as a writer still writes, so a
/// rename empties a permission loudly rather than quietly. Every kind on the list is
/// accounted for on the way out. And the way out does not deny a removal this plugin
/// makes.
///
/// The fourth arrived after the uninstall section said this plugin has no removal
/// path at all while four of its sources took a file off a disk. That sentence was
/// wrong on the day it was written, and the map below already named one of those
/// sources as a permitted remover, so the page and this class contradicted each
/// other in the same commit and every route stayed green. What makes the denial
/// decidable here rather than a reading is that the population it asserts is empty
/// is the one this class already computes.
///
/// The vocabulary is assembled from fragments, which is the neighbouring scanners'
/// reasoning: a file holding the literals it looks for cannot be read by the next
/// scanner somebody writes over this directory.
///
/// WHAT THIS DOES NOT DO.
///
/// It does not watch a run. Whether a run writes only where these call sites point is
/// the middle condition of the issue behind this page, it needs a run to observe, and
/// nothing here observes one: what is compared is the sources against the page. A path
/// handed to a writer is invisible to this, so a call site that stays where it is and
/// starts being given somewhere else passes.
///
/// It reads tokens, so a write reached through reflection or through a helper under
/// another name walks past it, and it reads only lines that are not comments, so a
/// call inside a block comment opened part way along a line of code is counted as
/// code. Both fail in the safe direction except the first, and the first is the same
/// edge the boundary scanner beside it states about itself.
///
/// It does not judge whether a sentence on the page is TRUE of the location it
/// describes. That is a reading, and the review is where a wrong one is caught. What
/// is refused is a location, or a kind, that nothing on the page answers for.
///
/// The denial leg is the one exception and it is a narrow one. It reads a phrase and
/// not a meaning: the vocabulary holds the sentence that was actually written rather
/// than every way of writing it, so the same denial in other words passes. What it
/// buys is that this one cannot come back.
/// </remarks>
public class WriteLocationsTests
{
    /// <summary>
    /// The section listing the kinds of thing this plugin puts on a disk, as the
    /// reader beside this one splits a heading from its title.
    /// </summary>
    internal const string ListTitle = "What it writes, and where";

    /// <summary>
    /// The section listing the kinds of thing this plugin puts on a disk.
    /// </summary>
    private const string ListHeading = "## " + ListTitle;

    /// <summary>
    /// The section saying what survives the plugin being removed, as the reader
    /// beside this one splits a heading from its title.
    /// </summary>
    internal const string UninstallTitle = "What removing the plugin does not delete";

    /// <summary>
    /// The section saying what survives the plugin being removed.
    /// </summary>
    private const string UninstallHeading = "## " + UninstallTitle;

    private const string Dot = ".";

    /// <summary>
    /// A sentence in the uninstall section saying this plugin takes nothing off a
    /// disk at all.
    /// </summary>
    /// <remarks>
    /// The words the page actually carried, rather than a guess at the ways somebody
    /// could write the same denial. The bound that leaves is stated at the class.
    /// </remarks>
    private const string DenialOfEveryRemoval = "no removal path";

    /// <summary>
    /// Creating, moving, replacing or removing something on a disk. Opening a file
    /// for reading is deliberately not here: the remote backend opens the extracted
    /// audio to send it, and a rule coarse enough to refuse that would be refusing a
    /// read for looking like a write.
    /// </summary>
    private static readonly string[] _writes =
    [
        "File" + Dot + "WriteAll",
        "File" + Dot + "AppendAll",
        "File" + Dot + "Create",
        "File" + Dot + "Move",
        "File" + Dot + "Delete",
        "File" + Dot + "Copy",
        "File" + Dot + "Replace",
        "File" + Dot + "OpenWrite",
        "File" + Dot + "Open" + "(",
        "Directory" + Dot + "CreateDirectory",
        "Directory" + Dot + "Delete",
        "Directory" + Dot + "Move",
        "new File" + "Stream",
        "new Stream" + "Writer"
    ];

    /// <summary>
    /// The tokens above that take something off a disk rather than putting it there.
    /// </summary>
    /// <remarks>
    /// A subset rather than a second vocabulary, and a leg below holds that it is one,
    /// so the shape proof the writes carry covers these as well instead of a copy of
    /// it going stale on its own.
    /// </remarks>
    private static readonly string[] _removals =
    [
        "File" + Dot + "Delete",
        "Directory" + Dot + "Delete"
    ];

    /// <summary>
    /// The three kinds the page names, each tied to the sources that write it.
    /// </summary>
    /// <remarks>
    /// The writers are named file by file rather than matched by folder, so widening
    /// the permission is something somebody does on purpose and a reviewer sees.
    ///
    /// The second kind lost <c>TemporaryAudioSweep.cs</c> and gained
    /// <c>SystemFileRemoval.cs</c> under #71. The sweep held the removal that reaches
    /// the disk as a private class of its own, so the file that decided WHICH files
    /// go was also the file that removed them; the removal now lives behind the seam
    /// the composition root registers, and the sweep names no write. The leg below
    /// that asks whether every writer still writes is what caught the move.
    ///
    /// The third kind has no writer here and that is the fact rather than an omission:
    /// the server writes the plugin's configuration and would write its records, and
    /// nothing in this tree reaches that location. The day something here does, the
    /// file it is in is refused by the leg below until it is added, which is the
    /// moment the page has to gain a sentence too.
    /// </remarks>
    private static readonly Kind[] _kinds =
    [
        new Kind(
            "the subtitle file",
            "The subtitle file",
            "subtitle files stay on disk",
            ["AtomicSubtitleFile.cs", "SubtitleOutput.cs"]),
        new Kind(
            "temporary audio",
            "Temporary audio",
            "Temporary audio is already gone",
            ["AudioExtractor.cs", "ExtractedAudio.cs", "SystemFileRemoval.cs"]),
        new Kind(
            "plugin data",
            "where the server puts plugin data",
            "the server removes plugin data",
            []),
    ];

    /// <summary>
    /// How the list section names each kind.
    /// </summary>
    /// <remarks>
    /// Handed out so that <see cref="LimitsPageTests"/> can ask its state question of
    /// each kind's own paragraph rather than of the section around them. The phrases
    /// are the ones this class already resolves against the page, so the neighbour
    /// reads the vocabulary rather than keeping a second copy of it that drifts.
    /// </remarks>
    internal static IReadOnlyList<string> KindsAsTheListNamesThem =>
        _kinds.Select(kind => kind.OnTheList).ToArray();

    /// <summary>
    /// How the uninstall section names each kind.
    /// </summary>
    /// <remarks>
    /// The same reason as its neighbour above, one section further down. That
    /// section carried a single marker at its end covering three kinds in one
    /// sentence, so two of them stood in the present tense saying things this tree
    /// does not do: that removal takes away a record nothing writes, and that
    /// temporary audio never survives a run when what a dead process orphaned is
    /// collected by nothing. Both were the accident the list section had twice
    /// already, and this hands the vocabulary over so the same leg can ask the same
    /// question here.
    /// </remarks>
    internal static IReadOnlyList<string> KindsAsTheWayOutNamesThem =>
        _kinds.Select(kind => kind.OnTheWayOut).ToArray();

    public static TheoryData<string> EveryPluginSourceFile =>
        new(PluginSourceFiles().Select(Path.GetFileName).ToArray()!);

    public static TheoryData<string, string> EveryWriterAndItsKind
    {
        get
        {
            var rows = new TheoryData<string, string>();

            foreach (var kind in _kinds)
            {
                foreach (var writer in kind.Writers)
                {
                    rows.Add(writer, kind.Name);
                }
            }

            return rows;
        }
    }

    [Fact]
    public void The_scanner_can_see_the_plugin_sources_it_judges()
    {
        // Guards every leg reading a source. A scan that found no files would report
        // a plugin writing nowhere at all, in green, whatever the plugin did.
        var files = PluginSourceFiles();

        Assert.True(files.Count > 40, $"only {files.Count} plugin source files were found beside {ThisFile()}");
        Assert.Contains("AtomicSubtitleFile.cs", files.Select(Path.GetFileName));
        Assert.Contains("AudioExtractor.cs", files.Select(Path.GetFileName));
    }

    [Fact]
    public void The_scanner_would_see_a_shape_it_was_shown()
    {
        // The vocabulary is assembled from fragments, so a typo in the assembly would
        // leave a token matching nothing and passing for as long as nobody looked.
        foreach (var token in _writes)
        {
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Contains(token, "        " + token + "something;", StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_reader_can_see_both_sections_it_judges()
    {
        // The same guard for the page. A reader that found neither section would
        // report every kind as accounted for by finding nothing to account for.
        var page = Page();

        Assert.NotEmpty(Section(page, ListHeading).Trim());
        Assert.NotEmpty(Section(page, UninstallHeading).Trim());
        Assert.True(Section(page, ListHeading).Length < page.Length, "the list section ran to the end of the page");
        Assert.True(Section(page, UninstallHeading).Length < page.Length, "the uninstall section ran to the end of the page");
    }

    [Theory]
    [MemberData(nameof(EveryPluginSourceFile))]
    public void Everything_that_writes_is_a_writer_of_a_kind_the_page_lists(string fileName)
    {
        if (_kinds.SelectMany(kind => kind.Writers).Contains(fileName, StringComparer.Ordinal))
        {
            return;
        }

        var source = WithoutComments(Read(fileName));

        foreach (var token in _writes)
        {
            Assert.False(
                source.Contains(token, StringComparison.Ordinal),
                $"{fileName} puts something on a disk and no kind on {ListHeading} answers for it: it carries {token}");
        }
    }

    [Theory]
    [MemberData(nameof(EveryWriterAndItsKind))]
    public void Every_writer_this_page_is_read_against_still_writes(string fileName, string kind)
    {
        // A permission that has stopped being about anything. A writer renamed or
        // emptied leaves this map allowing a file, and the next write to land in a
        // file of that name inherits a permission nobody granted it.
        Assert.Contains(fileName, PluginSourceFiles().Select(Path.GetFileName));

        var source = WithoutComments(Read(fileName));

        Assert.True(
            _writes.Any(token => source.Contains(token, StringComparison.Ordinal)),
            $"{fileName} is named as a writer of {kind} and writes nothing, so the permission stands for nothing");
    }

    [Fact]
    public void Every_removal_token_is_one_the_write_vocabulary_already_holds()
    {
        // So the shape proof above reaches these too. A removal token spelled
        // differently from its write would match nothing and pass for as long as
        // nobody looked, which is the accident that proof exists against.
        Assert.All(_removals, token => Assert.Contains(token, _writes));
    }

    [Fact]
    public void The_scanner_can_see_that_this_plugin_removes_something()
    {
        // Guards the leg below rather than duplicating it. The denial it refuses is
        // TRUE of a plugin that removes nothing, so a scan finding none would let the
        // page say it and pass; the day this plugin stops removing anything is a
        // change somebody makes on purpose and should meet here rather than discover
        // later on the page.
        Assert.NotEmpty(SourcesThatRemove());
    }

    [Fact]
    public void The_way_out_does_not_deny_a_removal_this_plugin_makes()
    {
        Assert.False(
            DeniesEveryRemoval(Page()),
            $"{UninstallHeading} says this plugin has no removal path, and each of {string.Join(", ", SourcesThatRemove())} takes a file off a disk");
    }

    [Fact]
    public void The_reader_refuses_a_way_out_that_denies_every_removal()
    {
        // The fixture differs from the accepted neighbour in one sentence, and it is
        // the sentence the page carried. Both other legs stay green on it, which is
        // what says this one is answering for the denial and not for a kind going
        // missing at the same time.
        var fixture = Fixture("denies-every-removal", "md");

        Assert.True(DeniesEveryRemoval(fixture));
        Assert.False(DeniesEveryRemoval(Fixture("clean", "md")));
        Assert.Empty(MissingFromList(fixture));
        Assert.Empty(MissingFromTheWayOut(fixture));
    }

    [Fact]
    public void The_list_names_every_kind_this_plugin_writes()
    {
        Assert.Empty(MissingFromList(Page()));
    }

    [Fact]
    public void The_uninstall_section_accounts_for_every_kind_on_the_list()
    {
        Assert.Empty(MissingFromTheWayOut(Page()));
    }

    [Fact]
    public void The_reader_refuses_a_page_whose_uninstall_section_drops_a_kind()
    {
        // The failure this issue is written against, from the side an operator meets
        // it: a kind the plugin writes, listed, and then unanswered on the way out.
        var fixture = Fixture("a-kind-the-uninstall-section-does-not-account-for", "md");

        Assert.Equal(["temporary audio"], MissingFromTheWayOut(fixture));
        Assert.Empty(MissingFromList(fixture));
    }

    [Fact]
    public void The_reader_refuses_a_page_whose_list_drops_a_kind()
    {
        // The other direction, and the one a change to the code produces rather than
        // a change to the page: something written, and no sentence on the list about
        // where it went.
        var fixture = Fixture("a-kind-the-list-does-not-name", "md");

        Assert.Equal(["plugin data"], MissingFromList(fixture));
        Assert.Empty(MissingFromTheWayOut(fixture));
    }

    [Fact]
    public void The_page_that_accounts_for_every_kind_is_accepted()
    {
        // Without this a reader that found nothing anywhere would pass both legs
        // above by refusing every kind in both sections.
        var fixture = Fixture("clean", "md");

        Assert.Empty(MissingFromList(fixture));
        Assert.Empty(MissingFromTheWayOut(fixture));
    }

    [Fact]
    public void The_scanner_refuses_a_source_that_writes_somewhere_new()
    {
        var fixture = WithoutComments(Fixture("writes-a-record-of-its-own", "cs"));

        Assert.True(_writes.Any(token => fixture.Contains(token, StringComparison.Ordinal)), "the fixture trips no token");
    }

    [Fact]
    public void The_neighbour_that_only_reads_is_accepted()
    {
        // The near miss rather than a distant one. This fixture differs from the one
        // above in doing nothing but read, and the plugin has such a source: the
        // remote backend opens the extracted audio to send it. A vocabulary as coarse
        // as the word file would pass its own fixture and fail here.
        var fixture = WithoutComments(Fixture("reads-without-writing", "cs"));

        Assert.False(_writes.Any(token => fixture.Contains(token, StringComparison.Ordinal)), "reading a file trips a write token");
        Assert.Contains("OpenRead", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scanner_reads_the_code_and_not_the_prose_beside_it()
    {
        // A document comment naming a call is not a call, and the plugin carries one:
        // the file-removal seam explains itself by naming what the framework method
        // does. Read without this the plugin would have a writer nobody can point at
        // a location for, and the repair would be to weaken the page instead.
        var fixture = Fixture("names-a-write-in-a-comment", "cs");

        Assert.True(_writes.Any(token => fixture.Contains(token, StringComparison.Ordinal)), "the fixture names no write at all");
        Assert.False(
            _writes.Any(token => WithoutComments(fixture).Contains(token, StringComparison.Ordinal)),
            "the comment survived being taken out");
    }

    [Fact]
    public void No_fixture_is_a_document_or_a_source_anything_else_reads()
    {
        // The extension is the whole of what keeps these away from a check that walks
        // the tree for markdown or compiles the sources beside it. A fixture that
        // acquired a plain one would be a second page about this repository saying
        // things that are deliberately untrue, and a source claiming a write this
        // plugin does not make.
        var fixtures = Directory.GetFiles(FixtureDirectory())
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(fixtures);
        Assert.All(
            fixtures,
            path => Assert.True(
                path.EndsWith(".md.fixture", StringComparison.Ordinal)
                || path.EndsWith(".cs.fixture", StringComparison.Ordinal),
                $"{Path.GetFileName(path)} is a fixture under an extension something else reads"));
    }

    private static bool DeniesEveryRemoval(string page) =>
        Flattened(Section(page, UninstallHeading))
            .Contains(DenialOfEveryRemoval, StringComparison.Ordinal);

    /// <summary>
    /// The plugin sources that take something off a disk.
    /// </summary>
    private static List<string> SourcesThatRemove() =>
        PluginSourceFiles()
            .Where(path => _removals.Any(token =>
                WithoutComments(File.ReadAllText(path)).Contains(token, StringComparison.Ordinal)))
            .Select(path => Path.GetFileName(path)!)
            .ToList();

    private static List<string> MissingFromList(string page) =>
        Absent(Section(page, ListHeading), kind => kind.OnTheList);

    private static List<string> MissingFromTheWayOut(string page) =>
        Absent(Section(page, UninstallHeading), kind => kind.OnTheWayOut);

    private static List<string> Absent(string section, Func<Kind, string> phrase)
    {
        var flat = Flattened(section);

        return _kinds
            .Where(kind => !flat.Contains(phrase(kind), StringComparison.Ordinal))
            .Select(kind => kind.Name)
            .ToList();
    }

    /// <summary>
    /// A section as one line, so a phrase is looked for in the prose rather than in
    /// the shape the prose was wrapped into.
    /// </summary>
    /// <remarks>
    /// Without this the check answers to where somebody's editor broke the line. The
    /// page names the third location across a wrap today, so a reader comparing the
    /// text as written would report it missing and the repair would be to reflow a
    /// paragraph.
    /// </remarks>
    private static string Flattened(string section) =>
        string.Join(' ', section.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The body of one section, up to the next heading of the same level.
    /// </summary>
    private static string Section(string page, string heading)
    {
        var start = page.IndexOf(heading, StringComparison.Ordinal);

        Assert.True(start >= 0, $"no section headed {heading} was found");

        var body = page[(start + heading.Length)..];
        var next = body.IndexOf("\n## ", StringComparison.Ordinal);

        return next < 0 ? body : body[..next];
    }

    /// <summary>
    /// The source with its comment lines removed.
    /// </summary>
    /// <remarks>
    /// Whole lines rather than a parse, because a parse of C# to decide this would be
    /// a larger thing than what it guards. What that costs is stated at the class: a
    /// block comment opened part way along a line of code leaves its contents counted
    /// as code, which refuses something that is not a write rather than admitting one
    /// that is.
    /// </remarks>
    private static string WithoutComments(string source) =>
        string.Join(
            '\n',
            source
                .Split('\n')
                .Select(line => line.TrimEnd('\r').TrimStart())
                .Where(line =>
                    !line.StartsWith("//", StringComparison.Ordinal)
                    && !line.StartsWith("/*", StringComparison.Ordinal)
                    && !line.StartsWith('*')));

    private static string Fixture(string name, string kind) =>
        File.ReadAllText(Path.Combine(FixtureDirectory(), name + "." + kind + ".fixture"));

    private static string FixtureDirectory() =>
        Path.Combine(Path.GetDirectoryName(ThisFile())!, "Fixtures", "write-locations");

    private static string Read(string fileName) =>
        File.ReadAllText(PluginSourceFiles().Single(path => Path.GetFileName(path) == fileName));

    /// <summary>
    /// The plugin's own sources, out of the checkout rather than out of a copy.
    /// </summary>
    /// <remarks>
    /// From the compiler's record of this file's path, for the reason the neighbouring
    /// scanners give: sources are not copied beside the assembly, and a path walked
    /// upwards from one depends on the configuration and the framework it was built
    /// for. The build directories are left out because what is in them is generated.
    /// </remarks>
    private static List<string> PluginSourceFiles()
    {
        var root = Path.Combine(
            Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!,
            "Jellyfin.Plugin.WhisperSubtitles");

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string Page() =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!,
            "docs",
            "limits.md"));

    private static string ThisFile([CallerFilePath] string path = "") => path;

    /// <summary>
    /// One kind of thing the plugin puts on a disk: what the list calls it, what the
    /// uninstall section says becomes of it, and the sources that write it.
    /// </summary>
    private sealed record Kind(string Name, string OnTheList, string OnTheWayOut, string[] Writers);
}
