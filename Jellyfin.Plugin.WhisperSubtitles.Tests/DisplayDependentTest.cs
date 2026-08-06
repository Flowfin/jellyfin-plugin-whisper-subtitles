using System;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Added on purpose to show that the headless job refuses a test which needs a
/// display, and removed in the next commit. It is a stand-in: a test that opened
/// an X connection would need a library this tree does not carry, and what makes
/// such a test fail on a headless machine is exactly this dependency.
/// </summary>
public class DisplayDependentTest
{
    [Fact]
    public void Needs_a_display()
    {
        Assert.False(
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")),
            "there is no display to draw on");
    }
}
