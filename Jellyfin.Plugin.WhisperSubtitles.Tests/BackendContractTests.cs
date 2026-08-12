using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Local;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// One suite every implementation of <see cref="ITranscriptionBackend"/> is
/// pointed at.
/// </summary>
/// <remarks>
/// Two backends written at the same time by the same hand agree with each other by
/// accident. This is what makes the agreement deliberate, and it is what a fourth
/// implementation is measured against rather than against whatever tests were
/// written beside it.
///
/// It names no concrete backend in a clause. The list below is the population, and
/// every clause is a function of the interface alone, so a case that needed a
/// clause bent around it would show up as a change to a clause rather than as one
/// more entry.
///
/// What each clause asserts is the weakest thing the interface actually promises,
/// which is why the do-nothing backend can sit in the same list as the two that
/// transcribe. A backend that refuses to work is not a backend that may answer
/// anything it likes: it owes a readiness answer with a reason, a cost hint that
/// behaves, and a declared failure rather than whatever exception happened to be
/// nearest.
///
/// WHAT IS NOT CHECKED HERE is the cancellation BUDGET. The interface states a
/// time within which a backend must stop, and measuring an elapsed time needs a
/// clock the suite refuses to read, so what is asserted is that a stopped backend
/// does not answer with a transcription. A backend that stops late passes this and
/// would fail the clause the interface actually writes.
/// </remarks>
public sealed class BackendContractTests : IDisposable
{
    private const string NoName = "no name";

    private const string ShrinkingCost = "shrinking cost";

    private const string TranscribingWhileCheckingReadiness = "transcribing while checking readiness";

    private const string UndeclaredException = "undeclared exception";

    private const string RemoteAnswer = """
        {
          "task": "transcribe",
          "language": "en",
          "duration": 4.5,
          "text": "A line somebody said. And another one.",
          "segments": [
            { "id": 0, "start": 0.0, "end": 2.25, "text": " A line somebody said." },
            { "id": 1, "start": 2.25, "end": 4.5, "text": " And another one." }
          ]
        }
        """;

    /// <summary>
    /// The clause each interface member is covered by, and the reason this list
    /// exists rather than a count. A member added to the interface with no clause
    /// is a member nothing in this suite says anything about, and it would arrive
    /// silently.
    /// </summary>
    private static readonly Dictionary<string, string> _clausePerMember = new(StringComparer.Ordinal)
    {
        ["get_Description"] = nameof(A_description_answers_before_anything_else_is_asked),
        ["CheckReadinessAsync"] = nameof(Readiness_answers_a_declared_shape_and_transcribes_nothing),
        ["EstimateCost"] = nameof(A_cost_hint_never_shrinks_as_the_media_gets_longer),
        ["TranscribeAsync"] = nameof(A_transcription_answers_with_ordered_segments_or_a_declared_failure)
    };

    private static readonly TimeSpan[] _ascendingDurations =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(45),
        TimeSpan.FromHours(3)
    ];

    private static readonly string[] _everyImplementation =
    [
        "stub",
        "not configured",
        "local",
        "remote"
    ];

    private static readonly string[] _threeCues =
    [
        "[00:00:00.000 --> 00:00:02.500]   The first thing said.",
        "[00:00:02.500 --> 00:00:05.000]   The second.",
        "[00:00:05.000 --> 00:00:07.250]   And the third."
    ];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    public BackendContractTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(AudioPath, Encoding.ASCII.GetBytes("not really audio"));
    }

    /// <summary>
    /// Gets every implementation this repository ships, plus the stub every other
    /// test drives. The do-nothing backend is here because it is what a fresh
    /// install uses and the one most likely to be left half written, since nothing
    /// it does looks like work.
    /// </summary>
    public static TheoryData<string> EveryImplementation => new(_everyImplementation);

    private string AudioPath => Path.Combine(_directory, "A Film.wav");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(EveryImplementation))]
    public void A_description_answers_before_anything_else_is_asked(string implementation) =>
        DescriptionClause(Build(implementation));

    [Theory]
    [MemberData(nameof(EveryImplementation))]
    public async Task Readiness_answers_a_declared_shape_and_transcribes_nothing(string implementation) =>
        await ReadinessClause(Build(implementation)).ConfigureAwait(true);

    [Theory]
    [MemberData(nameof(EveryImplementation))]
    public void A_cost_hint_never_shrinks_as_the_media_gets_longer(string implementation) =>
        CostClause(Build(implementation));

    [Theory]
    [MemberData(nameof(EveryImplementation))]
    public async Task A_transcription_answers_with_ordered_segments_or_a_declared_failure(string implementation) =>
        await TranscriptionClause(Build(implementation)).ConfigureAwait(true);

    [Theory]
    [MemberData(nameof(EveryImplementation))]
    public async Task A_backend_handed_a_stopped_token_does_not_answer_with_a_transcription(string implementation) =>
        await CancellationClause(Build(implementation)).ConfigureAwait(true);

    [Fact]
    public void Every_member_of_the_interface_is_named_by_a_clause_here()
    {
        // Methods rather than members, so a property counts once, through its
        // accessor, instead of twice under two names. A property added to the
        // interface still arrives here, as its getter.
        var members = typeof(ITranscriptionBackend)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .ToArray();

        Assert.NotEmpty(members);

        var uncovered = members.Where(name => !_clausePerMember.ContainsKey(name)).ToArray();

        Assert.True(
            uncovered.Length == 0,
            $"the interface has {string.Join(", ", uncovered)} and no clause here says anything about it");
    }

    [Fact]
    public async Task The_clause_about_segments_is_reached_rather_than_caught_past()
    {
        // The guard on the clause above. Every branch of it sits behind a call
        // that a backend is free to answer with a declared failure, so a suite
        // where nothing ever returned a transcription would run green over
        // assertions none of which executed. This says the population reached both
        // halves, and it names no backend to say it.
        var answered = 0;
        var refused = 0;
        var segments = 0;

        foreach (var implementation in _everyImplementation)
        {
            var outcome = await RunTranscription(Build(implementation)).ConfigureAwait(true);

            if (outcome.Answered)
            {
                answered++;
                segments += outcome.Segments;
            }
            else
            {
                refused++;
            }
        }

        Assert.True(answered >= 3, $"only {answered} of the implementations answered with a transcription at all");
        Assert.True(refused >= 1, "no implementation answered with a declared failure, so that half of the clause never ran");
        Assert.True(segments > 0, "every answer was empty, so nothing was compared against the ordering rule");
    }

    [Fact]
    public async Task A_backend_reporting_progress_that_goes_backwards_fails_the_progress_clause_and_no_other()
    {
        var wrong = new BackendCase(
            "progress that goes backwards",
            new StubBackend("progress that goes backwards") { ProgressPattern = [0.5, 0.2] },
            () => 0);

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(
            () => TranscriptionClause(wrong)).ConfigureAwait(true);

        DescriptionClause(wrong);
        CostClause(wrong);
        await ReadinessClause(wrong).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(NoName, nameof(DescriptionClause))]
    [InlineData(ShrinkingCost, nameof(CostClause))]
    [InlineData(TranscribingWhileCheckingReadiness, nameof(ReadinessClause))]
    [InlineData(UndeclaredException, nameof(TranscriptionClause))]
    public async Task Each_remaining_clause_refuses_a_backend_that_breaks_it_and_no_other(
        string misbehaviour,
        string clause)
    {
        // One deliberately wrong backend per clause the two implementations above
        // do not reach. Each leg breaks one promise and asserts that exactly the
        // clause about that promise reddens, because a suite whose clauses all
        // redden together says a backend is broken and not where.
        var misbehaving = new StubBackend(misbehaviour)
        {
            DescribesWithNoName = misbehaviour == NoName,
            CostShrinksWithLength = misbehaviour == ShrinkingCost,
            TranscribesWhileCheckingReadiness = misbehaviour == TranscribingWhileCheckingReadiness,
            ThrowsSomethingUndeclared = misbehaviour == UndeclaredException
        };
        var wrong = new BackendCase(misbehaviour, misbehaving, () => misbehaving.TranscriptionsAsked);

        foreach (var candidate in new[]
                 {
                     nameof(DescriptionClause),
                     nameof(CostClause),
                     nameof(ReadinessClause),
                     nameof(TranscriptionClause)
                 })
        {
            var broke = await Broke(candidate, wrong).ConfigureAwait(true);

            Assert.True(
                broke == string.Equals(candidate, clause, StringComparison.Ordinal),
                broke
                    ? $"{misbehaviour} reddened {candidate}, which is not the clause it breaks"
                    : $"{misbehaviour} left {candidate} green, and that is the clause it breaks");
        }
    }

    private async Task<bool> Broke(string clause, BackendCase subject)
    {
        try
        {
            switch (clause)
            {
                case nameof(DescriptionClause):
                    DescriptionClause(subject);
                    break;
                case nameof(CostClause):
                    CostClause(subject);
                    break;
                case nameof(ReadinessClause):
                    await ReadinessClause(subject).ConfigureAwait(true);
                    break;
                default:
                    await TranscriptionClause(subject).ConfigureAwait(true);
                    break;
            }

            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception)
#pragma warning restore CA1031
        {
            // Every way a clause can refuse. An assertion is one of them and an
            // exception the interface never declared is the other: that one escapes
            // the clause rather than being caught inside it, which is the whole
            // point of the filter there, and to this helper it is the same verdict.
            return true;
        }
    }

    [Fact]
    public async Task A_backend_that_returns_overlapping_segments_fails_the_segment_clause_and_no_other()
    {
        // Deliberately wrong, and wrong in exactly one way. The value of it is the
        // "and no other": a suite whose clauses all redden together is a suite that
        // says a backend is broken without saying where.
        var wrong = new BackendCase(
            "overlapping",
            new StubBackend("overlapping")
            {
                Segments =
                [
                    new TimedSegment(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3), "first"),
                    new TimedSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), "second")
                ]
            },
            () => 0);

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(
            () => TranscriptionClause(wrong)).ConfigureAwait(true);

        DescriptionClause(wrong);
        CostClause(wrong);
        await ReadinessClause(wrong).ConfigureAwait(true);
        await CancellationClause(wrong).ConfigureAwait(true);
    }

    [Fact]
    public async Task A_backend_that_ignores_cancellation_fails_the_stopping_clause_and_no_other()
    {
        var wrong = new BackendCase(
            "deaf to the token",
            new StubBackend("deaf to the token") { ObservesCancellation = false },
            () => 0);

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(
            () => CancellationClause(wrong)).ConfigureAwait(true);

        DescriptionClause(wrong);
        CostClause(wrong);
        await ReadinessClause(wrong).ConfigureAwait(true);
        await TranscriptionClause(wrong).ConfigureAwait(true);
    }

    private static void DescriptionClause(BackendCase subject)
    {
        var description = subject.Backend.Description;

        Assert.NotNull(description);
        Assert.False(string.IsNullOrWhiteSpace(description.Name), $"{subject.Name} has a description with no name");
        Assert.NotNull(description.Models);
        Assert.NotNull(description.Languages);
        Assert.True(
            description.CancellationBudget >= TimeSpan.Zero,
            $"{subject.Name} states a cancellation budget of {description.CancellationBudget}");
    }

    private static void CostClause(BackendCase subject)
    {
        var estimates = _ascendingDurations
            .Select(subject.Backend.EstimateCost)
            .ToArray();

        Assert.All(estimates, estimate => Assert.NotNull(estimate));
        Assert.All(
            estimates,
            estimate => Assert.True(
                estimate.Longest >= estimate.Shortest,
                $"{subject.Name} hints at {estimate.Shortest} to {estimate.Longest}, which is backwards"));

        for (var later = 1; later < estimates.Length; later++)
        {
            Assert.True(
                estimates[later].Shortest >= estimates[later - 1].Shortest
                && estimates[later].Longest >= estimates[later - 1].Longest,
                $"{subject.Name} hints at less work for {_ascendingDurations[later]} than for {_ascendingDurations[later - 1]}");
        }
    }

    private static async Task ReadinessClause(BackendCase subject)
    {
        var readiness = await subject.Backend.CheckReadinessAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.NotNull(readiness);

        if (!readiness.IsReady)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(readiness.Reason),
                $"{subject.Name} answers that it is not ready and says nothing about why");
        }

        Assert.Equal(0, subject.WorkObserved());
    }

    private async Task TranscriptionClause(BackendCase subject) =>
        await RunTranscription(subject).ConfigureAwait(true);

    private async Task<(bool Answered, int Segments)> RunTranscription(BackendCase subject)
    {
        var progress = new RecordingProgress();
        var answered = false;
        var count = 0;

        try
        {
            var result = await subject.Backend
                .TranscribeAsync(new TranscriptionRequest(AudioPath, "en"), progress, CancellationToken.None)
                .ConfigureAwait(true);

            Assert.NotNull(result);
            Assert.NotNull(result.Segments);
            Assert.False(string.IsNullOrWhiteSpace(result.Language), $"{subject.Name} answered without a language");

            var previousEnd = TimeSpan.MinValue;
            foreach (var segment in result.Segments)
            {
                Assert.True(segment.Start >= TimeSpan.Zero, $"{subject.Name} answered with a segment starting at {segment.Start}");
                Assert.True(segment.End >= segment.Start, $"{subject.Name} answered with a segment ending before it starts");
                Assert.True(
                    segment.Start >= previousEnd,
                    $"{subject.Name} answered with a segment at {segment.Start} that overlaps the one ending at {previousEnd}");
                previousEnd = segment.End;
            }

            answered = true;
            count = result.Segments.Count;
        }
        catch (Exception failure) when (IsDeclared(failure))
        {
            // A declared failure is an answer. What the interface refuses is an
            // exception it never named crossing the boundary, because the caller
            // has nothing to write in a report about one.
        }

        AssertProgressBehaved(subject, progress);

        return (answered, count);
    }

    private async Task CancellationClause(BackendCase subject)
    {
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync().ConfigureAwait(true);

        var progress = new RecordingProgress();

        try
        {
            var result = await subject.Backend
                .TranscribeAsync(new TranscriptionRequest(AudioPath, "en"), progress, stopped.Token)
                .ConfigureAwait(true);

            Assert.Fail($"{subject.Name} was handed a stopped token and answered with {result.Segments.Count} segment(s)");
        }
        catch (Exception failure) when (IsDeclared(failure))
        {
            // The token was observed, or the backend refused for a reason it
            // declares. Either is a backend that did not pretend to work.
        }

        AssertProgressBehaved(subject, progress);
    }

    private static void AssertProgressBehaved(BackendCase subject, RecordingProgress progress)
    {
        var highest = double.MinValue;
        foreach (var value in progress.Reported)
        {
            Assert.True(value >= 0 && value <= 1, $"{subject.Name} reported progress of {value}");
            Assert.True(value >= highest, $"{subject.Name} reported {value} after {highest}");
            highest = value;
        }
    }

    private static bool IsDeclared(Exception failure) =>
        failure is OperationCanceledException
        || failure is TranscriptionFailedException
        || failure is BackendNotConfiguredException;

    private BackendCase Build(string implementation)
    {
        switch (implementation)
        {
            case "stub":
                var stub = new StubBackend("stub");
                return new BackendCase(implementation, stub, () => stub.TranscriptionsAsked);

            case "not configured":
                return new BackendCase(implementation, new NotConfiguredBackend(), () => 0);

            case "local":
                var runner = ScriptedProcessRunner.Starting(ScriptedProcess.Printing(_threeCues));
                var tool = "/opt/whisper/whisper-cli";
                var model = "/var/lib/models/ggml-base.bin";
                return new BackendCase(
                    implementation,
                    new LocalWhisperBackend(
                        runner,
                        StubFileFacts.Empty().WithTool(tool).WithModel(model),
                        new LocalBackendOptions(tool, model)),
                    () => runner.Invocation is null ? 0 : 1);

            case "remote":
                var endpoint = StubEndpoint.Answering(HttpStatusCode.OK, RemoteAnswer);
                return new BackendCase(
                    implementation,
                    new RemoteWhisperBackend(
                        endpoint,
                        new RemoteBackendOptions("https://transcription.example", "sk-a-key", "a-model")),
                    () => endpoint.Transcriptions);

            default:
                throw new ArgumentOutOfRangeException(nameof(implementation), implementation, "no such backend in this suite");
        }
    }

    /// <summary>
    /// One implementation, with the one thing a clause needs that the interface
    /// does not offer: whether anything was actually asked to transcribe.
    /// </summary>
    /// <remarks>
    /// The interface cannot answer that, and the clause about readiness is
    /// worthless without it: a backend that quietly transcribed on the way to
    /// deciding whether it could would satisfy every other line. It is supplied
    /// beside the implementation rather than inside a clause, so no clause knows
    /// which backend it is judging.
    /// </remarks>
    private sealed class BackendCase
    {
        public BackendCase(string name, ITranscriptionBackend backend, Func<int> workObserved)
        {
            Name = name;
            Backend = backend;
            WorkObserved = workObserved;
        }

        public string Name { get; }

        public ITranscriptionBackend Backend { get; }

        public Func<int> WorkObserved { get; }
    }
}
