using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Output;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A subtitle written by this plugin is read by players this repository will
/// never run, so the evidence that it is well formed is a file whose bytes are
/// fixed rather than a string this suite builds and then agrees with itself
/// about. That only works while the file arrives out of a clone unchanged, which
/// is what <c>.gitattributes</c> is for and what these assertions notice the loss
/// of.
/// </summary>
public class SubRipFixtureBytesTests
{
    private static readonly IReadOnlyList<TimedSegment> _segments = new[]
    {
        new TimedSegment(TimeSpan.Zero, TimeSpan.FromMilliseconds(1500), "First line."),
        new TimedSegment(TimeSpan.FromMilliseconds(1500), new TimeSpan(0, 1, 2, 3, 456), "Second line."),
        new TimedSegment(new TimeSpan(0, 1, 2, 3, 456), new TimeSpan(0, 1, 2, 5, 0), "Über den Wolken, 山の音, \U0001F600")
    };

    [Fact]
    public void The_writer_produces_the_committed_fixture_byte_for_byte()
    {
        Assert.Equal(Fixture(), new SubRipWriter().Write(_segments));
    }

    [Fact]
    public void The_fixture_carries_no_line_feed_that_is_not_preceded_by_a_carriage_return()
    {
        // This is the assertion that fails when a checkout has rewritten the file,
        // and it is separate from the comparison above so the failure says which
        // of the two moved. A clone that stripped the carriage returns leaves the
        // writer untouched and the fixture wrong; a change to the writer leaves
        // the fixture untouched and the comparison wrong.
        var bytes = Fixture();

        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                Assert.True(
                    i > 0 && bytes[i - 1] == (byte)'\r',
                    $"byte {i} is a line feed with no carriage return before it, so this clone did not check the fixture out as committed");
            }
        }

        Assert.Contains((byte)'\r', bytes);
    }

    private static byte[] Fixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "three-cues.srt");

        Assert.True(File.Exists(path), $"the fixture was not copied next to the test assembly, looked in {path}");

        return File.ReadAllBytes(path);
    }
}
