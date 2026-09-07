using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Finds every place a language can be named on the way into a transcription.
/// </summary>
/// <remarks>
/// A transcription needs one language, which says what the audio is in. A
/// translation needs two, because it has to carry the language that was spoken and
/// the language to produce. So the count is the difference between the two, and a
/// surface that can only hold one cannot be asked for a translation whatever a
/// backend behind it is willing to do.
///
/// The bound is worth knowing before trusting a green run. This reads NAMES, on
/// public properties and public constructor parameters of a type, and on the
/// parameters of a method. A second language carried under a name that does not
/// mention one, in a dictionary, or inside a string somebody parses, is invisible
/// to it, and so is a backend that translates on its own without being asked.
/// </remarks>
internal static class TranslationSurface
{
    /// <summary>
    /// Names every public property and constructor parameter of <paramref name="type"/>
    /// that names a language.
    /// </summary>
    /// <param name="type">The request type to read.</param>
    /// <returns>The names, deduplicated without regard to case and sorted.</returns>
    public static IReadOnlyList<string> LanguagesNamedBy(Type type) =>
        Settle(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(NamesALanguage)
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.Name)
                .Where(name => name is not null && NamesALanguage(name))
                .Select(name => name!)));

    /// <summary>
    /// Names every parameter of <paramref name="method"/> that names a language.
    /// </summary>
    /// <param name="method">The transcribe call to read.</param>
    /// <returns>The names, deduplicated without regard to case and sorted.</returns>
    public static IReadOnlyList<string> LanguagesNamedBy(MethodInfo method) =>
        Settle(method.GetParameters()
            .Select(parameter => parameter.Name)
            .Where(name => name is not null && NamesALanguage(name))
            .Select(name => name!));

    // The constructor parameter and the property it sets are one place a language can
    // be named and not two, so the spelling difference between them is settled here
    // rather than being reported as a second language.
    private static List<string> Settle(IEnumerable<string> found) =>
        found.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static bool NamesALanguage(string name) =>
        name.Contains("language", StringComparison.OrdinalIgnoreCase);
}
