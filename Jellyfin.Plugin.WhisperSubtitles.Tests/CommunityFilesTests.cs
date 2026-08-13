using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Hygiene;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The defect form asks a reporter to choose a server line and a backend from a
/// fixed list, and both lists are decided somewhere else in this tree. A form is
/// only worth having while its lists match those places.
/// </summary>
/// <remarks>
/// The failure this is written against is quiet in both directions. A list that
/// offers a server line this plugin is not built for, or a backend name it does
/// not answer to, asks a question with no right answer and gets a report about a
/// configuration nobody can reproduce. A list missing one it does have is worse
/// in a different way: the reporter picks the nearest option, and the answer is
/// wrong rather than absent, which nothing downstream can tell apart from a real
/// one.
///
/// Neither direction breaks a build. A server line added to Directory.Build.props
/// and a backend added under Backends both compile with this form untouched, and
/// nothing else in the tree reads it, so without this the form drifts silently
/// for as long as nobody opens it.
///
/// The pull request template is here for a different reason, and it is the one
/// leg about a check answering itself. `Pull request hygiene` decides whether a
/// body names an issue by looking for a hash followed by a digit. An example
/// reference written into the template as an illustration is such a string, so a
/// contributor who submitted the template unfilled would pass the rule on the
/// template's own text. That leg uses the rule's own function rather than a copy
/// of it, so it moves if the rule does.
///
/// WHAT THIS DOES NOT DO. It reads the form line by line and it is not a YAML
/// parser: what it can read is an options list written as one plain scalar per
/// line, quoted or not. A block scalar, a flow sequence or an anchor would defeat
/// it, and the form is written flat for that reason. It also says nothing about
/// whether the form renders on the tracker, which is a fact about the tracker
/// rather than about these bytes and is not measured anywhere in this suite.
/// </remarks>
public sealed class CommunityFilesTests
{
    private static readonly Regex _serverLine = new(
        @"<JellyfinServerLine>([^<]+)</JellyfinServerLine>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // A list entry, which is a dash, a space and a scalar that may be quoted. The
    // carriage return is optional and that is deliberate: these files are tracked
    // text under `* text=auto`, so git stores a line feed and the checkout decides
    // what the file on disk ends its lines with. Without it a Windows clone reads
    // every option with a stray carriage return on the end and every comparison
    // below fails for a reason that has nothing to do with the form.
    private static readonly Regex _option = new(
        @"^\s*-\s*""?(?<value>[^""\r\n]+?)""?\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_reader_finds_both_lists_rather_than_comparing_nothing()
    {
        // Guards every leg below. A reader that matched no options and no server
        // lines would report a form whose every entry resolves, whatever the form
        // said, and it would do it in green.
        var built = ServerLinesTheTreeBuilds();
        var lines = OptionsFor("server-line");
        var backends = OptionsFor("backend");

        Assert.True(
            built.Count > 1,
            $"Directory.Build.props gave {built.Count} server line(s), so the comparison below has nothing to compare against");

        Assert.True(lines.Count > 1, $"the server-line field in the defect form gave {lines.Count} option(s)");

        Assert.True(backends.Count > 1, $"the backend field in the defect form gave {backends.Count} option(s)");
    }

    [Fact]
    public void The_defect_form_offers_every_server_line_this_tree_builds_and_no_other()
    {
        var built = ServerLinesTheTreeBuilds();
        var offered = OptionsFor("server-line");

        foreach (var line in built)
        {
            Assert.True(
                offered.Contains(line),
                $"Directory.Build.props builds for server line {line} and the defect form does not offer it, so a reporter on that line has no right answer to give");
        }

        foreach (var line in offered)
        {
            Assert.True(
                built.Contains(line),
                $"the defect form offers server line {line} and Directory.Build.props builds for no such line, so the form invites a report about a configuration this plugin does not produce");
        }
    }

    [Fact]
    public void The_defect_form_offers_every_backend_this_plugin_answers_to_and_no_other()
    {
        var known = BackendNames.Known;
        var offered = OptionsFor("backend");

        foreach (var name in known)
        {
            Assert.True(
                offered.Contains(name),
                $"{name} is a backend this plugin answers to and the defect form does not offer it, so a report about it arrives naming something else");
        }

        foreach (var name in offered)
        {
            Assert.True(
                known.Contains(name),
                $"the defect form offers the backend {name} and BackendNames has no such name, so the form names a backend no configuration can select");
        }
    }

    [Fact]
    public void The_pull_request_template_does_not_answer_the_hygiene_rule_on_its_own()
    {
        // The rule's own function rather than a second copy of it, so a change to
        // what counts as naming an issue reaches this leg.
        Assert.False(
            HygieneRules.NamesAnIssue(PullRequestTemplate()),
            "the pull request template carries a hash followed by a digit, so `Pull request hygiene` reads the template itself as a body that names an issue and an unfilled body passes the rule");
    }

    [Fact]
    public void The_reader_gives_the_same_answer_whatever_a_clone_did_to_the_line_endings()
    {
        // One clone checks these files out with carriage returns and another does
        // not, and neither is wrong: `.gitattributes` stores a line feed and lets
        // the checkout decide. What has to be true is that the answer does not
        // move between the two.
        var asLineFeeds = DefectForm().Replace("\r\n", "\n", StringComparison.Ordinal);
        var asCarriageReturns = asLineFeeds.Replace("\n", "\r\n", StringComparison.Ordinal);

        var fromLineFeeds = Options(asLineFeeds, "backend");
        var fromCarriageReturns = Options(asCarriageReturns, "backend");

        Assert.NotEmpty(fromLineFeeds);
        Assert.Equal(fromLineFeeds, fromCarriageReturns);
    }

    private static HashSet<string> ServerLinesTheTreeBuilds() =>
        _serverLine
            .Matches(Read("Directory.Build.props"))
            .Select(match => match.Groups[1].Value.Trim())
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> OptionsFor(string fieldId) =>
        Options(DefectForm(), fieldId).ToHashSet(StringComparer.Ordinal);

    // The options of one field, read from the line naming its id. Anchoring on the
    // id rather than on the label means a reworded label does not silently take
    // this to a different field's list, which would compare two real lists and
    // report a mismatch nobody could find.
    private static List<string> Options(string form, string fieldId)
    {
        var lines = form.Split('\n');
        var found = new List<string>();

        var start = Array.FindIndex(lines, line => line.Trim().Equals($"id: {fieldId}", StringComparison.Ordinal));
        Assert.True(start >= 0, $"the defect form has no field with id {fieldId}");

        var list = -1;
        for (var index = start + 1; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();

            Assert.False(
                trimmed.StartsWith("id:", StringComparison.Ordinal),
                $"the field {fieldId} in the defect form is followed by the next field before any options list, so it offers nothing to choose from");

            if (trimmed.Equals("options:", StringComparison.Ordinal))
            {
                list = index;
                break;
            }
        }

        Assert.True(list >= 0, $"the field {fieldId} in the defect form has no options list");

        for (var index = list + 1; index < lines.Length; index++)
        {
            var match = _option.Match(lines[index]);
            if (!match.Success)
            {
                break;
            }

            found.Add(match.Groups["value"].Value);
        }

        return found;
    }

    private static string DefectForm() =>
        Read(Path.Combine(".github", "ISSUE_TEMPLATE", "defect.yml"));

    private static string PullRequestTemplate() =>
        Read(Path.Combine(".github", "PULL_REQUEST_TEMPLATE.md"));

    // The files a clone checked out, rather than a copy carried next to the test
    // assembly. What these legs are about is the bytes a contributor and the
    // tracker are given, and the compiler is what knows where those are.
    private static string Read(string relativePath)
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;
        var path = Path.Combine(root, relativePath);

        Assert.True(File.Exists(path), $"{relativePath} was not found, looked in {path}");

        return File.ReadAllText(path);
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
