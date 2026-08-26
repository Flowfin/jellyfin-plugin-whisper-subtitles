using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;

namespace Jellyfin.Plugin.WhisperSubtitles.Estimation;

/// <summary>
/// How much memory the selected model holds while it runs, in the words the
/// project that publishes the model uses.
/// </summary>
/// <remarks>
/// NOTHING HERE WAS MEASURED BY THIS PLUGIN, and that is the whole reason this
/// type exists rather than a number written into a page. The figures are the
/// ones whisper.cpp publishes for its own models, and they are quoted rather
/// than converted: a byte count computed from "about 273 MB" would be a
/// conversion this plugin invented, and a reader could not tell it from a
/// measurement. So the figure travels as the text it was published as, and the
/// sentence a surface shows says whose number it is.
///
/// A model is identified by the file name an operator typed and never by
/// resolving a path. Resolving would mean reading a disk to answer a question
/// about a table, and a dry run that touched the disk to say what a run would
/// cost would be doing part of the run.
///
/// WHERE THE NAME IS AMBIGUOUS THIS ANSWERS NOTHING RATHER THAN GUESSING. A file
/// name carrying two of the size words is not a model this table knows twice; it
/// is a name this table cannot place, and picking either one would put a figure
/// in front of an operator that is wrong by an order of magnitude in the
/// direction that fills a machine.
/// </remarks>
public static class ModelMemory
{
    /// <summary>
    /// What an operator is told when the file name is not one of the published
    /// sizes.
    /// </summary>
    public const string NotRecognised =
        "unknown: the model name is not one of the sizes whisper.cpp publishes a figure for.";

    /// <summary>
    /// What an operator is told when the transcribing happens somewhere else.
    /// </summary>
    /// <remarks>
    /// The honest answer rather than a blank. A remote endpoint holds the model on
    /// its own machine, and a figure shown next to an operator's own server would
    /// be read as memory their server needs to have.
    /// </remarks>
    public const string BelongsToTheEndpoint =
        "held by the machine running the endpoint, not by this server.";

    private static readonly FrozenDictionary<string, string> _published =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tiny"] = "about 273 MB",
            ["base"] = "about 388 MB",
            ["small"] = "about 852 MB",
            ["medium"] = "about 2.1 GB",
            ["large"] = "about 3.9 GB",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets every size word this table has a published figure for.
    /// </summary>
    public static IReadOnlyCollection<string> Sizes => _published.Keys;

    /// <summary>
    /// The figure whisper.cpp publishes for the model in that file name.
    /// </summary>
    /// <param name="model">Whatever the operator typed as the model.</param>
    /// <returns>The published figure, or null where the name is not one this table places.</returns>
    public static string? PublishedFor(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var name = FileNameOf(model);

        var matched = _published.Keys
            .Where(size => name.Contains(size, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matched.Length == 1 ? _published[matched[0]] : null;
    }

    /// <summary>
    /// The one line a dry run shows for how much memory the model holds.
    /// </summary>
    /// <param name="backend">The backend the run would use.</param>
    /// <param name="model">Whatever the operator typed as the model.</param>
    /// <returns>The sentence, which always says whose figure it is.</returns>
    public static string SentenceFor(string? backend, string? model)
    {
        if (string.Equals(backend, RemoteWhisperBackend.BackendName, StringComparison.OrdinalIgnoreCase))
        {
            return BelongsToTheEndpoint;
        }

        var published = PublishedFor(model);

        return published is null
            ? NotRecognised
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}, which is the figure whisper.cpp publishes for that model and not one measured here.",
                published);
    }

    private static string FileNameOf(string model)
    {
        var trimmed = model.Trim();

        try
        {
            var name = Path.GetFileName(trimmed);

            return name.Length == 0 ? trimmed : name;
        }
        catch (ArgumentException)
        {
            return trimmed;
        }
    }
}
