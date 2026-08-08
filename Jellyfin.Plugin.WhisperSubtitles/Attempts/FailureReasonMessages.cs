namespace Jellyfin.Plugin.WhisperSubtitles.Attempts;

/// <summary>
/// The sentence an operator is shown for each way an attempt can end.
/// </summary>
/// <remarks>
/// A distinct line per reason, and the point of holding them in one place is that
/// distinctness is then something a test can check. Two modes sharing a sentence is
/// the same defect as two modes sharing a value: the vocabulary looks precise in
/// the code and arrives at the operator as one message they cannot act on.
///
/// The lines say what happened and never what to do about it. A sentence carrying
/// advice is a sentence that goes stale against the settings it advises about, and
/// #39 is where a run's report decides what to put in front of somebody.
/// </remarks>
public static class FailureReasonMessages
{
    /// <summary>
    /// What to tell an operator about one ending.
    /// </summary>
    /// <param name="reason">How the attempt ended.</param>
    /// <returns>One line, with no trailing punctuation and no advice in it.</returns>
    /// <remarks>
    /// Every value is named and there is no fallback arm. That is the refusal: a
    /// reason added to the vocabulary without a sentence leaves this switch
    /// incomplete, which is a warning this tree builds as an error, so the build
    /// fails rather than an operator being shown an enum name or a blank.
    /// </remarks>
    // CS8524 is the compiler pointing out that a value cast in from outside the
    // vocabulary is not handled, and it is not handled on purpose: such a value
    // throws here rather than taking the answer of whichever arm a fallback would
    // have been. CS8509, which fires for a NAMED value with no arm, stays on, and
    // that is the refusal this shape exists for - a mode added to the vocabulary
    // without a decision here fails the build.
#pragma warning disable CS8524
    public static string For(TranscriptionFailureReason reason) => reason switch
    {
        TranscriptionFailureReason.Cancelled =>
            "the run was stopped before this item finished",
        TranscriptionFailureReason.BackendNotReady =>
            "the configured backend was not usable when this item came up",
        TranscriptionFailureReason.BackendUnreachable =>
            "the backend could not be reached or could not be started",
        TranscriptionFailureReason.BackendFailed =>
            "the backend ran and reported a failure",
        TranscriptionFailureReason.NoAudioStream =>
            "this item has no audio stream to transcribe",
        TranscriptionFailureReason.AudioUnreadable =>
            "this item has audio that could not be read or decoded",
        TranscriptionFailureReason.OutputUnparseable =>
            "the backend produced output that could not be read as timed segments",
        TranscriptionFailureReason.AudioIsSilent =>
            "the audio in this item is silent or too quiet to hold speech",
        TranscriptionFailureReason.AudioHasNoSpeech =>
            "the audio in this item carries music or noise and no speech",
        TranscriptionFailureReason.AudioHasSeveralLanguages =>
            "the audio in this item carries more than one language, and one file names one language",
        TranscriptionFailureReason.DetectionBelowTheFloor =>
            "the detected language was less certain than the floor an operator set",
        TranscriptionFailureReason.NoSegments =>
            "the backend reported no failure and produced no segments",
        TranscriptionFailureReason.TimingsDoNotFitTheItem =>
            "the segments end after this item does, so they are not for this file"
    };
#pragma warning restore CS8524
}
