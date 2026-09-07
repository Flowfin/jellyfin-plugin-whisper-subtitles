using System.Threading.Tasks;

// This file is the fixture set for the translation surface reader: the request that
// can be asked for a translation, the one-change neighbour that cannot, and the same
// pair on a call rather than on a type. They are read together or not at all, so
// splitting them across four files by name would separate each fixture from the
// neighbour it is only meaningful beside.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable CA1801 // Parameter is never used
#pragma warning disable IDE0060 // Remove unused parameter

namespace Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures.Translation;

/// <summary>
/// The mistake the reader exists to find: a request carrying the language that was
/// spoken and the language to produce, which is a translation asked for in two
/// fields.
/// </summary>
internal sealed class TranslatingRequest
{
    public TranslatingRequest(string audioFilePath, string spokenLanguage, string targetLanguage)
    {
        AudioFilePath = audioFilePath;
        SpokenLanguage = spokenLanguage;
        TargetLanguage = targetLanguage;
    }

    public string AudioFilePath { get; }

    public string SpokenLanguage { get; }

    public string TargetLanguage { get; }
}

/// <summary>
/// The one-change neighbour. The same request with one language rather than two,
/// which is the shape this plugin's own request has and which has to stay accepted
/// or the leg above would pass over any request at all.
/// </summary>
internal sealed class TranscribingRequest
{
    public TranscribingRequest(string audioFilePath, string? language)
    {
        AudioFilePath = audioFilePath;
        Language = language;
    }

    public string AudioFilePath { get; }

    public string? Language { get; }
}

/// <summary>
/// The same mistake made where only the constructor sees it: the second language
/// arrives as a constructor parameter and is kept in a property named something
/// else. It is the shape a request written with positional parameters produces, and
/// it is the one the reader's property arm cannot answer, so it is what holds that
/// arm's neighbour honest.
/// </summary>
internal sealed class ConstructedTranslatingRequest
{
    public ConstructedTranslatingRequest(string audioFilePath, string spokenLanguage, string targetLanguage)
    {
        AudioFilePath = audioFilePath;
        Spoken = spokenLanguage;
        Target = targetLanguage;
    }

    public string AudioFilePath { get; }

    public string Spoken { get; }

    public string Target { get; }
}

/// <summary>
/// The one-change neighbour for the pair above, and the near-miss is deliberate: a
/// constructor parameter that abbreviates the word rather than carrying it. A
/// reader loosened to match <c>lang</c> accepts this one, and a reader that simply
/// returned every constructor parameter it met accepts both, so the leg beside it
/// would pass either way without this here.
/// </summary>
internal sealed class ConstructedTranscribingRequest
{
    public ConstructedTranscribingRequest(string audioFilePath, string langCode)
    {
        AudioFilePath = audioFilePath;
        Spoken = langCode;
    }

    public string AudioFilePath { get; }

    public string Spoken { get; }
}

/// <summary>
/// The same pair on a call. A second language reaching a backend beside the request
/// is the other way a translation could be asked for, and it is a different subject
/// from the request type because nothing about the request would move.
/// </summary>
internal static class TranslatingCall
{
    public static Task TranscribeAsync(TranscribingRequest request, string targetLanguage) =>
        Task.CompletedTask;

    public static Task TranscribeAsync(TranscribingRequest request) =>
        Task.CompletedTask;
}
