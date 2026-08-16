# What this plugin will not do

Most reports against a plugin like this one are not defects. They are things
somebody expected it to do, reasonably, because nothing said it would not. This
page is the list, and each entry names where the decision behind it was made, so
that a reader who disagrees can argue with the decision rather than with the
sentence.

Two states are distinguished throughout, because they are not the same promise.
A limit that is held by something in the tree today is one a machine refuses to
break. A limit that is a decision taken and not yet built is one a reader can
rely on for what this plugin will be, and not yet for what a running server
does.

## It transcribes, and it does not translate

What comes out is the words that were spoken, in the language they were spoken
in. Asking for a subtitle in a language other than the one on the audio is not
something this plugin does.

Held today. The backend interface has one language field, and its meaning is
fixed at the field: a language names what the audio is in, and its absence is a
request to detect that language rather than a request to convert it into
something else. The interface cannot express a translation, which is the point
of writing it that way. Recorded in #9 and readable in
`Backends/TranscriptionRequest.cs`.

Which language a library asks for is the operator's setting, in #30, and it is
still a statement about the audio.

## It carries no model and no inference runtime

The installable package holds this plugin's assembly and nothing else. No model
file, no native inference library, no media tool. A server that installs it does
not acquire several gigabytes it did not ask for, and it does not start
depending on a model being present.

Held today, by a check rather than by care. The package-contents check reads the
files a build produced and refuses a second assembly, a native library
extension, a model extension, or anything over its size ceiling, and it proves
it bites against a fixture carrying all three. Recorded in #16, and the check is
`.github/scripts/refuse-shipped-weight.sh`.

Whether the plugin may offer to download a model for the operator is a separate
question and it is open, in #8. What is settled is that nothing is downloaded
without being asked for and that the server never depends on a model existing.
Until that question is answered, an operator using the local backend places the
model file themselves and gives the plugin its path.

## It does not run the transcription inside the server process

Every backend is out of process: a child process on the same machine, or a
remote endpoint. A native fault inside an inference library therefore ends one
transcription rather than the media server, and killing a run reclaims its
memory because the memory belonged to another process.

Held today. The local backend drives a command line tool through an injected
process runner, in #12, and the package-contents check above refuses the shipped
runtime that an in-process design would need.

The cost is the one worth stating: progress inside a single item is only as fine
as what the backend reports, because the plugin is reading another process
rather than sitting inside the work.

## It writes one subtitle format

SubRip, with the `.srt` extension, and nothing else for the first release.

Held today. Recorded in #35, with the reasoning in `docs/subtitle-format.md`.
The writer takes cues and returns bytes behind an interface, so a second format
is a second implementation rather than a rewrite, which is why this is a limit
of the release and not of the design.

## It does not touch a subtitle that is already there

A file already sitting at the target path is never overwritten, truncated or
removed. The item is recorded as skipped, with the reason, and the run carries
on. A hand corrected subtitle is somebody's work and losing it would be the
worst thing this plugin could do.

Held today. Recorded in #28, and refused by `ExistingSubtitleTests`: a run over a
directory holding one hand corrected file leaves that file's bytes as they were,
the names left behind are one per item with no numbered variant beside the one
that was in the way, and the item is reported as `SkippedTargetExists` while the
run carries on. The file in the way is never opened for writing, which that suite
arranges by holding it open for reading with no write sharing, so a publish that
opened it to find out whether it could would fail on the file rather than on an
assertion about it. A file of nought bytes is in the way for the same reason a
full one is.

## It promises nothing about accuracy

What this plugin produces is machine transcription. How good it is depends on
the model, the language, the recording, the amount of background noise and the
number of people talking over each other, and those vary far more than any
sentence here could summarise. Poor audio produces a poor transcription, and
this plugin does not measure the difference or claim one.

Held today in what the plugin says about itself: the manifest description in
`build.yaml` states that it promises nothing about accuracy, which it does not
measure. Subtitles it writes are marked as machine made so a viewer can tell one
from a subtitle a person wrote, which is #26.

## It starts nothing on its own

Installing the plugin changes nothing about what the server does. The scheduled
task ships with no default trigger, so a server whose operator installed the
plugin and configured nothing behaves exactly as it did before, and there is a
task in the dashboard to look at.

Held today. `GetDefaultTriggers` on the task returns an empty set, and
`Nothing_this_task_ships_would_start_work_unattended` in
`SubtitleGenerationTaskTests` is red for a task that ships one trigger, so the
promise is refused rather than intended. Recorded in #17, which stays open for a
separate reason: whether the task appears in a server's dashboard is evidence only
a booted server produces, and that is #63.

## It waits for other work rather than asking other work to wait

A machine that is transcribing has very little left to give, so the interesting
collision between this plugin and another one is the machine rather than a name
either of them chose. The rule runs in one direction. This plugin waits for
another plugin's heavy task, and it never asks another plugin to wait for it.

The asymmetry is the decision and not a consequence of how it happens to be
written. A transcription that starts an hour later costs nobody anything they
can see. A library scan or a metadata refresh that runs an hour late is a delay
an operator does see, and this plugin is a guest on their server.

A run that stands aside says that is what it did. Reporting nothing to do is the
answer an operator gets when the selection is empty, and a run that found work
and declined to start it is a different answer to a different question.

Decided, not yet built, and the whole of it is decided. Nothing in the plugin
asks the server what it is running: no type here reaches `ITaskManager`, which
is the server's own answer to that question, so today a run neither yields nor
reports that it did. Recorded in #65, which holds the rule and the report it
owes. The place that answers whether the machine is busy is the seam in #22, and
the busy-server rule there watches transcoding rather than another plugin's
task, so the two differ in what they look at and not in what they do about it.

## It cannot tell you what happens to audio it sent away

Selecting the remote backend sends audio out of the server. Three facts, and the
third is the one worth reading twice.

What leaves the machine is the extracted audio of every item the run selects,
whole rather than sampled, as the body of one request per item.

Where it goes is the host in the URL the operator configured. This plugin
contacts nothing else, and the path under that URL is fixed rather than
configurable, so an operator who reads the URL knows the whole of where audio is
sent.

What this plugin cannot know is what the other end does with the audio after it
arrives. Whether it is written to disk there, kept after the transcript is
returned, logged, or used for anything else is a property of somebody else's
machine, and nothing this plugin can ask tells it. An operator who is answerable
to somebody else for that audio is answerable for this setting.

None of it applies to the local backend, which reads the audio on the same
machine and opens no socket.

Held today, in the sense that this is what the code does when the remote backend
is configured: `Backends/Remote/RemoteWhisperBackend.cs` posts the extracted
audio to the configured endpoint and reaches nothing else, and the key the
operator configures goes into one request header and into no message, URL or
form field, which `RemoteWhisperBackendTests` holds. Decided and not yet built is
the other half, which is the same three facts stated to the operator before the
backend can be selected and in the log line written when a run first uses it.
That is #81, and the page it belongs on is #36.

## What it writes, and where

Three kinds of thing, and no fourth.

The subtitle file, next to the media file or in the item's metadata folder,
following the library's own setting for where the server saves subtitles. Which
one applies when the library expresses no preference is open in #8; #25 holds
the writing itself, and #27 holds the rule that a file becomes visible only once
it is complete.

Temporary audio, extracted from the item so a backend has something to work
from, in a directory this plugin owns. It is deleted on every exit path, and
`AudioExtractorTests` holds that for a clean run, for a non-zero exit and for a
cancellation. What a dead process orphaned is collected by a sweep rather than by
a handler that may never run, and that half is decided and not yet built:
`TemporaryAudioSweep` is the sweep, and nothing calls it at the start of a run
because a run has no items for it to be before. #11 and #21.

Its own configuration and its record of what it produced, where the server puts
plugin data. #42.

Nothing else. #42 asks for a test that a full run writes nowhere outside that
list, which is what turns this paragraph from a description into a limit.

## What removing the plugin does not delete

Removing the plugin removes its configuration and its records, because those are
plugin data and the server removes plugin data. Temporary audio is already gone,
because it never survives a run.

Generated subtitle files stay on disk. They are in the operator's library, and
deleting a viewer's subtitles as a side effect of uninstalling a plugin would be
wrong. An operator who does want them gone gets a surface that lists what it
would remove before removing it, and that never removes a file it did not write
or one that has been edited since.

Decided, not yet built. Recorded in #42, with the deliberate removal surface in
#43.

## When this list is checked against the code

At the first release, and not only when somebody notices. Every entry above
either names a limit a check already holds or names the issue that will build
it, so the review is a comparison rather than a rereading: each "decided, not
yet built" entry is either built by then and moves up, or it is still open and
says so. The release checklist is #62 and that is where the condition belongs.

Part of that comparison is made on every run rather than once. `LimitsPageTests`
reads this page and refuses an entry that is in neither of the two states above,
one that names no issue, one that points a reader at a file this tree does not
have, and one that says a suite refuses it when the suite runs no such class.
What it cannot do is say whether a marker is TRUE: it reaches no tracker, so an
entry filed as decided and not yet built whose issue closed yesterday stays
green until a person moves it, and whether a named thing really holds a limit is
a reading rather than a comparison. So the review at the first release is
smaller than it was and it is not replaced.
