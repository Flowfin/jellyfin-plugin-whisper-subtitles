# Security policy

This repository is a Jellyfin plugin. It runs inside a media server somebody
else owns, and what it does there is read the audio of library items and write
subtitle files back into that library. The media was not written by the
operator, the transcription is done by a program this repository did not build,
and the files that come out land in a directory somebody's media player is
watching. Nearly everything worth reporting here follows from those three facts.

## What is actually running today

Nothing yet, and saying so first saves a reporter from testing a version that
does not exist.

    $ gh release list --repo Flowfin/jellyfin-plugin-whisper-subtitles
    $ gh api repos/Flowfin/jellyfin-plugin-whisper-subtitles/tags --jq 'length'
    0

No release and no tag. The tree goes further than that:
`PluginServiceRegistrator` builds both backends with empty settings, and it does
that whatever the operator saved. The configuration schema holds a local tool
path and a local model path, and the one place this plugin's source builds the
local backend's settings passes neither of them:

    $ git grep 'new LocalBackendOptions' -- 'Jellyfin.Plugin.WhisperSubtitles/*.cs'
    Jellyfin.Plugin.WhisperSubtitles/PluginServiceRegistrator.cs:        serviceCollection.AddSingleton(_ => new LocalBackendOptions(null, null));

So a path an operator types is validated and then reaches no backend, and no
setting holds a remote endpoint or a key at all.
`SubtitleGenerationTask.ExecuteAsync` selects a backend, records that none is
configured, and stops. Nothing in the plugin's own source calls the audio
extractor, the item selection or the subtitle publisher; those are reached only
from the test suite. On a server built from this tree no audio is extracted, no
child process is started, no request leaves the machine, and no file is written
into a library. The boundaries below are real code with tests over them, but a
flaw in one is a flaw in a part not yet joined to a run rather than something
loose on a live server.

## Where to report

Privately, through GitHub's advisory form for this repository:

https://github.com/Flowfin/jellyfin-plugin-whisper-subtitles/security/advisories/new

That channel is open, which is a reading rather than an intention:

    $ gh api repos/Flowfin/jellyfin-plugin-whisper-subtitles/private-vulnerability-reporting
    {"enabled":true}

Please do not open a public issue for something you believe is exploitable
against a running server. Anything you are happy to discuss in the open,
including a hardening idea, is welcome as an ordinary issue.

## What I do not promise

No acknowledgement deadline, anywhere in this document, on purpose. A maintainer
who publishes a number will eventually miss it, and the reporter on the other
end cannot tell a missed deadline from a report that never arrived. What I will
do is answer once I have read the report properly, and say what I intend to do.

## The surface worth a report

**Media files the operator did not author.** This is what makes this plugin its
own case: a library is full of files from elsewhere, and this plugin feeds them
to programs that parse them. It never parses a container itself. The media path
goes to the server's own media tool, whose location comes from Jellyfin's
`IMediaEncoder.EncoderPath` and from nowhere else: not a setting, not an
environment variable, not a bare name left to the search path. It travels as one
element of an argument vector, never a command line, and `SystemProcessRunner`
starts it with `UseShellExecute = false` and one `ArgumentList` entry per
argument. Extraction takes the chosen audio stream, refuses every other, and
stops at a byte ceiling.

**What the transcription tool prints back.** `WhisperOutputReader` reads the
standard output of a program the operator supplied. It holds a line ceiling of
8192 characters and a segment ceiling of 200000, parses timestamps by hand so
there is no expression to reason about over hostile bytes, and refuses a line it
cannot read rather than skipping it.

**What a remote endpoint answers with.** `TranscriptionResponseReader` reads a
body from a machine this plugin knows nothing about. The read is bounded at 8
MiB against the bytes that arrive rather than the length the response declares
about itself, the content type has to be JSON, invalid UTF-8 is refused, and so
is a segment timed past anything a library can hold. Both readers are fuzzed,
with corpora in `fuzz/corpus`, and that last refusal exists because a fuzzer
found it rather than because somebody predicted it.

**Metadata that becomes a file name.** An item name and a language code returned
by a backend both end up in a path. `SubtitleDestination.Resolve` compares the
resolved path against the folder it must stay inside, and `SubtitleLanguageCode`
refuses the shape of a code before any table is consulted. A name that reaches
the file system outside those folders is a report I want.

**Where files are written.** Three kinds and no fourth: the subtitle, in the two
folders Jellyfin itself saves subtitles to and following the library's own
setting for which; temporary audio, in a directory this plugin owns; and its own
configuration, which the server writes. `AtomicSubtitleFile` writes under a
`.whisper-part` name with `FileMode.CreateNew` and renames only once every byte
is flushed, so nothing reads a half written subtitle and no file this plugin did
not write is overwritten. A write outside those three is a defect whether or not
anybody can steer it.

**The configured endpoint key.** It goes into one Authorization header and into
no URL, body, message or log line, and text coming back from the endpoint is
scrubbed of it before it is quoted, because gateways do echo the headers they
were sent. A path where that key reaches a log or an operator's screen is worth
reporting even though the operator owns the key.

**The configuration page.** It is served in the Jellyfin dashboard and runs with
whoever is looking at it. Library names reach the DOM through `textContent`
rather than `innerHTML`, and a path where server-side data reaches that page as
markup would be a real finding.

## What is not a vulnerability here

**The operator running a program they chose.** The design is that the operator
supplies the transcription tool and the model file, or an endpoint URL. A
setting that causes a binary on their own machine to run is the feature, and
somebody who can already write this plugin's configuration already has the
server.

**A crash inside the media tool or the transcription tool.** Those are other
people's programs, and every backend here is out of process precisely so a
native fault in one ends a transcription instead of the media server. A
malformed media file that makes ffmpeg fall over belongs to ffmpeg, and a model
file that makes a whisper.cpp build fall over belongs to that build. What is
mine is a failure to keep such a thing out of process, or to bound what comes
back from it.

**What a remote endpoint does with the audio after it arrives.** Choosing that
backend sends the audio of every selected item to the host in the URL the
operator typed. Whether that host stores it, logs it or trains on it is a
property of somebody else's machine and nothing this plugin can ask.
`docs/limits.md` records that as a limit rather than a defect, and the operator
who chose the endpoint is answerable for it.

**A bad transcription.** Accuracy is not measured here and not promised
anywhere, and output is marked as machine made so it can be told apart from a
subtitle a person wrote. A wrong or garbled subtitle is not a security issue.

**A finding against Jellyfin itself.** If the flaw is in the media server rather
than in this plugin, it belongs to that project and its own security process, at
https://github.com/jellyfin/jellyfin. I am a guest on their interfaces and not
the right person to receive it.

**Scanner output with no path through this code.** A dependency alert on a
package this plugin never calls is an ordinary issue rather than an advisory.
Dependabot and secret scanning are already on here.

**A document here describing less than the tree does.** The README said two
greps printed nothing, one for the scheduled task and one for network types,
while both printed. That one is repaired and something reads it now, but the
class is not closed: a page that understates what this plugin holds is a stale
document worth an issue, and reading it is not a vulnerability.
