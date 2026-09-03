# Choosing a backend and a model

The first decision this plugin cannot make for an operator is which backend it
transcribes with, and, for one of the two, which model it hands that backend.
Everything else on the configuration page follows from that answer, and the
answer depends on the machine the server runs on rather than on anything in
this repository.

This page is written for somebody who has never seen a Whisper model. It says
what each backend needs, what each one costs, and which figures are somebody
else's measurement rather than this plugin's.

## What you set the backend to

The configuration page offers exactly these values, and the setting is stored as
the name in the first column.

| Setting value | What it transcribes with | What the operator supplies |
| --- | --- | --- |
| `None` | Nothing. The run reports that no backend is configured and transcribes no item. | Nothing. |
| `Local` | A whisper.cpp compatible command line tool, launched as a child process of the server. | The tool, and a model file. |
| `Remote` | An HTTP endpoint speaking the OpenAI audio transcription request shape. | A base URL, and a key if the endpoint wants one. |

`None` is not a failure state. It is what a fresh install holds, and it is the
value the page shows before anybody has chosen, so a server that has this plugin
installed and nothing configured does no work rather than guessing.

## The local backend

The tool and the model are two paths the operator types. Neither is ever
downloaded:

    git grep -n 'is ever downloaded\|Neither is downloaded' -- Jellyfin.Plugin.WhisperSubtitles
    Jellyfin.Plugin.WhisperSubtitles/Backends/Local/LocalBackendOptions.cs:16:/// Neither path is ever downloaded. That is a fixed property of this plugin
    Jellyfin.Plugin.WhisperSubtitles/Backends/Local/LocalWhisperBackend.cs:20:/// The tool and the model are paths an operator typed. Neither is downloaded and

That is a property of the plugin and not a default somebody set. A plugin that
fetched several gigabytes it was not asked for would be making a trust decision
on somebody else's server, and the cost of not making it is the one this page
exists to hand back: the operator fetches a model themselves before anything
works.

What it buys is that the transcription runs on the operator's own machine and no
audio leaves it.

### The models, and what the figures are

The sizes below are the upstream project's own table for its own runtime. Read
at `ggml-org/whisper.cpp` README blob
`6bf407df932892a697b9d780648e7951d4226672`:

    gh api repos/ggml-org/whisper.cpp/contents/README.md --jq '.content' | base64 -d | grep -A 6 -E '^\| Model'
    | Model  | Disk    | Mem     |
    | ------ | ------- | ------- |
    | tiny   | 75 MiB  | ~273 MB |
    | base   | 142 MiB | ~388 MB |
    | small  | 466 MiB | ~852 MB |
    | medium | 1.5 GiB | ~2.1 GB |
    | large  | 2.9 GiB | ~3.9 GB |

Read what that table is before choosing off it. `Disk` is the size of the model
file the operator downloads. `Mem` is the memory the upstream project reports
its own runtime holding while that model is loaded. Neither column is a figure
about this plugin, and neither is a speed.

A larger model is slower and, on most material, better. How much of each is not
in that table and is not sourced anywhere on this page: it depends on the
language, on how clean the audio is, and on the machine. Anybody who states a
ratio without measuring it on that machine is guessing, which is why the plugin
is meant to measure it instead.

The middle three rows are the ones the decision is usually between. An operator
choosing between `tiny` and `large` is choosing between two ends nobody
recommends: the first is fast enough to be worth trying and rarely good enough
to keep, and the last wants about four gigabytes of resident memory on a machine
that is also serving video.

There is a floor on what this plugin will believe is a model at all:

    git grep -n 'SmallestPlausibleModelBytes =' -- Jellyfin.Plugin.WhisperSubtitles
    Jellyfin.Plugin.WhisperSubtitles/Backends/Local/LocalBackendOptions.cs:41:    public const long SmallestPlausibleModelBytes = 1024L * 1024;

One mebibyte, and the smallest model in the table above is 75 times that. It
catches a download that was refused and saved anyway, a page of HTML from a
proxy, or an empty file made by a shell redirect, each of which otherwise
reaches the operator as a tool that starts and fails on the first item.

## The remote backend

The operator gives a base URL and, where the endpoint wants one, a key. The path
under that base URL is fixed rather than configurable:

    git grep -n 'TranscriptionPath =' -- Jellyfin.Plugin.WhisperSubtitles
    Jellyfin.Plugin.WhisperSubtitles/Backends/Remote/RemoteBackendOptions.cs:45:    public const string TranscriptionPath = "v1/audio/transcriptions";

An endpoint that speaks that request shape is served here. An endpoint that
serves the same thing at another path is not this interface with a different
path, it is a different interface.

Two bounds an operator meets rather than chooses. One request may take ten
minutes before it is abandoned, which is a number somebody with feature films
and a modest endpoint has to raise. At most eight mebibytes of response are
read, which is far above a verbose transcript of a long film and far below a
proxy that streams without stopping.

    git grep -nE 'DefaultRequestTimeout = |DefaultMaxResponseBytes = ' -- Jellyfin.Plugin.WhisperSubtitles
    Jellyfin.Plugin.WhisperSubtitles/Backends/Remote/RemoteBackendOptions.cs:34:    public const long DefaultMaxResponseBytes = 8L * 1024 * 1024;
    Jellyfin.Plugin.WhisperSubtitles/Backends/Remote/RemoteBackendOptions.cs:58:    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(10);

The cost of this backend is the one an operator has to decide about rather than
configure: the audio of the item being transcribed is sent to that endpoint. The
disclosure this plugin owes before the remote backend can be switched on is #81,
and it is not written yet, so an operator choosing `Remote` today is choosing it
with less in front of them than this repository intends to put there.

## Which of the two

Choose `Local` where the machine has the memory in the table above to spare and
the operator would rather spend the machine than send the audio anywhere. Choose
`Remote` where the server is small, the endpoint is one the operator already
runs or already trusts, and sending the audio to it is acceptable.

Neither is a default. The plugin ships with `None` and asks.

## The licences

Four separate statements, and none of them is about the model file an operator
supplies.

    git show origin/master:LICENSE | sed -n '1,2p'
                        GNU GENERAL PUBLIC LICENSE
                           Version 3, 29 June 2007

    for r in ggml-org/whisper.cpp SYSTRAN/faster-whisper openai/whisper; do gh api repos/$r --jq '"\(.full_name) \(.license.spdx_id)"'; done
    ggml-org/whisper.cpp MIT
    SYSTRAN/faster-whisper MIT
    openai/whisper MIT

So this plugin is GPL-3.0, and whisper.cpp, faster-whisper and the original
Whisper release each declare MIT. Those are readings of what each repository
declares rather than an audit of what is inside it.

The model file is not covered by any of them. Whoever published the weights
an operator downloads set terms for them, and checking those terms is the
operator's to do.

## What the tree holds today

No run of this plugin has ever produced a subtitle. The scheduled task selects a
backend and finishes without reaching any part of the pipeline:

    git grep -nE 'ItemSelection|AudioExtractor|BoundedRun|SubtitlePublisher|TranscriptionRequest|TemporaryAudioSweep|AttemptLedger|DurationWeightedProgress' -- Jellyfin.Plugin.WhisperSubtitles/Scheduling/SubtitleGenerationTask.cs
    exit=1

That sentence is the one thing on this page a run reads, and
`BackendGuidePageTests` refuses it in both directions: the sentence surviving the
arrival of a pipeline, which would leave this page reading as a walkthrough of
something that works, and the sentence disappearing while nothing runs, which
would leave the same impression more quietly. The join is #183.

Four things this page would otherwise tell an operator to do follow from that,
and they are in two states rather than one. Three have arrived as far as a field
on a page and no further, which is worth naming rather than filing under done.
One does not exist to be run.

**Where the tool path and the model path are typed.** On the configuration page,
under the backend chooser, and they appear there only while `Local` is the backend
chosen. They are the first two settings a backend owns rather than the plugin:

    git grep -n 'public .* { get; set; }' -- Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:33:    public int SchemaVersion { get; set; } = ConfigurationValidation.CurrentSchemaVersion;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:45:    public string Backend { get; set; } = ConfigurationValidation.NoBackendChosen;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:63:    public string LocalToolPath { get; set; } = ConfigurationValidation.NoPathNamed;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:76:    public string LocalModelPath { get; set; } = ConfigurationValidation.NoPathNamed;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:97:    public string RemoteBaseUrl { get; set; } = ConfigurationValidation.NoRemoteSettingNamed;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:115:    public string RemoteApiKey { get; set; } = ConfigurationValidation.NoRemoteSettingNamed;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:128:    public string RemoteModel { get; set; } = ConfigurationValidation.NoRemoteSettingNamed;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:141:    public string TargetLanguage { get; set; } = string.Empty;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:162:    public int ItemsAtOnce { get; set; } = ConfigurationValidation.LetTheMachineDecide;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:180:    public int ThreadsPerItem { get; set; } = ConfigurationValidation.LetTheMachineDecide;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:200:    public LibraryLanguageTarget[] LibraryTargets { get; set; } = [];
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:227:    public int FailuresBeforeQuarantine { get; set; } = ConfigurationValidation.LetThePolicyDecide;

Typing a path does not check it. Whether a file is there, whether it runs and
whether it is a model are the readiness probe's questions, which is #15, so what
these two fields buy today is that the values a run would use are values an
operator chose rather than values nobody could set.

**Where the URL and the key are typed.** On the configuration page, under the
backend chooser, and they appear there only while `Remote` is the backend chosen,
with the model name beside them and a statement of where the audio goes that
names the host out of the URL typed. Typing them checks one thing, which is that
the URL is one the backend could post to; whether the host answers, accepts the
key and serves the model are the readiness probe's questions, which is #15. As
with the paths, nothing carries the three values to the backend yet, so choosing
`Remote` reaches selection, which reports which settings are not filled in and
transcribes nothing.

**How to run the calibration so the estimate is about this machine.** There is no
calibration to run. The arithmetic that folds measured items into a factor exists
and is held by a suite; nothing has measured anything, because measuring needs a
run over an item and the task performs none. What joins the pipeline into such a
run is #183, and the estimate a factor would feed is the dry run in #37.

**Which setting to lower first when a run interferes with playback.** Both limits
are settings now and neither reaches a transcription yet, which are two different
sentences and both of them matter. You can type either number on the
configuration page, and the page says in its own words that nothing carries it to
a backend, because nothing performs a run over an item. So what follows is what a
run would do to your machine on the defaults, and lowering a number today changes
what the file says rather than what a server does.

The two fields on the page:

    git grep -n 'public int ItemsAtOnce\|public int ThreadsPerItem' -- Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:162:    public int ItemsAtOnce { get; set; } = ConfigurationValidation.LetTheMachineDecide;
    Jellyfin.Plugin.WhisperSubtitles/Configuration/PluginConfiguration.cs:180:    public int ThreadsPerItem { get; set; } = ConfigurationValidation.LetTheMachineDecide;

Zero on either means nobody has chosen and the machine decides. A number above
one per processor is refused rather than quietly reduced, and what is in force
stays the default, which is the sentence an operator gets instead of a number
they did not ask for.

How many items run at once, where nobody has chosen, is a constant at the
conservative end:

    git grep -n 'public const int Default' -- Jellyfin.Plugin.WhisperSubtitles/Scheduling/ConcurrencyCap.cs
    Jellyfin.Plugin.WhisperSubtitles/Scheduling/ConcurrencyCap.cs:31:    public const int Default = 1;

How many threads one of them may use follows the machine rather than a constant,
and it is below the processors the server reports:

    git grep -n 'public static int DefaultFor' -- Jellyfin.Plugin.WhisperSubtitles/Scheduling/ThreadCount.cs
    Jellyfin.Plugin.WhisperSubtitles/Scheduling/ThreadCount.cs:52:    public static int DefaultFor(int processorCount)

Half the processors, rounded down, and one on a single-processor machine, where
there is no value below it. That number reaches the local tool on every run:

    git grep -n '"-t",' -- Jellyfin.Plugin.WhisperSubtitles/Backends/Local/LocalWhisperBackend.cs
    Jellyfin.Plugin.WhisperSubtitles/Backends/Local/LocalWhisperBackend.cs:365:            "-t",

It is passed on every run rather than only when it differs from something,
because there is no value of that flag meaning "whatever you would have done".
Leaving it out picks whisper.cpp's own default, and the transcripts upstream
pastes into its README show that default not following the machine:

    gh api repos/ggml-org/whisper.cpp/contents/README.md --jq '.content' | base64 -d | grep -oE 'n_threads = [0-9]+ / [0-9]+'
    n_threads = 4 / 10
    n_threads = 4 / 8
    n_threads = 4 / 10
    n_threads = 4 / 10

Four threads on a ten-processor machine and four on an eight-processor one. Those
are example runs somebody pasted rather than a reading of the tool's source,
so it is evidence about those runs; what it is offered against is the claim that
omitting the flag is not the neutral option. Either way the number would be
chosen by somebody who never saw your server.

Conservative defaults are the answer question 6 of #8 carries, taken on
2026-08-24: one item at a time, few threads, and this page telling the raising
story. Raising them is now something you can do on the page, and it does not yet
reach a run. The process priority is no longer among what is missing: both
programs this plugin starts, the transcription tool and the media tool that
extracts the audio, ask the operating system to schedule them below ordinary
work, and a platform that refuses costs the item nothing. Nothing tells you when
one refuses, because this plugin does not log yet. What is still missing from
#22 is a per-item time limit and a rule that yields to a busy server, and the
definition of busy that last rule turns on is still open in that issue's own
body.

So this page can be read to a decision about a backend and a model, and it cannot
be followed to a first generated subtitle. That is what it says rather than
something a reader has to discover, and `docs/limits.md` keeps the same two
states apart for the rest of the plugin.
