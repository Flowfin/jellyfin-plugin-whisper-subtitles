using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A seam exists so the expensive half can be replaced by something deterministic.
/// One that nothing in this suite replaces is a seam in name only, and this is
/// what refuses that.
/// </summary>
/// <remarks>
/// The failure it is written against is quiet, which is why a test is worth
/// spending on it. Adding an interface costs nothing and reads as testability
/// arriving; what makes it real is a double, and a seam that never got one fails
/// nothing, because the code behind it goes on being exercised through whatever
/// path already existed. The gap only surfaces the day somebody needs to write a
/// test that cannot be written, which is the day they are trying to do something
/// else.
///
/// The population is derived rather than listed. A list of the seams in this file
/// would drift against the plugin the moment one is added, and the drift would be
/// silent in the direction that matters: a new seam missing from the list is a new
/// seam nothing asks about.
///
/// WHAT THIS READS IS TYPES AND NEVER BEHAVIOUR. A double that implements every
/// member by throwing satisfies this, and so does one no test ever constructs.
/// Whether the double is faithful to the thing it stands in for is a judgement,
/// and the suites that use it are where a wrong one is caught. What is asserted
/// here is narrower and is the half that can go missing without anything noticing:
/// that a stand-in exists at all.
///
/// IT SAYS NOTHING ABOUT THE COMPOSITION ROOT. #71 asks for a second thing beside
/// this one, that no type outside the composition root constructs a real
/// implementation directly, and reflection cannot answer it: a construction inside
/// a method body is invisible to a reading of signatures. The tree already holds
/// one such site, named in <c>PluginServiceRegistrator</c>'s own remarks, and the
/// route that would refuse the shape is the token scan in
/// <see cref="UntrustedInputTests"/> rather than anything here.
/// </remarks>
public class SeamDoubleTests
{
    public static TheoryData<string> EverySeam =>
        new(Seams().Select(seam => seam.FullName!).ToArray());

    [Fact]
    public void The_check_can_see_the_seams_it_judges()
    {
        // An empty population passes every theory below it while asserting nothing,
        // and it is the state a renamed assembly or a changed visibility rule
        // produces. So the count is asserted rather than assumed, and the floor is
        // the seams the plan names rather than one.
        var seams = Seams();

        Assert.True(
            seams.Length >= 4,
            $"the plugin assembly declares {seams.Length} public seam(s), which is fewer than this check was written against.");

        Assert.Contains(typeof(ITranscriptionBackend), seams);
    }

    [Theory]
    [MemberData(nameof(EverySeam))]
    public void Every_seam_this_plugin_declares_has_a_double_in_the_suite(string seamName)
    {
        var seam = Seams().Single(candidate => string.Equals(candidate.FullName, seamName, StringComparison.Ordinal));

        var doubles = typeof(SeamDoubleTests).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(seam.IsAssignableFrom)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            doubles.Length > 0,
            $"{seam.Name} is a seam this plugin declares and no type in this suite implements it, so nothing can be substituted for the thing behind it.");
    }

    [Fact]
    public void A_double_is_a_type_this_suite_owns_rather_than_the_real_implementation()
    {
        // The direction that would make the theory above pass while proving nothing:
        // counting the plugin's own implementation as its own stand-in. The scan is
        // over this assembly and never over the plugin's, so a seam whose only
        // implementation ships in the plugin has no double, and this states that
        // rather than leaving it to be read out of the query.
        var doubles = typeof(SeamDoubleTests).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(typeof(ITranscriptionBackend).IsAssignableFrom)
            .ToArray();

        Assert.NotEmpty(doubles);
        Assert.All(doubles, type => Assert.Equal(typeof(SeamDoubleTests).Assembly, type.Assembly));
    }

    /// <summary>
    /// Every public interface the plugin assembly declares.
    /// </summary>
    /// <remarks>
    /// Public rather than every interface, because a seam is a thing something
    /// outside its own file substitutes for, and an internal one is not offered to
    /// anybody. Derived from the assembly rather than from the source tree, so an
    /// interface that is declared and never compiled is not counted as one this
    /// suite owes a double for.
    /// </remarks>
    private static Type[] Seams() =>
        typeof(ITranscriptionBackend).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && type.IsPublic)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
}
