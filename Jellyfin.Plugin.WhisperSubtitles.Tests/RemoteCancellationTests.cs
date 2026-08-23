using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Attempts;
using Jellyfin.Plugin.WhisperSubtitles.Backends;
using Jellyfin.Plugin.WhisperSubtitles.Backends.Remote;
using Xunit;

namespace Jellyfin.Plugin.WhisperSubtitles.Tests;

/// <summary>
/// Cancelling a transcription has to reach the request, not only the method waiting
/// on it. The local backend's half of that is held by a double that records whether
/// the tool was asked to stop; this is the same separation on the remote side, where
/// what has to end is a send that is already out.
/// </summary>
/// <remarks>
/// THE TWO STATES LOOK IDENTICAL FROM THE CALLER AND ARE NOT THE SAME THING. A
/// backend that noticed the token and threw leaves the request outstanding: the
/// endpoint keeps reading the body, the connection stays up, and the audio of the
/// item an operator just stopped carries on leaving the machine. A backend that
/// aborted the send leaves nothing running. Both raise
/// <see cref="OperationCanceledException"/> at the caller, so a test that asserts
/// only on the exception cannot tell them apart, and the one it accepts is the
/// weaker one.
///
/// WHAT WAS ALREADY PROVED, AND WHY IT IS NOT THIS. The contract suite's cancellation
/// clause hands a backend a token that was cancelled before the call, so no request
/// is ever in flight to abandon.
/// <c>An_operator_stopping_the_run_is_cancellation_and_not_a_timeout</c> in
/// <see cref="RemoteWhisperBackendTests"/> has its own stub raise the cancellation
/// and then throw, which proves the caller's token wins the tie against the request
/// deadline and says nothing about the send. Both are about the token being
/// OBSERVED, which the issue behind this class separates by name from the abort
/// happening.
///
/// HOW THE DIFFERENCE IS MADE VISIBLE. The handler is the seam. It reports when a
/// request has arrived and then waits on the token it was handed and on nothing
/// else, so the test can cancel at a moment when the send is genuinely outstanding.
/// What it records is that its own token fired while it was still inside the send.
/// A backend that observed the caller's token and returned without passing it down
/// leaves that flag false, whatever it raised at the caller.
///
/// The waiting is on the token rather than on a duration, which is the rule the
/// determinism scan holds this suite to and also what keeps the test from depending
/// on a machine being fast enough.
///
/// TWO THINGS HOLD THIS PROPERTY AND THE NEAR MISS HAD TO DEFEAT BOTH, which is worth
/// knowing before anybody edits either of them. The token reaches the send, and the
/// client the send was made on is disposed on every exit from the method, which
/// cancels whatever it still has outstanding. Taking the token off the send alone
/// leaves this class green, measured rather than reasoned about, and the run is in
/// the pull request that added it. So what is proved is the PROPERTY rather than one
/// mechanism for it, and a change that removes one of the two is not caught here. It
/// is also the reason the belt and the braces are both worth keeping: neither is
/// redundant while nothing distinguishes them.
///
/// WHAT THIS DOES NOT DO. It says nothing about the socket. The handler is where this
/// suite stops, by the same decision that keeps every test here offline, so what is
/// proved is that the abort reaches the seam the request goes through rather than
/// that a connection was torn down. It is also one clause of the issue behind it: the
/// run that has to be cancelled mid item, the temporary audio that must not remain
/// and the subtitle that must not appear all need a task that performs a run, and
/// this covers none of those.
/// </remarks>
public sealed class RemoteCancellationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "whisper-subtitles-tests-" + Guid.NewGuid().ToString("N"));

    public RemoteCancellationTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(AudioPath, Encoding.ASCII.GetBytes("not really audio"));
    }

    private string AudioPath => Path.Combine(_directory, "A Film.wav");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cancellation_abandons_the_request_rather_than_only_the_wait_for_it()
    {
        using var handler = new HoldsTheRequestOpen();
        using var stopping = new CancellationTokenSource();

        var backend = new RemoteWhisperBackend(handler, Options());

        var transcription = backend.TranscribeAsync(
            new TranscriptionRequest(AudioPath, "en"),
            new Progress<double>(),
            stopping.Token);

        await handler.Arrived.ConfigureAwait(true);

        Assert.False(handler.AbandonedInFlight, "the send was abandoned before anybody cancelled");

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transcription);

        Assert.True(
            handler.AbandonedInFlight,
            "the token was observed but the request was left running");
    }

    [Fact]
    public async Task A_request_nobody_stops_is_not_reported_as_abandoned()
    {
        // The neighbour, without which the leg above would pass on a handler that
        // called every send abandoned. This one is ended by the endpoint answering
        // rather than by anybody cancelling.
        using var handler = new HoldsTheRequestOpen();
        using var answering = new CancellationTokenSource();

        var backend = new RemoteWhisperBackend(handler, Options());

        var transcription = backend.TranscribeAsync(
            new TranscriptionRequest(AudioPath, "en"),
            new Progress<double>(),
            answering.Token);

        await handler.Arrived.ConfigureAwait(true);

        handler.Answer();

        var failed = await Assert.ThrowsAsync<TranscriptionFailedException>(() => transcription);

        Assert.Equal(TranscriptionFailureReason.OutputUnparseable, failed.Reason);
        Assert.False(handler.AbandonedInFlight, "an answered request was recorded as abandoned");
    }

    private static RemoteBackendOptions Options() =>
        new(
            "https://transcription.example",
            "sk-a-key-nobody-may-see",
            "a-model",
            TimeSpan.FromMinutes(10),
            RemoteBackendOptions.DefaultMaxResponseBytes);

    /// <summary>
    /// A handler that says when a request has reached it and then holds the send open
    /// until either the token it was given fires or the test answers.
    /// </summary>
    /// <remarks>
    /// The token it waits on is the one the backend passed down, so whether it fires
    /// is exactly the question this class is about. It is not the test's own token
    /// and it is not read from anywhere else.
    /// </remarks>
    private sealed class HoldsTheRequestOpen : HttpMessageHandler
    {
        private readonly TaskCompletionSource _arrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<HttpResponseMessage> _answer =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets a task that completes once a request is inside the send.
        /// </summary>
        public Task Arrived => _arrived.Task;

        /// <summary>
        /// Gets a value indicating whether the token this handler was given fired
        /// while a request was still out.
        /// </summary>
        public bool AbandonedInFlight { get; private set; }

        /// <summary>
        /// Lets the outstanding request finish, with an answer the backend refuses for
        /// a reason that has nothing to do with cancellation.
        /// </summary>
        public void Answer() =>
            _answer.TrySetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("not json", Encoding.UTF8, "text/plain"),
            });

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var abandons = cancellationToken.Register(() =>
            {
                AbandonedInFlight = true;
                _answer.TrySetCanceled(cancellationToken);
            });

            _arrived.TrySetResult();

            return await _answer.Task.ConfigureAwait(false);
        }
    }
}
