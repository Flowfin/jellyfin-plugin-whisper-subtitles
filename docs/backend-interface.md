# Adding a transcription backend

This plugin does no transcription of its own. Everything heavy sits behind
`ITranscriptionBackend`, and the point of that is that a third backend can be
added without the scheduled task, the output writer, the naming, the language
handling or the configuration page changing.

That is only true if what a new backend has to satisfy is written down. This page
is that, and it is written for somebody who has not read the rest of the code.

## Where a backend lives

Under `Jellyfin.Plugin.WhisperSubtitles/Backends/`. Nothing outside that folder
may name a concrete backend in a signature, and `BackendInterfaceTests` refuses
it. What the rest of the plugin sees is the interface and nothing else.

## What the interface promises, member by member

The interface returns timed segments rather than a formatted subtitle file.
Formatting, naming, the machine-made marking and the language a file is named
under belong to this plugin and must not differ between backends, so a backend
that produced a finished file would be deciding four things that are not its to
decide.

**`Description`** says what the backend offers: its name, the models and languages
it can be asked for, whether it can detect a language, whether it can put a
confidence on that detection, and the time within which it stops when it is told
to. Answering must not start a transcription and must not require the backend to
be usable. A configuration page asks a backend that is not configured what it
offers, and it has to get an answer.

**`CheckReadinessAsync`** answers whether the backend can transcribe right now,
and where it cannot, why not. It transcribes nothing. An administrator surface
asks it whenever it likes, so a readiness check that quietly did the work would
turn a page refresh into a machine's afternoon.

Where the answer is that the backend is not ready, it carries a reason a person
can act on. "Not ready" with nothing else is a dead end for whoever is reading it.

A ready answer is a statement about one moment and a narrow one. What the two
backends that do work check is that the things an operator named are there: files
at the configured paths for the local one, a host that answers and does not refuse
the key for the remote one. Neither transcribes, so neither shows that the tool
runs, that the model loads, that the endpoint serves the configured model, or that
any of it is still true when a run starts an hour later. A backend states its own
bound in its remarks rather than leaving a caller to read a ready answer as a
guarantee, and each of them is bounded in time by a deadline it carries, because
what waits for the answer is a page somebody is sitting in front of.

**`EstimateCost`** turns a media duration into the wall-clock range the backend
expects to need. It is a hint and not a promise: it exists so a dry run can say
what a library will cost before an operator commits a machine to it. It must not
shrink as the media gets longer.

**`TranscribeAsync`** does the work. It reports progress as a fraction between
nought and one that never goes backwards, and it answers with segments in order,
each starting at or after nought, each ending at or after it starts, and none
overlapping the one before it. The language it answers with is the language the
segments are in, which is not always the language that was asked for.

## What a backend may assume about the audio

One file, on the local file system, that this plugin extracted before calling.
It is linear PCM in a WAV container, sixteen thousand samples a second, one
channel, sixteen bits a sample. Those numbers are in `PcmAudio` and are read from
there rather than repeated in a backend.

Nothing else about it is guaranteed. It may be silent, it may be speech in a
language nobody asked for, it may be an hour of applause, and it may have been
stopped at a size ceiling. A backend is not the thing that decides what any of
that means: it transcribes, or it reports why it could not, and the plugin
decides what to do about the answer.

The file is the plugin's to delete. A backend does not remove it, move it or
write beside it.

## Failure is reported, not thrown

Every way a transcription can fail has a name in `TranscriptionFailureReason`, and
a backend reports one of those names by throwing `TranscriptionFailedException`
with it. Nothing else may cross the boundary except `OperationCanceledException`,
which is what a stop looks like.

The reason a caller can do nothing with an arbitrary exception is that the run has
to write a line about the item, decide whether to try again, and tell an operator
what to look at. `docs/troubleshooting.md` is the page it points them to, and a
test compares that page against the reason type in both directions, so the names
are not restated here.

## Cancellation

Part of the contract rather than a courtesy. The token arrives at
`TranscribeAsync` and the backend stops within the time its own `Description`
states. A backend that cannot stop within that time does not satisfy the
interface.

Stopping means the work stops, not that the token was noticed. A child process is
killed. An HTTP request is aborted. Whatever the backend created is gone.
`LocalWhisperBackend` is the worked example: `Kill` sits on `IStartedProcess`
rather than inside a runner, precisely so a test can see whether it was asked.

## Reaching outside

Whatever a backend reaches out to goes through an injected seam, so no test needs
a model, a binary or a network. `IProcessRunner` is the seam for a child process
and `LocalWhisperBackend` shows a backend driven through it; the remote backend
takes an `HttpMessageHandler` and `RemoteWhisperBackendTests` drives it through a
stub endpoint. A backend that constructs its own process or its own HTTP client
cannot be tested, and this suite does not accept one that cannot.

## The tests a new backend has to pass

`BackendContractTests`. Add the backend to the list at the top of it and every
clause runs against it. What each clause checks is in the suite, and it is not
restated here, because a description of a check drifts against the check.

Two things it deliberately does not do. It does not time the cancellation budget,
because measuring an elapsed time needs a clock the suite refuses to read, so a
backend that stops late passes it. And it holds only what every backend owes: a
backend still needs its own tests for what it does differently, in the shape
`LocalWhisperBackendTests` and `RemoteWhisperBackendTests` already use.

## Two shapes worth checking this against

Neither is built, and both are here because a contract checked against two
implementations written together is a contract checked against one habit.

An endpoint speaking the Wyoming protocol, which is a different transport with the
same shape of work. What it would test is whether the interface's assumptions
about a request and a response survive a protocol that is not HTTP with a JSON
body.

A service exposing something other than the audio transcription request this
plugin already speaks, where the response is not segments at all and has to be
turned into them. That is the case that would say whether the segment vocabulary
here is the interface's or one endpoint's.
