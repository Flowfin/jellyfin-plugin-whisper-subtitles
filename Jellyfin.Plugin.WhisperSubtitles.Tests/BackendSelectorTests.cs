using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Selection is where a configuration that cannot be honoured turns into
/// behaviour, and every branch of it has to end in the same place: nothing is
/// transcribed, and an operator is told which value was the problem.
/// </summary>
public class BackendSelectorTests
{
    private static readonly IReadOnlyList<string> _nothingMissing = Array.Empty<string>();

    [Fact]
    public async Task A_configuration_that_names_nothing_falls_back()
    {
        var choice = await Select(null, Ready("local"));

        Assert.Equal(BackendSelectionOutcome.NothingConfigured, choice.Outcome);
        Assert.IsType<NotConfiguredBackend>(choice.Backend);
    }

    [Fact]
    public async Task A_blank_name_is_the_same_as_no_name()
    {
        // A configuration file edited by hand ends up holding "   " more often
        // than it ends up holding nothing at all.
        var choice = await Select("   ", Ready("local"));

        Assert.Equal(BackendSelectionOutcome.NothingConfigured, choice.Outcome);
    }

    [Fact]
    public async Task A_name_this_plugin_does_not_have_falls_back_and_says_which_name()
    {
        var choice = await Select("whisperx", Ready("local"), Ready("remote"));

        Assert.Equal(BackendSelectionOutcome.UnknownName, choice.Outcome);
        Assert.IsType<NotConfiguredBackend>(choice.Backend);
        Assert.Contains("whisperx", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_setting_falls_back_and_names_the_setting()
    {
        var choice = await Select(
            "local",
            new BackendCandidate("local", new StubBackend("local"), new[] { "ModelPath" }));

        Assert.Equal(BackendSelectionOutcome.MissingSetting, choice.Outcome);
        Assert.IsType<NotConfiguredBackend>(choice.Backend);
        Assert.Contains("ModelPath", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_setting_is_checked_before_the_machine_is()
    {
        // The cheap answer comes first, and it is also the better sentence: "you
        // have not filled in the model path" and "the model path names a file that
        // is not there" have different repairs.
        var backend = NotReady("local", "no model file at that path.");

        var choice = await Select("local", new BackendCandidate("local", backend, new[] { "ModelPath" }));

        Assert.Equal(BackendSelectionOutcome.MissingSetting, choice.Outcome);
        Assert.Equal(0, backend.ReadinessChecks);
    }

    [Fact]
    public async Task A_readiness_check_that_says_no_falls_back_and_carries_its_reason()
    {
        var choice = await Select(
            "local",
            new BackendCandidate("local", NotReady("local", "the model file is not readable."), _nothingMissing));

        Assert.Equal(BackendSelectionOutcome.NotReady, choice.Outcome);
        Assert.IsType<NotConfiguredBackend>(choice.Backend);
        Assert.Contains("the model file is not readable.", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_readiness_check_that_throws_falls_back_rather_than_escaping()
    {
        // A backend's readiness check is its own code and may do anything. An
        // exception out of selection would reach the scheduled task, which has no
        // better answer than this one.
        var choice = await Select(
            "local",
            new BackendCandidate(
                "local",
                new StubBackend("local") { ReadinessThrows = new InvalidOperationException("readiness exploded") },
                _nothingMissing));

        Assert.Equal(BackendSelectionOutcome.NotReady, choice.Outcome);
        Assert.IsType<NotConfiguredBackend>(choice.Backend);
        Assert.Contains("readiness exploded", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_backend_that_is_there_and_ready_is_the_one_returned()
    {
        var candidate = Ready("local");

        var choice = await Select("local", candidate);

        Assert.Equal(BackendSelectionOutcome.Selected, choice.Outcome);
        Assert.Same(candidate.Backend, choice.Backend);
    }

    [Fact]
    public async Task The_name_is_matched_the_way_a_person_types_it()
    {
        var candidate = Ready("Local");

        var choice = await Select("  local ", candidate);

        Assert.Equal(BackendSelectionOutcome.Selected, choice.Outcome);
        Assert.Same(candidate.Backend, choice.Backend);
    }

    [Fact]
    public async Task An_unusable_choice_is_never_replaced_by_a_working_one()
    {
        // The rule this whole type exists for. An operator who configured a model
        // on their own machine and silently got a remote endpoint has had a
        // decision made for them about where their audio goes.
        var working = Ready("remote");

        var choices = new[]
        {
            await Select("local", Broken("local"), working),
            await Select("nonesuch", working),
            await Select("local", new BackendCandidate("local", new StubBackend("local"), new[] { "ModelPath" }), working)
        };

        Assert.All(choices, c => Assert.IsType<NotConfiguredBackend>(c.Backend));
        Assert.All(choices, c => Assert.NotSame(working.Backend, c.Backend));
    }

    [Fact]
    public async Task Every_way_of_failing_gives_its_own_outcome_and_its_own_sentence()
    {
        // "Distinct" is the done-condition, so it is asserted as distinctness
        // rather than as four separate string comparisons that could all be the
        // same string.
        var results = new[]
        {
            await Select(null, Ready("local")),
            await Select("nonesuch", Ready("local")),
            await Select("local", new BackendCandidate("local", new StubBackend("local"), new[] { "ModelPath" })),
            await Select("local", Broken("local"))
        };

        Assert.Equal(4, results.Select(r => r.Outcome).Distinct().Count());
        Assert.Equal(4, results.Select(r => r.Reason).Distinct(StringComparer.Ordinal).Count());
        Assert.All(results, r => Assert.NotEqual(BackendSelectionOutcome.Selected, r.Outcome));
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Reason)));
    }

    [Fact]
    public async Task Cancellation_is_the_caller_stopping_and_not_a_backend_failing()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BackendSelector.SelectAsync(
                "local",
                new[] { new BackendCandidate("local", new StubBackend("local"), _nothingMissing) },
                cancelled.Token));
    }

    private static Task<BackendChoice> Select(string? configuredName, params BackendCandidate[] candidates) =>
        BackendSelector.SelectAsync(configuredName, candidates, CancellationToken.None);

    private static BackendCandidate Ready(string name) =>
        new(name, new StubBackend(name), _nothingMissing);

    private static BackendCandidate Broken(string name) =>
        new(name, NotReady(name, "it is not ready."), _nothingMissing);

    private static StubBackend NotReady(string name, string reason) =>
        new(name) { IsReady = false, NotReadyReason = reason };
}
