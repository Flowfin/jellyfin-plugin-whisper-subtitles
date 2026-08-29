using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.WhisperSubtitles.Audio;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Calibration;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using Jellyfin.Plugin.WhisperSubtitles.Estimation;
using Jellyfin.Plugin.WhisperSubtitles.Selection;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// What a dry run is for is an operator learning the cost before committing a
/// machine, and the two ways it can fail that operator are the two things
/// asserted here: doing the work it was asked to describe, and showing a number
/// nothing measured.
/// </summary>
/// <remarks>
/// The second is the one worth stating carefully. Every backend answers
/// <see cref="ITranscriptionBackend.EstimateCost"/> with a range today, so a dry
/// run always has two numbers within reach, and both configured backends say in
/// their own remarks that theirs is a placeholder. A surface showing those would
/// be indistinguishable, to the operator reading it, from one showing a range
/// measured on their own library. So the refusal is asserted as behaviour rather
/// than left as an intention.
/// </remarks>
public sealed class DryRunTests
{
    private static readonly Guid _library = new("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset _epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_dry_run_asks_the_backend_for_no_transcription()
    {
        var backend = new StubBackend();

        var report = Perform(backend, Items(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));

        Assert.Equal(0, backend.TranscriptionsAsked);
        Assert.Equal(2, report.Items);
    }

    [Fact]
    public void The_report_names_the_count_and_the_total_duration_of_what_was_selected()
    {
        var report = Perform(
            new StubBackend(),
            Items(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(6)));

        Assert.Equal(3, report.Items);
        Assert.Equal(TimeSpan.FromMinutes(96), report.TotalDuration);
    }

    [Fact]
    public void The_report_names_the_threads_one_transcription_gets_and_how_many_run_together()
    {
        var report = Perform(new StubBackend(), Items(TimeSpan.FromHours(1)), itemsAtOnce: 3, threadsPerItem: 5);

        Assert.Equal(3, report.ItemsAtOnce);
        Assert.Equal(5, report.ThreadsPerItem);
    }

    [Fact]
    public void The_peak_temporary_disk_is_the_items_in_flight_and_not_the_whole_selection()
    {
        var durations = new[]
        {
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(10),
        };

        var report = Perform(new StubBackend(), Items(durations), itemsAtOnce: 2);

        var twoLongest = PcmAudio.SmallestSizeFor(TimeSpan.FromHours(2))
            + PcmAudio.SmallestSizeFor(TimeSpan.FromHours(1));
        var everything = durations.Sum(duration => PcmAudio.SmallestSizeFor(duration));

        Assert.Equal(twoLongest, report.PeakTemporaryAudioBytes);
        Assert.True(report.PeakTemporaryAudioBytes < everything);
    }

    [Fact]
    public void With_fewer_items_than_seats_the_peak_is_everything_the_selection_holds()
    {
        var report = Perform(new StubBackend(), Items(TimeSpan.FromMinutes(20)), itemsAtOnce: 8);

        Assert.Equal(PcmAudio.SmallestSizeFor(TimeSpan.FromMinutes(20)), report.PeakTemporaryAudioBytes);
    }

    [Fact]
    public void With_nothing_measured_the_estimate_refuses_a_number_and_says_why()
    {
        var report = Perform(new StubBackend(), Items(TimeSpan.FromHours(4)));

        Assert.False(report.Estimate.HasANumber);
        Assert.Equal(DryRunEstimate.NothingMeasuredYet, report.Estimate.Refusal);
        Assert.Null(report.Estimate.Provenance);
    }

    [Fact]
    public void The_placeholder_range_a_backend_offers_is_not_what_the_refusal_falls_back_to()
    {
        var backend = new StubBackend
        {
            Estimate = new CostEstimate(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(9)),
        };

        var report = Perform(backend, Items(TimeSpan.FromHours(4)));

        Assert.False(report.Estimate.HasANumber);
        Assert.NotEqual(TimeSpan.FromMinutes(5), report.Estimate.Shortest);
        Assert.NotEqual(TimeSpan.FromMinutes(9), report.Estimate.Longest);
    }

    [Fact]
    public void A_measurement_taken_under_other_settings_refuses_with_its_own_sentence()
    {
        var ledger = new CalibrationLedger();
        ledger.Record(
            new CalibrationKey("Local", "ggml-tiny.bin", 4),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(40),
            _epoch);

        var report = Perform(
            new StubBackend(),
            Items(TimeSpan.FromHours(4)),
            calibration: ledger,
            model: "ggml-large-v3.bin");

        Assert.False(report.Estimate.HasANumber);
        Assert.Equal(DryRunEstimate.MeasuredUnderOtherSettings, report.Estimate.Refusal);
        Assert.NotEqual(DryRunEstimate.NothingMeasuredYet, report.Estimate.Refusal);
    }

    [Fact]
    public void A_measurement_under_these_settings_gives_a_range_and_says_what_it_rests_on()
    {
        var ledger = new CalibrationLedger();
        var key = new CalibrationKey("Local", "ggml-tiny.bin", 4);

        ledger.Record(key, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), _epoch);
        ledger.Record(key, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(60), _epoch.AddHours(1));

        var report = Perform(
            new StubBackend(),
            Items(TimeSpan.FromHours(1)),
            calibration: ledger,
            model: "ggml-tiny.bin",
            threadsPerItem: 4);

        Assert.True(report.Estimate.HasANumber);
        Assert.Null(report.Estimate.Refusal);

        Assert.Equal(TimeSpan.FromHours(2), report.Estimate.Shortest);
        Assert.Equal(TimeSpan.FromHours(6), report.Estimate.Longest);

        var provenance = Assert.IsType<string>(report.Estimate.Provenance);
        Assert.Contains("ggml-tiny.bin", provenance, StringComparison.Ordinal);
        Assert.Contains("4 thread(s)", provenance, StringComparison.Ordinal);
        Assert.Contains("2 item(s)", provenance, StringComparison.Ordinal);
        Assert.Contains("2026-01-01 01:00:00Z", provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void The_range_is_the_spread_actually_observed_and_not_a_band_around_the_answer()
    {
        var ledger = new CalibrationLedger();
        var key = new CalibrationKey("Local", "ggml-tiny.bin", 4);

        ledger.Record(key, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), _epoch);

        var report = Perform(
            new StubBackend(),
            Items(TimeSpan.FromHours(1)),
            calibration: ledger,
            model: "ggml-tiny.bin",
            threadsPerItem: 4);

        Assert.Equal(report.Estimate.Shortest, report.Estimate.Longest);
        Assert.Equal(TimeSpan.FromHours(3), report.Estimate.Shortest);
    }

    [Theory]
    [InlineData("ggml-tiny.en.bin", "about 273 MB")]
    [InlineData("/var/lib/models/ggml-base.bin", "about 388 MB")]
    [InlineData("ggml-small.bin", "about 852 MB")]
    [InlineData("ggml-medium.bin", "about 2.1 GB")]
    [InlineData("ggml-large-v3.bin", "about 3.9 GB")]
    public void The_memory_figure_is_the_published_one_for_the_model_that_was_named(string model, string figure)
    {
        var report = Perform(new StubBackend(), Items(TimeSpan.FromHours(1)), model: model);

        Assert.Contains(figure, report.ModelMemory, StringComparison.Ordinal);
        Assert.Contains("whisper.cpp publishes", report.ModelMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void A_directory_that_carries_a_size_word_does_not_decide_the_model()
    {
        Assert.Equal("about 388 MB", ModelMemory.PublishedFor("/srv/small-models/ggml-base.bin"));
    }

    [Theory]
    [InlineData("ggml-tiny-and-base.bin")]
    [InlineData("whatever-the-operator-typed.bin")]
    [InlineData("")]
    public void A_name_this_table_cannot_place_gets_no_figure_rather_than_the_nearest_one(string model)
    {
        Assert.Null(ModelMemory.PublishedFor(model));

        var report = Perform(new StubBackend(), Items(TimeSpan.FromHours(1)), model: model);

        Assert.Equal(ModelMemory.NotRecognised, report.ModelMemory);
    }

    [Fact]
    public void For_a_remote_endpoint_the_memory_belongs_to_the_other_machine()
    {
        var report = Perform(
            new StubBackend(),
            Items(TimeSpan.FromHours(1)),
            backendName: "Remote",
            model: "ggml-large-v3.bin");

        Assert.Equal(ModelMemory.BelongsToTheEndpoint, report.ModelMemory);
        Assert.DoesNotContain("3.9 GB", report.ModelMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void An_item_the_selection_rejects_is_in_no_figure_the_report_carries()
    {
        var withAudio = new ItemDescription(
            new Guid("22222222-2222-2222-2222-222222222222"),
            "kept",
            _library,
            "Episode",
            TimeSpan.FromHours(1),
            true,
            Array.Empty<string>(),
            _epoch);

        var silent = new ItemDescription(
            new Guid("33333333-3333-3333-3333-333333333333"),
            "no audio",
            _library,
            "Episode",
            TimeSpan.FromHours(9),
            false,
            Array.Empty<string>(),
            _epoch);

        var report = Perform(new StubBackend(), new[] { withAudio, silent });

        Assert.Equal(1, report.Items);
        Assert.Equal(TimeSpan.FromHours(1), report.TotalDuration);
        Assert.Equal(PcmAudio.SmallestSizeFor(TimeSpan.FromHours(1)), report.PeakTemporaryAudioBytes);
    }

    private static DryRunReport Perform(
        ITranscriptionBackend backend,
        IReadOnlyList<ItemDescription> items,
        string backendName = "Local",
        string model = "ggml-tiny.bin",
        int itemsAtOnce = 1,
        int threadsPerItem = 4,
        CalibrationLedger? calibration = null) =>
        DryRun.Perform(
            items,
            new SelectionOptions(
                new[] { _library },
                new[] { "Episode" },
                "en",
                null,
                null),
            new SettingsInForce(
                1,
                backendName,
                "en",
                new Dictionary<Guid, string>(),
                itemsAtOnce,
                threadsPerItem,
                ConfigurationValidation.NoPathNamed,
                ConfigurationValidation.NoPathNamed),
            backend,
            model,
            calibration ?? new CalibrationLedger());

    private static ItemDescription[] Items(params TimeSpan[] durations) =>
        durations
            .Select((duration, index) => new ItemDescription(
                DeterministicId(index),
                "item " + index.ToString(CultureInfo.InvariantCulture),
                _library,
                "Episode",
                duration,
                true,
                Array.Empty<string>(),
                _epoch))
            .ToArray();

    private static Guid DeterministicId(int index)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)(index + 1);

        return new Guid(bytes);
    }
}
