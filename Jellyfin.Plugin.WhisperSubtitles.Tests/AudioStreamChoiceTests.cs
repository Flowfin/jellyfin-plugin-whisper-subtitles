using System;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Choosing the wrong audio stream produces a subtitle that is well formed,
/// correctly timed, and about something else. Nothing downstream can notice
/// that, so the rule is decided here and each step of it has a case.
/// </summary>
public class AudioStreamChoiceTests
{
    [Fact]
    public void An_item_with_no_audio_chooses_nothing()
    {
        Assert.Null(AudioStreamChoice.Choose([], "eng"));
    }

    [Fact]
    public void The_only_stream_wins_whatever_it_says_about_itself()
    {
        var only = new AudioStreamDescription(1, null, 2, isDefault: false);

        Assert.Same(only, AudioStreamChoice.Choose([only], "eng"));
    }

    [Fact]
    public void The_stream_in_the_language_being_asked_for_wins()
    {
        var german = new AudioStreamDescription(1, "ger", 2, isDefault: true);
        var english = new AudioStreamDescription(2, "eng", 2, isDefault: false);

        // Over the default, deliberately. An operator asking for English subtitles
        // on a film whose default track is German has said which audio they want
        // words from, and the container's idea of default is not that answer.
        Assert.Same(english, AudioStreamChoice.Choose([german, english], "eng"));
    }

    [Fact]
    public void The_language_is_matched_without_regard_to_case()
    {
        var english = new AudioStreamDescription(3, "ENG", 2, isDefault: false);
        var german = new AudioStreamDescription(1, "ger", 6, isDefault: true);

        Assert.Same(english, AudioStreamChoice.Choose([german, english], "eng"));
    }

    [Fact]
    public void The_default_wins_when_nothing_carries_the_language_asked_for()
    {
        var commentary = new AudioStreamDescription(1, "fre", 2, isDefault: false);
        var dialogue = new AudioStreamDescription(2, "fre", 2, isDefault: true);

        // Not a failure. A container that says nothing about English still has the
        // track a viewer hears when they press play, and detection may yet find
        // English on it.
        Assert.Same(dialogue, AudioStreamChoice.Choose([commentary, dialogue], "eng"));
    }

    [Fact]
    public void The_default_wins_when_no_language_is_being_asked_for_at_all()
    {
        var first = new AudioStreamDescription(1, "eng", 6, isDefault: false);
        var marked = new AudioStreamDescription(2, "eng", 2, isDefault: true);

        Assert.Same(marked, AudioStreamChoice.Choose([first, marked], null));
    }

    [Fact]
    public void With_no_default_the_track_with_more_channels_wins()
    {
        // The common shape of a film with a commentary: neither is marked, the
        // dialogue is the surround track and the commentary is the mono one.
        var commentary = new AudioStreamDescription(2, "eng", 1, isDefault: false);
        var dialogue = new AudioStreamDescription(3, "eng", 6, isDefault: false);

        Assert.Same(dialogue, AudioStreamChoice.Choose([commentary, dialogue], null));
    }

    [Fact]
    public void Two_streams_nothing_distinguishes_choose_the_lower_index_every_time()
    {
        // Determinism rather than preference. The same item must produce the same
        // subtitle on every run, and without this the answer follows whatever order
        // the streams arrived in.
        var second = new AudioStreamDescription(4, "eng", 2, isDefault: false);
        var first = new AudioStreamDescription(2, "eng", 2, isDefault: false);

        Assert.Same(first, AudioStreamChoice.Choose([second, first], null));
        Assert.Same(first, AudioStreamChoice.Choose([first, second], null));
    }

    [Fact]
    public void Two_streams_in_the_wanted_language_choose_the_lower_index()
    {
        var second = new AudioStreamDescription(5, "eng", 2, isDefault: false);
        var first = new AudioStreamDescription(3, "eng", 2, isDefault: false);

        Assert.Same(first, AudioStreamChoice.Choose([second, first], "eng"));
    }

    [Fact]
    public void A_blank_language_is_read_as_no_preference_rather_than_as_a_language_to_match()
    {
        var marked = new AudioStreamDescription(2, "ger", 2, isDefault: true);
        var other = new AudioStreamDescription(1, "  ", 2, isDefault: false);

        Assert.Same(marked, AudioStreamChoice.Choose([other, marked], "   "));
    }

    [Fact]
    public void There_is_no_stream_list_to_choose_from_is_an_error_and_not_an_empty_list()
    {
        Assert.Throws<ArgumentNullException>(() => AudioStreamChoice.Choose(null!, "eng"));
    }
}
