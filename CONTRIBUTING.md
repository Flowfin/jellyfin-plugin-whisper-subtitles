# Contributing

## What is different about this repository

Three things, and they are the reason this file exists rather than a link to any
guide for a C# project.

There are two supported server lines and the tree builds both from one source.
`Directory.Build.props` is the only place a target framework or a Jellyfin
package version is written, and `Directory.Build.targets` refuses a line whose
framework and package pin disagree.

Transcription runs outside the server, so the heavy part of this plugin is a
program or an endpoint that is not here. Everything reaches it through
`ITranscriptionBackend`, and every test drives a double rather than a model.

No test may need a display, elevation, or a machine trust store. That is checked
rather than trusted, and it is the section below.

## Building

```
dotnet restore
dotnet build -c Release -f net9.0
dotnet build -c Release -f net10.0
```

Building either line needs only the SDK. `net9.0` is the 10.11 server line and
`net10.0` is the 12.0 line; which package version each one pins is in
`Directory.Build.props` and is not repeated here.

## Running the suite

```
dotnet test -c Release -f net10.0
```

RUNNING a line needs that line's runtime installed, not just the SDK, because
the test host is an application built against it. A machine with only one of the
two runs one line and reports the other as a host that could not start. Running
both at once is the same command without `-f`:

```
dotnet test -c Release
```

The headless job runs them one at a time on purpose, so a failure names the line
it happened on.

## The gate

`.github/workflows/` is the authority for what runs, and this file does not list
it, because a list in a document drifts against the thing it describes. What the
mainline actually refuses a merge without is smaller than what runs, and it is
printed rather than restated:

```
gh api "repos/iderex/jellyfin-plugin-whisper-subtitles/rulesets/$(gh api repos/iderex/jellyfin-plugin-whisper-subtitles/rulesets --jq '.[] | select(.name == "gate") | .id')" --jq '[.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[].context]'
```

The ruleset id is read rather than written down, so the command survives the
ruleset being replaced.

Moving the rest of the checks into that set is #54, and it is deliberately the
last thing in its milestone: requiring a check before it is stable turns a red
gate into something people learn to work around.

## The suite is headless, and that is checked

Every test in this repository runs without a display, without elevation, and
without touching a machine trust store.

This is a property of the suite from its first test rather than something
recovered later. A suite that has acquired one test needing any of the three has
acquired the machine it was written on as a dependency: it passes for whoever
added it and fails for the next person, who has no way of telling a real defect
from a missing X server.

In practice that means:

- No test opens a window, draws anything, or reads `DISPLAY`.
- No test runs a command that raises an elevation prompt, installs a service,
  registers a scheduled task, or writes to a machine-wide location. On Windows
  that includes `dotnet dev-certs https --trust`, `netsh` and `sc.exe`.
- No test adds, reads or removes a certificate from a machine trust store. A test
  that needs a certificate makes one and keeps it to itself.

A test that cannot be written under those conditions is not written and made to
pass anyway. It is left out, and what was left out is written down where somebody
looking for the missing coverage will find it. That is the next section.

The check is the `Headless suite` job in `.github/workflows/headless.yml`. It
asserts it is not running as root and that `DISPLAY` is unset, then runs the
suite with `DISPLAY` removed from the environment, once for each supported server
line. A test that needs a display turns it red.

## Tests that are refused, and what replaces each one

Here so that none of them is reinvented later by somebody who does not know it
was already decided. Each line is a test a reader would expect, and the thing
that stands in for it.

- A test that starts a desktop or GUI dependent server to check the
  configuration page. Replaced by a test of the page markup and script against
  the configuration object, in `ConfigurationShellTests`, and by the page load
  being exercised by the headless server boot in #63.
- A test that installs a certificate into the machine trust store to exercise a
  remote backend against TLS. Replaced by tests through an injected HTTP message
  handler, and where a real TLS path is needed, a certificate trusted by that one
  handler instance and never by the machine.
- A test that needs elevation to lower a process priority or apply a cgroup
  limit. Replaced by asserting the values the limiter computes and the calls it
  makes through an injected seam, plus a test that a failure to apply a limit is
  logged and does not fail the item.
- A test that requires a GPU. Replaced by capability probe tests over a stubbed
  device query, and by the backend contract tests, which are hardware
  independent by construction.
- A test that downloads a model or calls a public transcription service.
  Replaced by the stub backend and by recorded response fixtures.
- A test that depends on the wall clock or on the machine's locale. Replaced by
  an injected clock, and by an explicit culture in every test that formats or
  parses.

This list is NOT yet checked against the suite, and several of the replacements
name work that has not landed. #46 holds both halves: making each replacement a
real test, and making the list something a run compares rather than something a
reader trusts.

## Adding a backend

A backend implements `ITranscriptionBackend` and lives under
`Jellyfin.Plugin.WhisperSubtitles/Backends/`. The interface returns timed
segments rather than a formatted file, because formatting, naming and marking
belong to this plugin and must not differ between backends.

Nothing outside that folder may name a concrete backend in a signature.
`BackendInterfaceTests` refuses it, using `BackendIsolation`, and the fixtures it
judges against are in `BackendFixtures.cs`, including the one-change neighbour
that has to stay green.

Whatever the backend reaches out to goes through an injected seam, so a test
needs no model, no binary and no network. `IProcessRunner` is the one for a child
process, and `LocalWhisperBackendTests` shows a backend being driven through it.

THE CONTRACT SUITE EVERY BACKEND MUST PASS DOES NOT EXIST YET. It is #74, and
until it lands a new backend is measured against tests written beside it rather
than against one suite it can be pointed at. Writing a backend now means writing
those tests too.

## Writing a fixture

Where a test asserts on exact bytes, the file has to arrive out of a clone
unchanged, and by default it does not: `.gitattributes` stores text with a line
feed and the checkout puts carriage returns back on the platforms that ask for
them. So the same commit gives two clones different bytes.

`.gitattributes` is where that is decided for the tree, and it carries the reason
for each rule. `*.srt` is marked as not text, because SubRip ends every line with
a carriage return and a fixture exists to prove the bytes the writer produced.
`*.sh` is line feed everywhere, because a shell reads a carriage return as part
of the command.

`SubRipFixtureBytesTests` is the shape to copy. It compares the writer against
the committed file, and separately asserts the file still has its carriage
returns, so a clone that rewrote the fixture and a change to the writer fail
differently and say which one moved.

## Before you push

Run the suite. State what changed and what failure it prevents, in the commit
message. Where a guard is added, say how you know it bites: the way to know is to
break the thing it guards and watch the suite go red.

Commits carry a `Signed-off-by` line. The `DCO sign-off` check refuses a pull
request without one.
