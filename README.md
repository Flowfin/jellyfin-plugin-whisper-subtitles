> [!NOTE]
>
> **Part of [Flowfin](https://github.com/Flowfin).** It works with any Jellyfin
> server, and with the Flowfin clients.

# Whisper Subtitles

A Jellyfin plugin that generates subtitles for library items that have none, by
transcribing their audio with Whisper.

The transcription runs outside the server process, through a command line tool on
the same machine or through a remote transcription endpoint. This plugin carries
no model and no inference runtime of its own, and the package an operator
installs is one assembly.

What comes out is machine transcription. It is marked as machine made so it can
be told apart from a subtitle somebody wrote, and this plugin promises nothing
about how good it is, which it does not measure.

## What state this is in

There is no release, and nothing here is installable on a server yet.

    $ gh release list --repo Flowfin/jellyfin-plugin-whisper-subtitles
    $ git tag

Both print nothing.

What exists is the backend interface and the selection between backends, a local
backend that drives a whisper.cpp compatible tool as a child process, the SubRip
writer, item selection, the record of what was attempted, and audio extraction.
What is missing between those and a subtitle appearing in a library is the
scheduled task that would run them, in #17, and the composition that hands each
piece its collaborators, in #71. Nothing in the tree implements a server task
today:

    $ git grep -l IScheduledTask -- '*.cs'

That prints nothing too.

So everything below describes a plugin that is being built rather than one that
can be installed and used now. Where a sentence is about something the tree
already holds it says so, and where it is a decision that is not yet built it
says that instead. `docs/limits.md` keeps the same two states apart for the
things this plugin will not do, and it is the page to read before deciding
whether this is the plugin you want.

## Which servers it runs on

Two server lines, and the pin for each is written in one place:

    $ grep -n 'SupportedServerLines\|JellyfinServerLine\|JellyfinPackageVersion' Directory.Build.props
    12:        <SupportedServerLines>net9.0;net10.0</SupportedServerLines>
    16:        <JellyfinServerLine>10.11</JellyfinServerLine>
    17:        <JellyfinPackageVersion>10.11.11</JellyfinPackageVersion>
    21:        <JellyfinServerLine>12.0</JellyfinServerLine>
    22:        <JellyfinPackageVersion>12.0.0-rc4</JellyfinPackageVersion>

The 10.11 line is .NET 9 and the 12.0 line is .NET 10. One source tree serves
both, and one invocation of the build produces an assembly for each:

    $ dotnet build -c Release
      Jellyfin.Plugin.WhisperSubtitles -> .../bin/Release/net10.0/Jellyfin.Plugin.WhisperSubtitles.dll
      Jellyfin.Plugin.WhisperSubtitles -> .../bin/Release/net9.0/Jellyfin.Plugin.WhisperSubtitles.dll

The 12.0 pin is a release candidate because that line has published no stable
package yet. A line named as supported and never compiled against is a weaker
claim than one compiled against its release candidate, and the pin moves when a
stable package exists.

An installable package per line does not exist yet. A package manifest carries a
single framework and a single target ABI, so the second manifest and the
packaging that reads it are #51.

## Choosing a backend

The operator chooses where the transcription happens. This plugin ships neither
half of that choice.

A local command line tool is what exists today. The plugin launches a whisper.cpp
compatible executable the operator points it at, hands it the extracted audio and
a model file the operator placed themselves, and reads what it prints. It
downloads nothing, ships nothing, and never goes looking for an executable it was
not given.

A remote endpoint speaking the OpenAI audio transcription API is decided and not
yet built, in #13. Audio leaves the machine when that is used, which is something
an operator has to be told before they switch it on rather than afterwards, and
saying so in the interface is #81.

No backend configured at all is a supported state rather than an error. A server
with the plugin installed and nothing set answers that it is not configured and
does no work, instead of failing later with a message about a missing file.

## What a run costs

Time is the backend's, not this plugin's. A transcription takes as long as the
model and the hardware take, and that ranges over more than an order of magnitude
between a small model on a current processor and a large one with no
acceleration. No measurement of throughput has been made in this repository, so
no number for it is quoted here. Making that measurement, and building the
estimate an operator is shown on top of it, is #38 and #37.

Memory is the backend's for the same reason. The work happens in another process,
so what a model costs in memory is charged to that process and reclaimed when it
ends.

Disk is this plugin's, and it is arithmetic rather than a measurement. Audio is
extracted to 16 kHz mono 16-bit PCM, which is the only format the whisper.cpp
tool accepts, and that format does not compress. One second is 32,000 bytes, so
an hour is about 115 MB and the size of the file follows the length of the item
and nothing else. Extraction stops at a ceiling, 2 GiB unless an operator sets
another, which is a little over eighteen hours of that format. The temporary file
is deleted on every path out of the extraction, including a failure and a
cancellation.

## From an install to a first subtitle

This is the path as it is being built. None of it can be walked today, because
the task that would run it does not exist.

1. Install the plugin from a repository listing, which is #61, or from a release
   archive, which is #60.
2. Open the plugin's page in the dashboard and choose a backend. For the local
   tool that is the path to the executable and the path to a model file you
   placed yourself. The page is #36, and the readiness probe that says whether
   the choice works before a run starts is #15.
3. Set the target language per library, in #30, or leave it to detection, in #31.
4. Run the scheduled task by hand from the dashboard, in #17. It ships with no
   trigger, so nothing starts on its own on a server whose operator did not ask
   for it.
5. The subtitle appears where the library saves subtitles, either beside the
   media file or in the item's metadata folder, in #25. It becomes visible only
   once it is complete, in #27, and a subtitle that is already there is never
   touched, in #28.

Where a run fails, `docs/troubleshooting.md` maps each reason the plugin reports
to something an operator can do about it.

## Building it and running its suite

The .NET SDK is the only prerequisite.

    git clone https://github.com/Flowfin/jellyfin-plugin-whisper-subtitles
    cd jellyfin-plugin-whisper-subtitles
    dotnet build -c Release
    dotnet test -c Release -f net10.0

`dotnet test -c Release` on its own runs the suite once per server line, which
needs both the .NET 9 and the .NET 10 runtimes installed. With one of them
present, name the line with `-f net9.0` or `-f net10.0`, which is also what the
workflow does so that a failure names the line it happened on.

The suite needs no display, no elevated rights and no network. That is a property
a check refuses to let go of rather than a habit, and `CONTRIBUTING.md` is where
it is written down.

## Lawful use

This plugin transcribes media that is already on the operator's server. Whether
transcribing a particular recording, keeping the result, or sending its audio to
a third party is lawful depends on who owns the recording, who is speaking on it,
and where everyone involved is. That judgement is the operator's, and neither
this repository nor the license makes it for them.

The remote backend, once it exists, sends audio off the machine to whatever
endpoint the operator configured. It is the only thing here that would do so, and
nothing in the tree opens a network connection today:

    $ git grep -n 'HttpClient\|WebRequest\|Socket' -- '*.cs'

That prints nothing. No telemetry is collected and none is planned.

The license disclaims warranty and liability. That disclaimer is in `LICENSE` and
is neither repeated nor softened here.

## Documentation

- `docs/limits.md`, what this plugin will not do, and which decision each limit
  came from.
- `docs/troubleshooting.md`, every failure reason it reports and what an operator
  can do about each one.
- `docs/subtitle-format.md`, why the output is SubRip and what the writer holds
  to.
- `docs/backend-interface.md`, what a transcription backend has to satisfy, for
  somebody adding a third one.
- `docs/logging.md`, what this plugin says in the server log and at which level.
- `CONTRIBUTING.md`, how to build it, run the suite and send a change.

## License

The GNU General Public License, version 3. The full text is in `LICENSE`.

See [NOTICE.md](NOTICE.md) for the intended-use notice.
