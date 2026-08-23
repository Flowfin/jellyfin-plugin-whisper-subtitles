using System;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Jellyfin.Plugin.WhisperSubtitles.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The names this plugin puts on surfaces a server shares between plugins, written
/// down here so that changing one is a change to this file as well.
///
/// #64 is the scan that compares them against what the other supported plugins
/// claim, and it needs a running server. What it also asks for, and what needs no
/// server, is that the set this plugin claims lives in the repository and is
/// compared on every run, so a later change appears as a difference rather than as
/// a surprise in the full-set run. The task key and the routes are held that way
/// already, in <see cref="SubtitleGenerationTaskTests"/> and
/// <see cref="RouteClaimsTests"/>. These are the ones that were not.
///
/// Every assertion below compares against a literal rather than against the
/// constant it came from. A comparison to the constant moves with the constant and
/// stays green through a rename, which is the whole failure this is written
/// against.
/// </summary>
public class ClaimedNamesTests
{
    [Fact]
    public void The_task_carries_the_name_an_operator_finds_it_under()
    {
        // The server shows this string in the scheduled task list, so it is what an
        // operator reads and what a second plugin's task could be confused with. The
        // suite otherwise only asks that it is not blank, which a rename passes.
        Assert.Equal("Generate subtitles", Task().Name);
    }

    [Fact]
    public void The_marker_a_generated_subtitle_carries_is_this_word()
    {
        // What a viewer reads in a track list. Its properties are held by
        // MachineMadeMarkerTests against the server's own flag vocabularies, and
        // every one of those assertions reads the constant, so all of them survive
        // the word being changed to another word with the same properties.
        Assert.Equal("Transcribed", GeneratedSubtitleName.Marker);
    }

    [Fact]
    public void The_file_name_a_generated_subtitle_takes_is_this_shape()
    {
        // The name another plugin writing subtitles could claim, recorded whole
        // rather than as the parts it is built from. The base name comes off the
        // media file, so what this plugin decides is the order of the fields, the
        // dots between them and the marker sitting where the server's parser leaves
        // it as a title.
        Assert.Equal(
            "Arrival (2016).en.Transcribed.srt",
            GeneratedSubtitleName.For("/media/Films/Arrival (2016)/Arrival (2016).mkv", "en", "srt"));
    }

    private static SubtitleGenerationTask Task() =>
        new(Array.Empty<BackendCandidate>());
}
