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
/// The manifest describes one of the two server lines this tree builds, and the reason
/// it describes only one is held against the tree rather than left as a sentence.
/// </summary>
/// <remarks>
/// The failure this is written against had landed and stood. build.yaml said a second
/// manifest for the other line was part of packaging and named the issue that owns it,
/// and stopped there. Read as it stands that is ordinary work waiting for somebody in
/// this repository, and it is not. Such a manifest would promise servers from 12.0.0.0
/// upward while the line it describes compiles against a release candidate, so the
/// promise would name a released server no build here has ever seen, and
/// .github/scripts/read-abi-floor.sh already refuses exactly that. The manifest is
/// refused rather than merely unwritten, and what it waits for is a release nobody in
/// this repository can produce.
///
/// A paragraph saying so is worth no more than the sentence it replaced unless
/// something notices the day it stops being true. The condition that ends the wait is
/// the pin in Directory.Build.props ceasing to be a candidate, which is a fact of this
/// tree, so it is read here rather than trusted. Two numbers in that paragraph are
/// derived from the pin and neither is written down in this class: the promise such a
/// manifest would carry, and the package the undescribed line compiles against.
///
/// Each refusal carries a fixture under Fixtures/second-manifest/, because moving the
/// real pin to prove the last leg is not available: no released 12.0 package exists, so
/// that edit fails the restore before any test runs, and a proof nobody can execute is
/// the kind this repository writes down instead of running. clean.props.fixture and
/// clean.yaml.fixture are the neighbour that has to stay accepted, or a reader
/// complaining about everything would pass every leg.
///
/// WHAT THIS DOES NOT DO. It reads the comment block at the top of the manifest, which
/// is where the explanation lives, and says nothing about a sentence written further
/// down or in another file. It compares strings and has no opinion on whether the
/// paragraph around them argues correctly, which is what the review is for. It reaches
/// no network and no registry, so whether a released server exists for a line is read
/// from the pin's own shape and never from a feed. And it is not a YAML parser: it
/// reads the fields it needs line by line, which is how both files are written and how
/// the floor script reads them too.
/// </remarks>
public sealed class SecondServerLineManifestTests
{
    private static readonly Regex _supportedLines = new(
        @"<SupportedServerLines>([^<]+)</SupportedServerLines>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _frameworkCondition = new(
        @"'\$\(TargetFramework\)'\s*==\s*'([^']+)'",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _serverLine = new(
        @"<JellyfinServerLine>([^<]+)</JellyfinServerLine>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _packageVersion = new(
        @"<JellyfinPackageVersion>([^<]+)</JellyfinPackageVersion>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex _manifestField = new(
        "^([a-zA-Z]+):[ \t]*\"?([^\"\\s]+)\"?[ \t]*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void The_tree_says_which_line_it_describes_and_why_it_describes_only_one()
    {
        Assert.Empty(Complaints(Props(), Manifest()));
    }

    [Fact]
    public void The_tree_has_a_second_line_for_the_paragraph_to_be_about()
    {
        // Without this the leg above passes over a tree where every line is
        // described, where the paragraph is about nothing, and where the day the
        // second manifest landed would look exactly like today.
        var built = Lines(Props());

        Assert.True(built.Count > 1, "Directory.Build.props declares one server line, so nothing here is undescribed");
        Assert.Single(Described(built, Manifest()));
    }

    [Fact]
    public void The_reader_refuses_a_line_whose_server_has_been_released()
    {
        var complaints = Complaints(Fixture("a-line-with-a-released-server.props"), Fixture("clean.yaml"));

        Assert.Contains(complaints, complaint => complaint.Contains("is a release rather than a candidate", StringComparison.Ordinal));
        Assert.Single(complaints);
    }

    [Fact]
    public void The_reader_refuses_a_paragraph_that_never_says_what_the_missing_manifest_would_promise()
    {
        var complaints = Complaints(Fixture("clean.props"), Fixture("a-paragraph-that-omits-the-promise.yaml"));

        Assert.Contains(complaints, complaint => complaint.Contains("never names the promise", StringComparison.Ordinal));
        Assert.Single(complaints);
    }

    [Fact]
    public void The_reader_refuses_a_paragraph_that_never_names_the_package_the_line_compiles_against()
    {
        var complaints = Complaints(Fixture("clean.props"), Fixture("a-paragraph-that-omits-the-package.yaml"));

        Assert.Contains(complaints, complaint => complaint.Contains("never names the package", StringComparison.Ordinal));
        Assert.Single(complaints);
    }

    [Fact]
    public void The_reader_refuses_a_manifest_that_leaves_no_line_undescribed()
    {
        var complaints = Complaints(Fixture("a-props-with-one-line.props"), Fixture("clean.yaml"));

        Assert.Contains(complaints, complaint => complaint.Contains("leaves no line undescribed", StringComparison.Ordinal));
        Assert.Single(complaints);
    }

    [Fact]
    public void The_reader_accepts_the_neighbour_that_breaks_nothing()
    {
        // The paragraph reworded around both numbers rather than through them. A
        // reader that refused this would refuse every legitimate edit to that
        // comment and would pass every leg above it for the wrong reason.
        Assert.Empty(Complaints(Fixture("clean.props"), Fixture("clean.yaml")));

        Assert.Empty(Complaints(Fixture("clean.props"), Fixture("a-paragraph-reworded-around-both-numbers.yaml")));
    }

    /// <summary>
    /// Everything wrong with one pair of a project file and a manifest, in the order the
    /// arms are written.
    /// </summary>
    /// <param name="props">The text declaring the supported server lines.</param>
    /// <param name="manifest">The text of the manifest that describes one of them.</param>
    /// <returns>One sentence per complaint, and nothing where the pair is sound.</returns>
    private static List<string> Complaints(string props, string manifest)
    {
        var found = new List<string>();
        var built = Lines(props);
        var described = Described(built, manifest);
        var undescribed = built.Except(described).ToList();
        var explanation = Explanation(manifest);

        if (described.Count != 1)
        {
            found.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"the manifest names framework {Field(manifest, "framework")} and promises servers from {Field(manifest, "targetAbi")} upward, which matches {described.Count} of the {built.Count} line(s) this tree builds, so what it describes is no longer one line out of the set"));
        }

        if (undescribed.Count == 0)
        {
            found.Add("the manifest leaves no line undescribed, so the paragraph at the top of it about a manifest that is still owed is about nothing");
        }

        foreach (var line in undescribed)
        {
            var promise = string.Create(CultureInfo.InvariantCulture, $"{line.ServerLine}.0.0");

            if (!explanation.Contains(promise, StringComparison.Ordinal))
            {
                found.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"the manifest does not describe {line.Framework} and the paragraph explaining that never names the promise {promise} a manifest for that line would have to carry, so a reader is told one is owed and never what the refused thing would have said"));
            }

            if (!explanation.Contains(line.PackageVersion, StringComparison.Ordinal))
            {
                found.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"the manifest does not describe {line.Framework} and the paragraph explaining that never names the package {line.PackageVersion} that line compiles against, which is the whole reason such a manifest is refused rather than merely unwritten, so the paragraph goes stale the day the pin moves with nothing to notice"));
            }

            if (!line.PackageVersion.Contains('-', StringComparison.Ordinal))
            {
                found.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{line.Framework} has no manifest and is pinned at {line.PackageVersion}, which is a release rather than a candidate, so the reason written for leaving that line undescribed has expired and a manifest promising {promise} is a promise this build can follow. That is #51"));
            }
        }

        return found;
    }

    /// <summary>
    /// Every supported server line, with the two facts the project file holds about it.
    /// </summary>
    /// <param name="props">The text declaring the supported server lines.</param>
    /// <returns>One entry per framework named in SupportedServerLines, in the order given.</returns>
    private static List<ServerLineFacts> Lines(string props)
    {
        var supported = _supportedLines.Match(props);

        Assert.True(
            supported.Success,
            "there is no SupportedServerLines declaration to read, so this check does not know which lines the tree builds and would pass for the wrong reason");

        var found = new List<ServerLineFacts>();

        foreach (var entry in supported.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var framework = entry.Trim();
            var block = BlockFor(props, framework);
            var serverLine = _serverLine.Match(block);
            var packageVersion = _packageVersion.Match(block);

            Assert.True(
                serverLine.Success && packageVersion.Success,
                $"{framework} is a supported line with no server line or no package version beside it, so there is nothing to compare the manifest against");

            found.Add(new ServerLineFacts(framework, serverLine.Groups[1].Value, packageVersion.Groups[1].Value));
        }

        return found;
    }

    /// <summary>
    /// The property group for one framework, from its condition to the end of that group.
    /// </summary>
    /// <param name="props">The whole of the project file.</param>
    /// <param name="framework">The target framework the group is conditioned on.</param>
    /// <returns>The text of that group, or nothing where there is no such group.</returns>
    private static string BlockFor(string props, string framework)
    {
        foreach (Match condition in _frameworkCondition.Matches(props))
        {
            if (!string.Equals(condition.Groups[1].Value, framework, StringComparison.Ordinal))
            {
                continue;
            }

            var end = props.IndexOf("</PropertyGroup>", condition.Index, StringComparison.Ordinal);

            return end < 0 ? string.Empty : props[condition.Index..end];
        }

        return string.Empty;
    }

    /// <summary>
    /// The supported lines a manifest describes, which is the framework it names at a
    /// promise inside that line.
    /// </summary>
    /// <param name="built">Every line the tree builds.</param>
    /// <param name="manifest">The text of the manifest.</param>
    /// <returns>Those the manifest describes.</returns>
    private static List<ServerLineFacts> Described(List<ServerLineFacts> built, string manifest) =>
        built
            .Where(line => string.Equals(line.Framework, Field(manifest, "framework"), StringComparison.Ordinal)
                && Field(manifest, "targetAbi").StartsWith(line.ServerLine + ".", StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// One scalar field of a manifest, read line by line the way the floor script reads it.
    /// </summary>
    /// <param name="manifest">The text of the manifest.</param>
    /// <param name="name">The field to read.</param>
    /// <returns>The field's value.</returns>
    private static string Field(string manifest, string name)
    {
        foreach (var line in Rows(manifest))
        {
            var match = _manifestField.Match(line);

            if (match.Success && string.Equals(match.Groups[1].Value, name, StringComparison.Ordinal))
            {
                return match.Groups[2].Value;
            }
        }

        Assert.Fail($"the manifest carries no {name} field, so this check has nothing to compare and would otherwise pass for the wrong reason");

        return string.Empty;
    }

    /// <summary>
    /// The comment block at the top of a manifest, which is where it explains its own shape.
    /// </summary>
    /// <param name="manifest">The text of the manifest.</param>
    /// <returns>Those lines, joined, with the comment markers left on.</returns>
    private static string Explanation(string manifest)
    {
        var explanation = Rows(manifest)
            .TakeWhile(line => line.StartsWith('#')
                || line.StartsWith("---", StringComparison.Ordinal)
                || line.Trim().Length == 0)
            .ToList();

        Assert.Contains(explanation, line => line.StartsWith('#'));

        return string.Join('\n', explanation);
    }

    private static string[] Rows(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string Props() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "Directory.Build.props"));

    private static string Manifest() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "build.yaml"));

    /// <summary>
    /// One fixture, read from beside the test assembly.
    /// </summary>
    /// <param name="name">The fixture's name without the trailing extension.</param>
    /// <returns>Its text.</returns>
    private static string Fixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "second-manifest", name + ".fixture");

        Assert.True(File.Exists(path), $"there is no fixture at {path}, so the leg reading it proves nothing");

        return File.ReadAllText(path);
    }

    private static string RepositoryRoot() =>
        Path.GetDirectoryName(Path.GetDirectoryName(ThisFile())!)!;

    private static string ThisFile([CallerFilePath] string path = "") => path;

    /// <summary>
    /// A supported server line, as the project file declares it.
    /// </summary>
    /// <param name="Framework">The target framework the line is built for.</param>
    /// <param name="ServerLine">The Jellyfin server line that framework carries.</param>
    /// <param name="PackageVersion">The Jellyfin package version that line compiles against.</param>
    private sealed record ServerLineFacts(string Framework, string ServerLine, string PackageVersion);
}
