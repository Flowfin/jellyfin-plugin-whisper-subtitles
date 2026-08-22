# When an item did not get a subtitle

Every attempt that ends without a subtitle ends with one of the reasons below,
and the run reports that reason by name. This page takes each name in turn and
says what it means, what to check, and what changing it would do.

The names here are the values of `TranscriptionFailureReason`. A test asserts
that this page and that type carry the same set of names, in both directions, so
a reason cannot be added to the code without an entry here and an entry here
cannot outlive the reason it describes.

Several of the reasons are not failures of this plugin at all. An item with no
audio, audio a model cannot make words out of, or an endpoint that is reachable
and refusing the request all end a run the same way, and the action in each case
is somewhere other than this plugin's settings. Where that is so, the entry says
it.

Some of what this page tells an operator to look at is not built yet. The reason
type, the backends, the readiness probe and the scheduled task are in the tree.
What is not is the run the task performs, which is #183 and is what the counts
and the per item reasons below would come out of; the configuration page that
would show a readiness report, which is #36, and which the readiness clause of
#15 waits on; and the run summary that lists outcomes, which is #39. What holds today is the vocabulary and the
correspondence, which the test asserts. The rest is what the reasons will be
read against, and it is written here so that each reason arrives with its action
rather than acquiring one afterwards.

## The reason Cancelled

The run was stopped while this item was being worked on, either by the operator
or by the server shutting down. Nothing was learned about the item. It is not a
statement that the item cannot be transcribed, and it is not a defect.

An item that ends this way is left exactly as it was found. No subtitle is
written and no temporary file is kept.

### What to do

Run the task again. The item is picked up on the next run like any other item
without a subtitle.

If every item in a run ends this way and nobody stopped it, the server was
shutting down under the run. That is a scheduling question rather than a
transcription one: a trigger that fires close to a restart gives the run less
time than it needs, and moving the trigger is what changes it.

## The reason BackendNotReady

The backend named in the configuration exists and could not be used when the
item came up. For a local tool that is a missing or unreadable executable or
model file; for a remote endpoint it is a configuration that does not describe a
usable endpoint. The check happens before the item is sent anywhere, so nothing
was transcribed and nothing left the machine.

### What to do

Read what the backend says about itself. Each backend answers a readiness
question with a sentence naming what stands in the way, and that sentence is
what the configuration page is to show once #15 lands. It says which setting is
wrong, which is what makes it worth reading before the log.

For a local tool, check that the path points at a file that exists, that the
account the server runs as may execute it, and that the model file is where the
setting says it is. A path that is right on your own account and wrong for the
server account is the common case, and it is the one that looks like a working
configuration from a terminal.

Fixing the setting is enough. Nothing has to be cleaned up first, because an
item that failed this way was never started.

## The reason BackendUnreachable

The backend was configured well enough to try, and the attempt to reach it did
not get as far as a transcription. A remote endpoint did not answer, and a local
tool could not be started as a process.

This is separated from a backend that ran and failed because the two want
different actions. Nothing here says anything about the audio or about the model.

### What to do

For a remote endpoint, check that the host in the configured URL resolves and
answers from the server itself rather than from your own machine. A server
behind different firewall rules than your desktop reaches a different set of
hosts, and the plugin reports what the server saw.

For a local tool, check that the file at the configured path is executable on
this platform. A tool built for another architecture, or a file that is a script
whose interpreter is missing, fails at the same point as a file that is not
there at all.

Trying again unchanged is worth doing once, because a host that was briefly down
is the ordinary cause. Trying again repeatedly without changing anything is not.

## The reason BackendFailed

The backend ran, and it ended without producing a transcription, in a way it did
not describe as permanent. A remote endpoint that answered with a refusal lands
here, and so does a local tool that started and exited non-zero.

An endpoint that is reachable and rejecting the request is this reason and not
`BackendUnreachable`. The distinction is worth knowing when reading a log,
because a refusal usually names a cause the endpoint is willing to state, and
that cause is in the log line beside the reason.

### What to do

Read the message the backend gave, which the run carries next to the reason. A
rejected request from a remote endpoint is most often a model name the endpoint
does not have, a credential it did not accept, or a request larger than it
allows.

For a local tool, run the same tool by hand on a short file with the same model
and see what it prints. The plugin passes an argument list a test covers, so a
tool that fails by hand fails for a reason outside this plugin.

A model too small for the language it was pointed at can also end here, when the
tool refuses rather than producing poor text. The action is a larger model, and
the cost of it is in the operator guide.

## The reason NoAudioStream

The item has no audio stream, so there is nothing to transcribe. This is a fact
about the file and not a failure of anything.

### What to do

Nothing, for a file that genuinely has no audio. Silent film, a video with its
audio in a separate file the server has not been told about, and a placeholder
file all land here correctly.

If the item plays with sound in a client, the server's own probe of the file
disagrees with what the client does, and that is a server question rather than a
plugin one. The item's media information page is where the server says which
streams it found.

## The reason AudioUnreadable

The item has an audio stream and it could not be read or decoded into something
a transcription can be made from. The file is damaged, truncated, or in a
container or codec the server's own media tool did not handle here.

### What to do

Play the item in a client and listen. A file that will not play is a library
problem this plugin has surfaced rather than caused, and replacing the file is
the repair.

A file that plays and still fails here is worth reporting, with the container
and codec the server lists for it. Nothing about this reason is affected by the
model or by the backend, so changing either will not move it.

## The reason OutputUnparseable

The backend ran, said it succeeded, and what it produced could not be read as
timed segments. The output was not in the shape this plugin expects, so nothing
was written rather than something being guessed at.

Nothing partial reaches the library from this reason. The parse happens before
anything is written, so an unparseable transcription leaves no file behind.
Nothing partial reaches the library from a write that is interrupted either, and
that is a different guarantee held by #27.

### What to do

Check that the tool at the configured path is the tool the configuration thinks
it is, and that the version is one this plugin was built against. A tool that
changed its output format between versions produces exactly this.

For a remote endpoint, check that the URL points at a transcription endpoint and
not at a proxy or a login page in front of one. An endpoint that answers with
something other than a transcription answers successfully, and the failure only
becomes visible at the point where the answer is read.

If both are right, this is worth reporting, and the report is much more useful
with the tool version in it.

## Audio with no speech in it, and a machine slower than the estimate

Neither of these produces a reason, because neither is a failure.

Audio that is music, applause or noise gives a model nothing to make words out
of. What comes back is an empty transcription or a short one that has little to
do with the item, and the plugin writes it, because it has no way to tell a
sparse transcription of a quiet film from a wrong transcription of a loud one.
The way to find these is to look at what was produced for items you know are not
speech, and the way to avoid them is a selection that does not include those
items.

A run that takes far longer than the estimate said is an estimate that was
measured on a different machine, not a fault. An estimate is required to say
what it was measured on and when, which is #37's ground, so the first thing to
compare is that machine against yours. Fewer cores, a slower disk, or other work
running at the same time all take longer, and a run that is slow is still a run
that will finish.

## The reason AudioIsSilent

The audio is there and holds nothing to transcribe: silence, or a level so low
that there is no speech in it. This is a fact about the file and the backend
behaved.

It is separate from NoSegments on purpose. That one says the backend examined
nothing; this one says it examined silence.

### What to do

Nothing, for a file that is genuinely silent. A recording whose microphone was
off, a title sequence extracted on its own, and a badly remuxed item where the
audio track carries no samples all land here correctly.

If the item plays with sound in a client, the audio the extraction step produced
is not the audio you can hear, and the thing to compare is which stream was
chosen against which one the client plays. An item with several audio streams is
where those differ.

## The reason AudioHasNoSpeech

The audio carries music or noise and no speech. Nothing is wrong and there is
nothing to write.

### What to do

Nothing, for a concert recording, a nature film with no narration, or anything
else whose soundtrack is not somebody talking. No subtitle is written, which is
the correct outcome: an empty subtitle track looks exactly like the work was
done, so the item would be skipped by anybody later looking for what is left to
transcribe.

If the item does carry speech, what to look at is the audio stream that was
chosen rather than any setting here.

## The reason AudioHasSeveralLanguages

The audio carries more than one language. One subtitle file names one language,
so a transcription of a bilingual recording would be a file that is wrong about
most of itself, and it is refused rather than written under whichever language
won.

### What to do

Transcribe such an item by hand, or leave it. There is no setting that makes this
correct, because the shape of the output is what cannot hold the answer.

A film with subtitled foreign passages inside a single spoken language is not
this case and does not land here.

## The reason DetectionBelowTheFloor

Detection returned a language and returned it less certainly than the floor an
operator set. The run reports this rather than writing a subtitle named in a
language the backend was guessing at.

### What to do

Two settings decide it and they pull in opposite directions. Lowering the floor
accepts less certain answers, which is right for a library of clean recordings in
one language and wrong for a mixed one. Naming the language for that library
instead removes the question, and it is the better answer wherever the library
actually is one language.

A larger model detects more confidently on the same audio, so a floor that
refuses everything on the smallest model may hold on a larger one.

The floor itself is #31 and the per-library target is #30.

## The reason NoSegments

The backend ran, reported no failure, and produced no segments. It is not
silence and it is not music: it is the backend having examined nothing.

A model path that names a file the tool could not load, a tool that exited nought
having written nothing, and an endpoint that answered with an empty body all land
here.

### What to do

Look at the log lines around the failure, which carry what the backend printed.
Then check the configured model path against the file that is actually there, and
the readiness report for the backend, which is #15.

If every item in a run ends this way, the backend is not working rather than the
items being unusual, and one item run on its own with the log open is the
shortest way to see it.

## The reason TimingsDoNotFitTheItem

The segments end after the item does, by more than a two second tolerance, so
they are not a transcription of this file. This is the check that catches a
backend pointed at the wrong audio: what it produced is well formed, and the only
thing about it that does not fit is that it runs past the end.

No file is written, which is the point. A subtitle that drifts further out of
step the longer an item plays is a defect nobody reports as a plugin problem.

### What to do

Look at the log lines around the failure for the audio file that was handed to
the backend, and at whether more than one item was in flight at the time. Then
run the item on its own.

The tolerance is not a setting. It exists because a library's duration for an
item and the length a decoder produces from it differ by a frame or two
routinely, and it is far below the drift this reason exists to catch.

## What to attach when reporting a defect

Five things, and they are enough for almost every report.

The server line, which is 10.11 or 12.0. The backend that was selected. The
model that was configured. The reason name from the list above, spelled as the
run spelled it. And the log lines around the failure, which carry the message
the backend gave.

Never attach a configured key. A backend in this tree takes one and sends it as a
bearer header, in
`Jellyfin.Plugin.WhisperSubtitles/Backends/Remote/RemoteWhisperBackend.cs`, so a
key in what you are about to send may be the one you configured here rather than
somebody else's.

The rule that key carries is that it reaches no log line, no error message and no
page. What holds today is the backend half: `RemoteWhisperBackendTests` asserts
the key reaches no message that backend produces on any failure path, including
a refusal that echoes the key back. Checking the rule once for the whole plugin
rather than at each logger is #73, and it is not built, because nothing in this
plugin logs yet. So the half a reporter is relying on is the half that is
asserted, and the half that would cover a logger nobody has written is the half
that is owed.
