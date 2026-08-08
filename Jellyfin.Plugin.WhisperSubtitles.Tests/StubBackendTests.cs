using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// The stub is the only thing in this suite that stands where a model would, so
/// what it does is a shared assumption rather than one test's private business.
/// These hold it to the two properties that make it usable as one: every member
/// of the interface is reachable through its knobs, and it touches nothing
/// outside the process.
/// </summary>
public class StubBackendTests
{
    private const string StubSource = "Jellyfin.Plugin.WhisperSubtitles.Tests.StubBackend.cs";

    /// <summary>
    /// The names a stub could only carry if it opened a file or a socket. `File.`
    /// and `Directory.` keep their dot so that a field called AudioFilePath is not
    /// read as a file being opened.
    /// </summary>
    private static readonly string[] _reachesOutside =
    {
        "System.IO",
        "System.Net",
        "File.",
        "Directory.",
        "FileStream",
        "StreamReader",
        "StreamWriter",
        "HttpClient",
        "HttpRequestMessage",
        "Socket",
        "WebRequest",
        "Process",
        "Environment"
    };

    [Fact]
    public void The_suite_has_one_backend_it_transcribes_against()
    {
        // The property the single stub exists for, and the only way to keep it: a
        // fake hand-rolled inside a test file is invisible to review until two of
        // them disagree, and then both files look right.
        //
        // The fixture backends are exempt by namespace. They exist to be found by
        // the isolation check rather than to be transcribed against, and they
        // refuse to run at all.
        var handRolled = typeof(StubBackendTests).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITranscriptionBackend).IsAssignableFrom(t))
            .Where(t => t != typeof(StubBackend))
            .Where(t => t.Namespace is null
                || !t.Namespace.StartsWith("Jellyfin.Plugin.WhisperSubtitles.Tests.Fixtures", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToList();

        Assert.Empty(handRolled);
    }

    [Fact]
    public void The_stub_opens_no_file_and_no_socket()
    {
        // Read off the stub's own source, which is embedded in the test assembly so
        // the bytes judged are the bytes compiled rather than whatever a checkout
        // happens to hold beside it.
        //
        // What this decides is that the call is not written. It does not watch a
        // descriptor, and it could not: the stub is one self-contained type with no
        // dependency to reach through, so its source is the whole of what it can
        // do. A test that reaches out through something else is #48.
        var source = EmbeddedStubSource();

        var found = _reachesOutside
            .Where(name => source.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void The_check_above_would_notice_a_stub_that_opened_a_file()
    {
        // Without this the assertion above is a search for strings in a file nobody
        // proved was the right file, and an empty resource would pass it.
        Assert.Contains("StubBackend", EmbeddedStubSource(), StringComparison.Ordinal);

        var reaching = EmbeddedStubSource() + "\n// System.IO.File.ReadAllText(path);\n";

        Assert.Contains(_reachesOutside, name => reaching.Contains(name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_ready_stub_answers_every_member_without_being_told_anything()
    {
        var stub = new StubBackend();

        Assert.Equal("stub", stub.Description.Name);

        var readiness = await stub.CheckReadinessAsync(CancellationToken.None);
        Assert.True(readiness.IsReady);
        Assert.Null(readiness.Reason);
        Assert.Equal(1, stub.ReadinessChecks);

        Assert.Equal(TimeSpan.Zero, stub.EstimateCost(TimeSpan.FromMinutes(90)).Longest);

        var result = await Transcribe(stub);
        Assert.Equal("en", result.Language);
        Assert.Single(result.Segments);
        Assert.Equal(1, stub.TranscriptionsAsked);
    }

    [Fact]
    public async Task A_stub_told_it_is_not_ready_says_so_and_gives_the_reason()
    {
        var stub = new StubBackend { IsReady = false, NotReadyReason = "no model file at that path." };

        var readiness = await stub.CheckReadinessAsync(CancellationToken.None);

        Assert.False(readiness.IsReady);
        Assert.Equal("no model file at that path.", readiness.Reason);
    }

    [Fact]
    public async Task A_stub_told_to_throw_out_of_readiness_throws()
    {
        var stub = new StubBackend { ReadinessThrows = new InvalidOperationException("readiness exploded") };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => stub.CheckReadinessAsync(CancellationToken.None));

        Assert.Equal("readiness exploded", thrown.Message);
    }

    [Theory]
    [InlineData(TranscriptionFailureReason.Cancelled)]
    [InlineData(TranscriptionFailureReason.BackendNotReady)]
    [InlineData(TranscriptionFailureReason.BackendUnreachable)]
    [InlineData(TranscriptionFailureReason.BackendFailed)]
    [InlineData(TranscriptionFailureReason.NoAudioStream)]
    [InlineData(TranscriptionFailureReason.AudioUnreadable)]
    [InlineData(TranscriptionFailureReason.OutputUnparseable)]
    public async Task Every_typed_reason_can_be_asked_for(TranscriptionFailureReason reason)
    {
        // Named one by one rather than driven off Enum.GetValues, so a reason added
        // to the vocabulary shows up here as a value nothing asked the stub for
        // rather than as a case that silently joined a loop.
        var stub = new StubBackend { FailsWith = reason };

        var thrown = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(stub));

        Assert.Equal(reason, thrown.Reason);
    }

    [Fact]
    public async Task The_vocabulary_of_reasons_is_the_one_the_cases_above_name()
    {
        // The other half of naming them one by one. This fails when a reason is
        // added and no case was written for it.
        var asked = new HashSet<TranscriptionFailureReason>();

        foreach (var reason in Enum.GetValues<TranscriptionFailureReason>())
        {
            var stub = new StubBackend { FailsWith = reason };
            var thrown = await Assert.ThrowsAsync<TranscriptionFailedException>(() => Transcribe(stub));
            asked.Add(thrown.Reason);
        }

        Assert.Equal(7, asked.Count);
    }

    [Fact]
    public async Task A_stub_reports_the_progress_pattern_it_was_given()
    {
        var stub = new StubBackend { ProgressPattern = new[] { 0.25, 0.5, 1.0 } };
        var seen = new RecordingProgress();

        await stub.TranscribeAsync(
            new TranscriptionRequest("audio", "en"),
            seen,
            CancellationToken.None);

        Assert.Equal(new[] { 0.25, 0.5, 1.0 }, seen.Reported);
    }

    [Fact]
    public async Task A_held_transcription_finishes_only_once_the_test_releases_it()
    {
        // Slow without a clock. The gate is what the caller was going to observe
        // during a long transcription, and it closes at a moment the test chose.
        var gate = new TaskCompletionSource();
        var stub = new StubBackend { HeldUntilReleased = gate };

        var running = Transcribe(stub);

        Assert.False(running.IsCompleted);

        gate.SetResult();

        // ConfigureAwait(true) rather than nothing or false. CA2007 refuses a bare
        // await on a task held in a variable, and xUnit1030 refuses false inside a
        // test method because it escapes the runner's parallelism limit. True is
        // the one spelling both settings accept.
        var result = await running.ConfigureAwait(true);

        Assert.Single(result.Segments);
    }

    [Fact]
    public async Task A_stub_that_observes_cancellation_stops_and_one_that_does_not_finishes()
    {
        // Both halves in one test, because the pair is the point: a caller cannot
        // tell these apart by type, and every rule about cancellation is about the
        // second one.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new StubBackend().TranscribeAsync(
                new TranscriptionRequest("audio", null),
                new RecordingProgress(),
                cancelled.Token));

        var deaf = new StubBackend { ObservesCancellation = false };

        var result = await deaf.TranscribeAsync(
            new TranscriptionRequest("audio", null),
            new RecordingProgress(),
            cancelled.Token);

        Assert.Single(result.Segments);
    }

    [Fact]
    public async Task A_stub_records_what_it_was_asked_for()
    {
        var stub = new StubBackend();

        await stub.TranscribeAsync(
            new TranscriptionRequest("first", "de"),
            new RecordingProgress(),
            CancellationToken.None);
        await stub.TranscribeAsync(
            new TranscriptionRequest("second", null),
            new RecordingProgress(),
            CancellationToken.None);

        Assert.Equal(new[] { "first", "second" }, stub.AudioPathsAsked);
        Assert.Equal(new string?[] { "de", null }, stub.LanguagesAsked);
    }

    [Fact]
    public void A_stub_describes_itself_the_way_it_was_told_to()
    {
        var stub = new StubBackend("remote")
        {
            CanDetectLanguage = true,
            CancellationBudget = TimeSpan.FromSeconds(5)
        };

        Assert.Equal("remote", stub.Description.Name);
        Assert.True(stub.Description.CanDetectLanguage);
        Assert.Equal(TimeSpan.FromSeconds(5), stub.Description.CancellationBudget);
    }

    private static string EmbeddedStubSource()
    {
        using var stream = typeof(StubBackendTests).Assembly.GetManifestResourceStream(StubSource);

        Assert.True(stream is not null, $"the stub's source is not embedded under {StubSource}");

        using var reader = new System.IO.StreamReader(stream!);

        return reader.ReadToEnd();
    }

    private static Task<TranscriptionResult> Transcribe(StubBackend stub) =>
        stub.TranscribeAsync(
            new TranscriptionRequest("audio", "en"),
            new RecordingProgress(),
            CancellationToken.None);
}
