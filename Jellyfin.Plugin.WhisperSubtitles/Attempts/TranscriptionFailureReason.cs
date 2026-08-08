namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// Why one attempt at one item ended without a subtitle.
/// </summary>
/// <remarks>
/// The ways transcription goes wrong are not one failure, and collapsing them into
/// one is what makes a run impossible to debug and impossible to trust. An
/// operator looking at a hundred failed items needs to know which of them are a
/// misconfigured endpoint, which are files with no audio, and which are the
/// backend transcribing the wrong thing, because those are three different
/// evenings of work and one of them is none.
///
/// Every value here answers three questions, and each answer is held somewhere a
/// missing one is refused rather than defaulted. What an operator is told is
/// <see cref="FailureReasonMessages"/>. Whether trying again could end differently
/// is <see cref="RetryPolicy.IsRetryable"/>, whose switch names every value with
/// no fallback arm, so a value added here without a decision fails the build.
/// Whether anything is written is <see cref="TranscriptionOutcome"/>, which
/// carries no segments for any of these.
/// </remarks>
public enum TranscriptionFailureReason
{
    /// <summary>
    /// The operator stopped the run, or the server did. Nothing was learned about
    /// the item.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The configured backend was not usable when the item came up.
    /// </summary>
    BackendNotReady,

    /// <summary>
    /// The remote endpoint could not be reached, or the local tool could not be
    /// started.
    /// </summary>
    BackendUnreachable,

    /// <summary>
    /// The backend ran and failed, for a reason it did not describe as permanent.
    /// </summary>
    BackendFailed,

    /// <summary>
    /// The item has no audio stream, so there is nothing to transcribe.
    /// </summary>
    NoAudioStream,

    /// <summary>
    /// The audio is there and cannot be read or decoded.
    /// </summary>
    AudioUnreadable,

    /// <summary>
    /// The backend produced output this plugin could not parse into segments.
    /// </summary>
    OutputUnparseable,

    /// <summary>
    /// The audio is silent, or so quiet that there is nothing in it to transcribe.
    /// </summary>
    /// <remarks>
    /// Distinct from producing no segments, because they mean different things to
    /// the operator: this one says the file is the problem and the backend behaved,
    /// and it is the state a badly remuxed item arrives in.
    /// </remarks>
    AudioIsSilent,

    /// <summary>
    /// The audio carries music or noise and no speech.
    /// </summary>
    /// <remarks>
    /// A concert recording, a title sequence extracted on its own, a nature film
    /// with no narration. Nothing is wrong and there is nothing to write, and an
    /// operator told this does not go looking for a broken backend.
    /// </remarks>
    AudioHasNoSpeech,

    /// <summary>
    /// The audio carries more than one language.
    /// </summary>
    /// <remarks>
    /// One subtitle file names one language, so a transcription of a bilingual
    /// recording is a file that lies about most of itself. Refused rather than
    /// written under whichever language won.
    /// </remarks>
    AudioHasSeveralLanguages,

    /// <summary>
    /// Detection returned a language, and returned it with less confidence than the
    /// operator set as a floor.
    /// </summary>
    /// <remarks>
    /// The floor itself and the number that is compared against it are #31. What is
    /// here is the outcome, so a run reports this rather than reporting a subtitle
    /// in a language the backend was guessing at.
    /// </remarks>
    DetectionBelowTheFloor,

    /// <summary>
    /// The backend ran, said nothing failed, and produced no segments.
    /// </summary>
    /// <remarks>
    /// Distinct from silence and from music, because it says the audio was not
    /// examined rather than that it held nothing: a wrong model path, a tool that
    /// exited nought having written nothing, an endpoint answering with an empty
    /// body.
    /// </remarks>
    NoSegments,

    /// <summary>
    /// The segments do not fit the item they are supposed to be for.
    /// </summary>
    /// <remarks>
    /// The last segment ends after the item does, by more than a tolerance. This is
    /// the one that catches a backend transcribing the wrong file, and a run that
    /// wrote such a file would leave subtitles drifting further out of step the
    /// longer the item plays, which is a defect nobody reports as a plugin problem.
    /// </remarks>
    TimingsDoNotFitTheItem
}
