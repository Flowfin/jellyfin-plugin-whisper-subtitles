using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The case this exists for cannot be arranged by disposing anything, because
/// the case is that nothing was disposed: a process that stopped between writing
/// a file and deleting it. So the arrangement is the leftover itself, placed in
/// the directory a dead run would have left it in.
///
/// Most of these assert on what the sweep does not touch. A collector aimed at a
/// directory is one edit away from being a collector aimed at a media tree, and
/// the edits that do it are ordinary ones: a wider pattern, a recursive search.
/// </summary>
public sealed class TemporaryAudioSweepTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void A_file_a_dead_run_left_behind_is_collected()
    {
        var stale = Leftover("6f1d2c3b4a594e6f8a7b0c1d2e3f4a5b.wav");

        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.False(File.Exists(stale));
        Assert.Equal(1, outcome.Collected);
        Assert.Equal(0, outcome.Left);
    }

    [Fact]
    public void Every_leftover_goes_rather_than_the_first_one()
    {
        Leftover("a.wav");
        Leftover("b.wav");
        Leftover("c.wav");

        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.Equal(3, outcome.Collected);
        Assert.Empty(Directory.EnumerateFiles(_workingDirectory));
    }

    [Fact]
    public void A_first_run_with_no_directory_yet_is_not_a_failure()
    {
        // Nothing has been extracted on this server, so the directory has never
        // been created. Refusing to start over that turns an empty disk into a
        // failed run.
        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.Equal(0, outcome.Collected);
        Assert.Equal(0, outcome.Left);
    }

    [Fact]
    public void Nothing_but_extracted_audio_is_removed()
    {
        // The pattern is the bound. A sweep of everything in the directory would
        // work identically today and would take whatever the next feature puts
        // there with it.
        var notAudio = Leftover("notes.txt");
        var alsoNot = Leftover("model.bin");
        var audio = Leftover("kept-until-swept.wav");

        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.True(File.Exists(notAudio));
        Assert.True(File.Exists(alsoNot));
        Assert.False(File.Exists(audio));
        Assert.Equal(1, outcome.Collected);
    }

    [Fact]
    public void A_directory_below_the_working_directory_is_not_walked()
    {
        // The failure this refuses is a working directory an operator pointed at
        // a place with other things under it. A recursive sweep of that is a
        // deletion nobody asked for, and it is one enum value away.
        var below = Path.Combine(_workingDirectory, "somebody-elses");
        Directory.CreateDirectory(below);

        var untouched = Path.Combine(below, "theirs.wav");
        File.WriteAllText(untouched, "not this plugin's");

        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.True(File.Exists(untouched));
        Assert.Equal(0, outcome.Collected);
        Assert.True(Directory.Exists(below), "the sweep removed a directory rather than a file");
    }

    [Fact]
    public void A_file_that_will_not_go_is_counted_rather_than_thrown()
    {
        // Something still holds it open. The next sweep finds it released, and a
        // run that refused to start over one locked leftover is a worse outcome
        // than the leftover.
        var held = Leftover("held-open.wav");
        var goes = Leftover("free.wav");

        using var handle = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None);

        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.Equal(1, outcome.Collected);
        Assert.Equal(1, outcome.Left);
        Assert.False(File.Exists(goes));
    }

    [Fact]
    public async Task What_the_extractor_writes_is_what_the_sweep_matches()
    {
        // The two halves read from one another rather than from a reader's
        // memory. A rename of the extension in either place, with the other left
        // alone, leaves a sweep that runs and collects nothing, which reports as
        // a clean directory rather than as a defect.
        var runner = MediaToolRunner.Writing(2048);
        var extractor = new AudioExtractor(runner, "/usr/lib/jellyfin-ffmpeg/ffmpeg", _workingDirectory);

        var audio = await extractor
            .ExtractAsync("/media/films/A Film.mkv", new AudioStreamDescription(1, "eng", 2, isDefault: true), CancellationToken.None)
            .ConfigureAwait(true);

        // Not disposed, which is the whole arrangement: this is the file a
        // process that stopped would have left.
        Assert.True(File.Exists(audio.Path));

        var outcome = TemporaryAudioSweep.Run(_workingDirectory);

        Assert.Equal(1, outcome.Collected);
        Assert.False(File.Exists(audio.Path));
    }

    [Fact]
    public void A_directory_that_was_not_named_is_refused()
    {
        Assert.Throws<ArgumentException>(() => TemporaryAudioSweep.Run("   "));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    private string Leftover(string name)
    {
        Directory.CreateDirectory(_workingDirectory);

        var path = Path.Combine(_workingDirectory, name);

        File.WriteAllText(path, "what a run that died left behind");

        return path;
    }
}
