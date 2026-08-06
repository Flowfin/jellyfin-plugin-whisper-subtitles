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

Decided, not yet built. Recorded in #28, which asks for a test that a
pre-existing file's bytes are unchanged after a run, that no numbered variant
appears beside it, and that the item is reported as skipped.

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

Decided, not yet built. Recorded in #17, whose done-condition is that
`GetDefaultTriggers` returns no trigger that would start work unattended.

## What it writes, and where

Three kinds of thing, and no fourth.

The subtitle file, next to the media file or in the item's metadata folder,
following the library's own setting for where the server saves subtitles. Which
one applies when the library expresses no preference is open in #8; #25 holds
the writing itself, and #27 holds the rule that a file becomes visible only once
it is complete.

Temporary audio, extracted from the item so a backend has something to work
from, in a directory this plugin owns. It is deleted on every exit path, and
anything a dead process orphaned is swept before the next run begins rather than
by a handler that may never run. #11 and #21.

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
