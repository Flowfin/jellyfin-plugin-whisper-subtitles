# The mutation run, and the first score it produced

The coverage floor in `.github/coverage/pure-logic.txt` says which lines of the
pure logic ran. It does not say whether any test would have noticed the line
being wrong. This run answers that: it replaces each statement in that same set
with a wrong one and asks whether the suite goes red.

The scope is that file and not a second copy of it. `.github/workflows/mutation.yml`
reads it to build the run, and `.github/scripts/read-mutation-report.sh` reads it
again to refuse a report that scored a mutant outside it.

The score is printed and never enforced. A threshold on a score becomes a number
people tune, and the useful output is the survivor list under it: each line names
a file, a line and the mutation that lived, which is a test somebody can write.
What the job does refuse is a run that says nothing, in five directions, and each
refusal is executed against a damaged copy of a real report in the same job.

## The first score

    mutation score 86.68% (319 detected of 368 scored: 168 killed, 151 timed out,
    39 survived, 10 never reached)

Measured on 2026-08-08, at `940d9d7`, on a developer machine and not on a runner.
The workflow builds the scope with a loop rather than a substitution; the same run
written for a shell prompt is:

    cd Jellyfin.Plugin.WhisperSubtitles.Tests
    VSTEST_CONNECTION_TIMEOUT=300 dotnet stryker --skip-version-check \
      --target-framework net10.0 --configuration Release \
      --reporter Json --reporter ClearText --output ../StrykerOutput \
      $(grep -v '^[[:space:]]*#' ../.github/coverage/pure-logic.txt | grep -v '^$' | sed 's/^/--mutate /')

    .github/scripts/read-mutation-report.sh \
      StrykerOutput/reports/mutation-report.json \
      .github/coverage/pure-logic.txt Jellyfin.Plugin.WhisperSubtitles

A later score has this to be compared against, and the date and the commit are
here because a score with neither beside it cannot be compared with anything: a
score that moved says nothing until it is known whether the code or the suite
moved under it.

## What this number is worth, and where it is soft

Read the three things below before comparing a later score to this one.

Nearly half of what is counted as detected is a timeout rather than a killed
mutant. A mutant that makes the suite hang is genuinely detected, and it is also
what a mutant looks like on a machine too loaded to answer in time. This run
retried seven test sessions and needed the connection timeout raised from its
default of 90 seconds before it could discover any test at all, so 151 of the 319
are the softest part of the figure. A runner may return a different split for the
same tree.

`Backends/BackendSelector.cs` contributes nothing. All 36 of its mutants failed to
compile, so the file is in the scope and outside the score, and a change to it
moves this number not at all. The mutation tool removes mutations inside methods
it cannot safely mutate, which here is the async selection path.

The figure is from one server line. The mutated set carries no conditional
compilation, so `net9.0` compiles the same statements, and a second line would
double the longest job in this repository to re-mutate identical source.

## The version

`.config/dotnet-tools.json` pins the tool, so a later run is comparable rather
than measured with whatever was newest that morning. The pin is the reason the
score above can be read as a fact about this tree.
