# Contributing

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
looking for the missing coverage will find it.

The check is the `Headless suite` job in `.github/workflows/headless.yml`. It
asserts it is not running as root and that `DISPLAY` is unset, then runs the
suite with `DISPLAY` removed from the environment, once for each supported server
line. A test that needs a display turns it red.

## Running the suite

```
dotnet test -c Release
```

Both supported server lines are built and tested. Running one at a time is
`-f net9.0` or `-f net10.0`, which is what the headless job does so that a
failure names the line it happened on.

## Before you push

State what changed and what failure it prevents, in the commit message. Where a
guard is added, say how you know it bites: the way to know is to break the thing
it guards and watch the suite go red.

Commits carry a `Signed-off-by` line. The `DCO sign-off` check refuses a pull
request without one.
