using System;
using System.IO;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Xunit;
using PluginUnderTest = Jellyfin.Plugin.WhisperSubtitles.Plugin;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The configuration page as a server would serve it, for the document checks
/// that make claims about whether it is there.
/// </summary>
/// <remarks>
/// The embedded resource rather than the file on disk, because what an operator
/// opens is the copy inside the assembly. A page dropped from the build is a
/// directory that still holds the markup, and a check reading the directory would
/// call that page present.
///
/// It reads the page a second time in this suite. The other reader is the class
/// that compares the page's list of backends against the names this plugin
/// answers to, which is a question about the page's contents; this is the tree
/// side of a claim a document makes, and the two are kept apart so a document
/// check does not go red when that comparison is reshaped.
/// </remarks>
internal static class ConfigurationPageSource
{
    /// <summary>
    /// The one thing a document filing this page as unbuilt is disagreeing with:
    /// the line that saves an operator's choice of backend.
    /// </summary>
    /// <remarks>
    /// Assembled rather than written out, so this file does not carry a literal
    /// that a search for the setting's own name would return.
    /// </remarks>
    private const string SavesTheChoice = "config." + nameof(PluginConfiguration.Backend) + " =";

    /// <summary>
    /// The markup and script of the one page this plugin registers.
    /// </summary>
    /// <returns>The page.</returns>
    internal static string Markup()
    {
        var plugin = new PluginUnderTest(new UnwrittenApplicationPaths(), new ThrowingXmlSerializer());
        var page = Assert.Single(plugin.GetPages());

        using var stream = typeof(PluginUnderTest).Assembly.GetManifestResourceStream(page.EmbeddedResourcePath);

        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Refuses the caller where the page an operator opens does not save a choice
    /// of backend, which is the premise every claim below rests on.
    /// </summary>
    /// <remarks>
    /// WHAT THIS DOES NOT DO. It matches the line the page saves the setting with
    /// rather than the behaviour, so a page rewritten to save the same setting
    /// another way turns the checks that call this red while an operator sees no
    /// difference. That is the safe direction: it fails towards somebody reading
    /// the page again.
    /// </remarks>
    internal static void RefuseUnlessAnOperatorChoosesTheBackendOnIt()
    {
        Assert.True(
            Markup().Contains(SavesTheChoice, StringComparison.Ordinal),
            $"the configuration page this plugin registers does not carry \"{SavesTheChoice}\", so the claim these checks are the tree side of no longer holds and the pages they read may be right");
    }
}
