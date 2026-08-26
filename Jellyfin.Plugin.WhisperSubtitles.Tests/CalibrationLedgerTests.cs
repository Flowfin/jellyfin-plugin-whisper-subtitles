using System;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Calibration;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// A throughput measurement is only evidence about the settings it was taken
/// under, and these are the ways it stops being that without anybody noticing.
/// </summary>
/// <remarks>
/// <see cref="ThroughputFactorTests"/> judges the arithmetic and is right about
/// every number it is given. Nothing in it can see the failure here, which is the
/// arithmetic being fed two quantities that are not the same quantity: seconds of
/// audio under the tiny model on eight threads and seconds of audio under the
/// large model on two are different measurements wearing one name, and their
/// average describes a configuration nobody has ever run.
///
/// Every moment below is stated. This plugin reads no wall clock and neither does
/// its suite, so the moment a measurement was taken is a parameter, and a test that
/// waited for time to pass would be refused by <see cref="DeterminismTests"/>.
/// </remarks>
public class CalibrationLedgerTests
{
    private static readonly DateTimeOffset _monday = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _tuesday = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nothing_is_held_before_anything_is_measured()
    {
        var ledger = new CalibrationLedger();

        Assert.False(ledger.HasAMeasurement);
        Assert.Null(ledger.For(Tiny()));
    }

    [Fact]
    public void A_measurement_is_returned_for_the_settings_it_was_taken_under()
    {
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _monday);

        var held = ledger.For(Tiny());

        Assert.NotNull(held);
        Assert.Equal(3.0, held.Factor.WorkPerSecondOfAudio, 6);
        Assert.Equal(_monday, held.MeasuredAt);
        Assert.Equal(Tiny(), held.Key);
    }

    [Theory]
    [InlineData("Remote", "ggml-tiny.bin", 8)]
    [InlineData(LocalWhisperBackend.BackendName, "ggml-large.bin", 8)]
    [InlineData(LocalWhisperBackend.BackendName, "ggml-tiny.bin", 2)]
    public void A_measurement_answers_to_no_other_settings(string backend, string model, int threads)
    {
        // One part of the key different at a time, so a comparison that reads only
        // some of the three is visible. Each of these is a real way an operator
        // changes what a second of audio costs.
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _monday);

        Assert.Null(ledger.For(new CalibrationKey(backend, model, threads)));
    }

    [Fact]
    public void A_settings_change_throws_the_measurement_away_rather_than_leaving_it_to_be_asked_for()
    {
        // The invalidation this issue names, and the reason it is not just the
        // lookup returning nothing: a configuration change happens with no item in
        // sight, and a stale measurement left sitting there is one an estimate can
        // still be built from by anything that asks under the old key.
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _monday);

        Assert.True(ledger.InvalidateUnless(Large()));

        Assert.False(ledger.HasAMeasurement);
        Assert.Null(ledger.For(Tiny()));
        Assert.Null(ledger.For(Large()));
    }

    [Fact]
    public void The_settings_staying_put_throws_nothing_away()
    {
        // The other direction, and the one that costs an operator a re-measurement
        // if it is wrong. Something asking on every configuration save would
        // otherwise discard a good measurement each time anything unrelated moved.
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _monday);

        Assert.False(ledger.InvalidateUnless(Tiny()));

        Assert.NotNull(ledger.For(Tiny()));
    }

    [Fact]
    public void An_item_transcribed_under_new_settings_replaces_the_measurement_rather_than_joining_it()
    {
        // The path that does not go through InvalidateUnless at all: nobody told the
        // ledger the settings moved, and the first item under the new ones says so.
        // Folding it in would average two quantities that are not the same quantity.
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromHours(10), TimeSpan.FromHours(10), _monday);

        var held = ledger.Record(Large(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), _tuesday);

        Assert.Equal(Large(), held.Key);
        Assert.Equal(1, held.Factor.Items);
        Assert.Equal(5.0, held.Factor.WorkPerSecondOfAudio, 6);
        Assert.Null(ledger.For(Tiny()));
    }

    [Fact]
    public void A_second_item_under_the_same_settings_refines_the_measurement()
    {
        // The running update the issue asks for, at the ledger rather than at the
        // arithmetic: two items under one key are one measurement of two items.
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _monday);

        var held = ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), _tuesday);

        Assert.Equal(2, held.Factor.Items);
        Assert.Equal(4.0, held.Factor.WorkPerSecondOfAudio, 6);
    }

    [Fact]
    public void The_measurement_carries_the_moment_its_newest_item_finished()
    {
        // What a reader wants from the date is how old the evidence is. A
        // measurement refined yesterday is not a month old because it started a
        // month ago, so the moment moves with each item folded in.
        var ledger = new CalibrationLedger();

        ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _monday);

        Assert.Equal(_tuesday, ledger.Record(Tiny(), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _tuesday).MeasuredAt);
    }

    [Fact]
    public void Two_keys_naming_the_same_settings_are_one_key()
    {
        // The lookup is by value and not by the object a caller happens to hold. A
        // key rebuilt from configuration on every read is the normal case, so
        // reference equality here would make every measurement unreachable.
        Assert.Equal(Tiny(), Tiny());
        Assert.Equal(Tiny().GetHashCode(), Tiny().GetHashCode());
        Assert.NotEqual(Tiny(), Large());
    }

    [Fact]
    public void Surrounding_space_in_a_typed_path_is_not_a_different_model()
    {
        // The model is whatever an operator typed into a field, and a trailing space
        // is the commonest thing a field carries that a person did not mean. Reading
        // it as another model would silently throw a good measurement away.
        Assert.Equal(
            new CalibrationKey(LocalWhisperBackend.BackendName, "  ggml-tiny.bin ", 8),
            Tiny());
    }

    [Theory]
    [InlineData("", "ggml-tiny.bin", 8)]
    [InlineData("   ", "ggml-tiny.bin", 8)]
    [InlineData(LocalWhisperBackend.BackendName, "ggml-tiny.bin", 0)]
    [InlineData(LocalWhisperBackend.BackendName, "ggml-tiny.bin", -1)]
    public void A_key_that_names_no_backend_or_no_thread_count_is_refused(string backend, string model, int threads)
    {
        // A key with a blank backend or a thread count that is not a number of
        // threads describes no run, and one built by accident would collect items
        // from every configuration into one measurement.
        Assert.ThrowsAny<ArgumentException>(() => new CalibrationKey(backend, model, threads));
    }

    [Fact]
    public void A_backend_that_takes_no_model_still_makes_a_key()
    {
        // Empty is legal and means a backend with no model to name. Refusing it
        // would leave such a backend unable to be calibrated at all.
        var key = new CalibrationKey("Remote", string.Empty, 4);

        Assert.Equal(string.Empty, key.Model);
        Assert.Contains("(none)", key.ToString(), StringComparison.Ordinal);
    }

    private static CalibrationKey Tiny() =>
        new(LocalWhisperBackend.BackendName, "ggml-tiny.bin", 8);

    private static CalibrationKey Large() =>
        new(LocalWhisperBackend.BackendName, "ggml-large.bin", 2);
}
