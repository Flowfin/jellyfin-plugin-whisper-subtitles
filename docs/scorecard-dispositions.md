# Scorecard findings, and what was decided about each

The Scorecard workflow is a self-audit and not a gate. `.github/workflows/scorecard.yml`
says so at the top and `#49` keeps it out of the required set, because the score moves
for reasons that have nothing to do with any one pull request.

What makes it worth running anyway is that every finding it reports ends somewhere.
Fixed, or accepted with the reason written down. A score with no disposition behind it
is a badge, and a badge is the thing this repository is trying not to have.

The findings are read rather than remembered, and they are read out of one run rather
than off the dashboard. The audit uploads the document it produced and then compares this
page against that same document in the same job, so what a run reported is the population
this page is organised by:

```
gh run download 32309502320 -n 'SARIF file' -D .
jq -r '([.runs[].tool.driver.rules[] | {key: .id, value: .name}] | from_entries) as $names
  | [.runs[].results[] | $names[.ruleId]] | sort[]' results.sarif
Branch-Protection
CII-Best-Practices
Code-Review
Fuzzing
Maintained
Pinned-Dependencies
```

Six, and they are the six below, taken worst first here rather than in the order the
document lists them. A run keeps that file for five days, so a reader after that reads
the same six off the accounting the comparison prints, which every run of the audit
carries whether it passes or fails.

The audit runs on each push to `master` rather than on the weekly schedule alone, so the
list is re-read rather than dating from one run. What it runs on is declared in the tree
rather than inferred from a history:

```
git grep -nE '^  (branch_protection_rule|schedule|push):' -- .github/workflows/scorecard.yml
.github/workflows/scorecard.yml:30:  branch_protection_rule:
.github/workflows/scorecard.yml:32:  schedule:
.github/workflows/scorecard.yml:34:  push:
```

What those triggers have produced is a window that moves with every push, so the command
is handed over and its output is not frozen here:

```
gh run list --workflow scorecard.yml --limit 20 --json event,headBranch,conclusion,headSha \
  --jq '.[] | "\(.event) \(.headBranch) \(.conclusion) \(.headSha[0:7])"'
```

The set moves in both directions and neither direction announces itself. One entry below
arrived on its own with a change that was about something else. One entry that used to be
here has stopped being reported, and the run that failed on `a26e997` is where it was
noticed: the comparison refused this page for still carrying a heading the run no longer
had a finding for. What left, and why, is at the end of this page rather than deleted
along with the heading.

## The tab is a different population and answers differently

The route a reader reaches for first is the code-scanning tab, and it does not return the
six above:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&state=open&per_page=100" \
  --jq '.[] | "\(.rule.description) \(.rule.security_severity_level)"'
Maintained high
```

That is not the audit having gone quiet. An alert leaves the open list when somebody
dismisses it or when a run stops reporting what it was about, and both have happened
here:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&per_page=100" \
  --jq '.[] | "\(.rule.description) \(.state)"' | sort
Branch-Protection dismissed
CII-Best-Practices dismissed
Code-Review dismissed
Fuzzing dismissed
Maintained open
Pinned-Dependencies dismissed
Security-Policy fixed
```

So a disposition here and a dismissal there are two records of one finding kept in two
places, and only the first of them is compared against a run. Every heading below carries
the score the run gave it, never the state the tab shows.

TWO RECORDS OF ONE FINDING CAN ALSO DISAGREE, AND ONE PAIR DOES. Being a different
population is a difference of extent, which is what the commands above measure. The
`Fuzzing` entry below and the dismissal beside it are not different in extent: they make
opposite statements about this repository, and that entry says which of the two the tree
holds. Nothing here reconciles them, because a dismissal is written on the tab and
changing one is not a change to this tree.

## Branch-Protection, score 3

Five warnings against `master`: stale review dismissal is disabled, the branch does not
require approvers, codeowners review is not required, last push approval is disabled,
and up-to-date branches is disabled.

Accepted, and it is not a finding a change to the tree could answer either way. What a
branch requires is the ruleset, which is a repository setting and not something a pull
request carries:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/rulesets/20467991" \
  --jq '{bypass: .bypass_actors, required: [.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks[].context]}'
```

The command is handed over and its output is not pasted, because it was pasted and it
stopped reproducing. The frozen line named three required contexts and the ruleset names
four. The fourth is read and recorded on `#49`, which is the issue that compares that set
and owes a re-derivation of it. Nothing here re-runs a command against a repository
setting, so a paste of one ages without saying so, and this disposition does not rest on
which contexts are in the set. It rests on the set being a setting.

`#54` is where the gate is tightened and where each of these five is either taken or
refused with a reason. One of them used to have a prerequisite in the tree rather than in
the settings, and no longer has it. Codeowners review cannot be required without a
codeowners file, and that file is here:

```
git log --oneline --diff-filter=A -- .github/CODEOWNERS
20b1bb7 Add the templates and the codeowners file, with a guard on the form (#85)
```

So all five warnings are settings now and none of them is waiting on anything in this
tree. Whether each one is taken is still `#54` and is not decided here.

The half of this check that is already answered is worth separating from the half that
is not. The ruleset has no bypass actors, so what it does require, it requires of
everybody.

## Code-Review, score 0

Reported as nought of sixteen approved changesets. The denominator is the number of
changesets the check sampled, so it moves as pull requests land, and it was fourteen when
this entry was written:

```
jq -r '([.runs[].tool.driver.rules[] | {key: .id, value: .name}] | from_entries) as $names
  | .runs[].results[] | select($names[.ruleId] == "Code-Review") | .message.text' results.sarif
score is 0: Found 0/16 approved changesets -- score normalized to 0
```

Accepted, and it is the same setting as Branch-Protection rather than a second thing.
The check counts approving reviews on the pull requests it sampled and found none, and
whether an approving review becomes a condition of merge is the ruleset above, which is
`#54`. Nothing in the tree moves this number.

A repository whose changes carry no approving review is a fact about that repository, and
the disposition for it is that the decision is open and named, not that the finding is
wrong.

This entry used to close by saying the finding was left rather than dismissed. That was a
statement about the tab, and it is not the state there:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&per_page=100" \
  --jq '.[] | select(.rule.description == "Code-Review") | "\(.state) \(.dismissed_reason)"'
dismissed won't fix
```

The dismissal reads it as unsatisfiable rather than as wrong, which is the same substance
as the paragraph above, so the sentence is corrected and the disposition is not changed.
The run still reports the finding, which is why this heading stays.

## Maintained, score 0

Reported as the project having been created within the last ninety days.

Accepted, and nothing can be done about it in either direction:

```
gh api repos/Flowfin/jellyfin-plugin-whisper-subtitles --jq .created_at
2026-08-05T16:38:10Z
```

This one clears itself with the calendar. Worth stating plainly because a high severity
against a young repository reads like a warning about neglect and is the opposite: the
check has too little history to judge, and says so.

## Fuzzing, score 0

Reported as the project not being fuzzed, with the warning that no fuzzer integrations
were found.

Accepted, and the finding is about what the check looks for rather than about what this
repository does. The two parsers that read bytes this plugin did not produce are fuzzed,
by a harness of this repository's own:

```
git ls-files fuzz | head -3
fuzz/WhisperSubtitles.Fuzz/AssemblyInfo.cs
fuzz/WhisperSubtitles.Fuzz/FuzzTargets.cs
fuzz/WhisperSubtitles.Fuzz/Program.cs

git grep -n 'schedule:\|workflow_dispatch:' -- .github/workflows/fuzz.yml
.github/workflows/fuzz.yml:20:  workflow_dispatch:
.github/workflows/fuzz.yml:31:  schedule:
```

The corpus that came out of it is committed under `fuzz/corpus/` and replayed on demand,
and the crashes it has already produced are under `fuzz/reported/`. `#82` is the issue
that owns all of it.

So the score is correct about the integrations it recognises and wrong as a statement
about this repository, and raising it would mean adopting one of those integrations for
the score rather than for the coverage. Not done, and the reason is that one.

THE DISMISSAL BESIDE THIS ENTRY SAYS THE OPPOSITE, AND IT IS THE HALF THAT IS WRONG. The
alert for this finding was dismissed with a sentence saying there is no untrusted input
surface here that a fuzzer would reach. Read it rather than taking this paragraph for it:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&per_page=100" \
  --jq '.[] | select(.rule.description == "Fuzzing") | .dismissed_comment'
```

This repository names the input it does not control in a document of its own,
`docs/untrusted-input.md`, and the harness above reaches two of those surfaces: the reader
that parses what a local tool printed, and the reader that parses what a remote endpoint
answered. Both are targets rather than an intention:

```
git grep -nE 'WhisperOutputReader|TranscriptionResponseReader' -- fuzz/
fuzz/WhisperSubtitles.Fuzz/FuzzTargets.cs:66:        var reader = new WhisperOutputReader();
fuzz/WhisperSubtitles.Fuzz/FuzzTargets.cs:101:        var read = TranscriptionResponseReader.TryRead(
fuzz/WhisperSubtitles.Fuzz/SegmentProperties.cs:39:        TimeSpan.FromSeconds(TranscriptionResponseReader.SecondsCeiling);
```

The other half of that sentence, that onboarding an external service needs maintainers who
can be paged, is untouched by any of this and is the part the disposition rests on.

WHAT IS RECORDED RATHER THAN REPAIRED, AND WHY. The dismissal is text on the tab, nothing
in this tree writes it, and the reasoning it gives for itself lives somewhere this
document does not reach. So the disagreement is written down here, where the record this
repository keeps is, and rewording the other one is a decision about somebody else's
record of their own reasoning. `#53` is where that is held. The disagreement has been read
twice on that issue and this is the first time it is in the repository.

## Pinned-Dependencies, score 9

Reported against one line, with the warning that the NuGet command is not pinned by
hash. Read out of the run's own document, which is the population this page is organised
by:

```
jq -r '([.runs[].tool.driver.rules[] | {key: .id, value: .name}] | from_entries) as $names
  | .runs[].results[] | select($names[.ruleId] == "Pinned-Dependencies")
  | "\(.locations[0].physicalLocation.artifactLocation.uri):\(.locations[0].physicalLocation.region.startLine) \(.message.text | split("\n")[0])"' results.sarif
.github/workflows/scan-codeql.yaml:97 score is 9: nugetCommand not pinned by hash
```

This entry used to read the same thing off the code-scanning tab, filtered to open
alerts. That command returns nothing now, because the alert there was dismissed, and a
reader running it meets an empty result under a heading claiming a finding. The tab is
the other population the second section of this page separates, and it was never the one
this heading is organised by.

Owed rather than accepted, and this is the one entry on this page that names work
instead of a reason for leaving something alone. The actions this repository calls are
pinned by commit with a version comment, which is what carries the other nine tenths of
the score.

The half of it that was missing is here now. The plugin's graph is written down, for
both server lines, and the project asks for the file to be kept:

```
git ls-files | grep -c 'packages.lock.json'
1

git grep -n 'RestorePackagesWithLockFile' -- Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles.csproj
Jellyfin.Plugin.WhisperSubtitles/Jellyfin.Plugin.WhisperSubtitles.csproj:22:    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
```

That does not make this finding green, and the distinction is the whole of what is
left here. The finding is about a command rather than about the repository: the audit
reads the restore in a workflow and asks whether that invocation is pinned, and the
one it points at is not in locked mode. So the score moves when a route reads the
file, not when the file exists.

```
git grep -nE '^ +dotnet restore' -- .github/workflows/
.github/workflows/publish.yaml:295:          dotnet restore "${project}" --locked-mode
.github/workflows/scan-codeql.yaml:97:          dotnet restore Jellyfin.Plugin.WhisperSubtitles.sln
```

The second of those is the line the finding names. It restores the solution, which is
four projects, and the lock file covers one of them:

```
grep -oE '[A-Za-z.]+\.csproj' Jellyfin.Plugin.WhisperSubtitles.sln | sort -u
Jellyfin.Plugin.WhisperSubtitles.csproj
Jellyfin.Plugin.WhisperSubtitles.Tests.csproj
PullRequestHygiene.csproj
WhisperSubtitles.Fuzz.csproj
```

So pointing that restore at locked mode is a change about three more graphs, none of
which ships, and it is not the one-word edit it looks like.

The line the finding points at arrived with `#174`, which took this repository's code
scanning off a shared workflow and gave it a build of its own. That build restores, so
the restore became visible to the audit; the absence it made visible is older than the
change and belongs to no part of it.

What the file did end is on the other route. `publish.yaml` refuses to build without
it, so a publish run started before it existed stopped at that step before reaching
anything it exists to do. That is recorded on `#59` from the other side. One thing
about that route is worth writing here rather than leaving as an assumption: locked
mode refusing a lock file that disagrees with the project is the property the release
rests on, and it has not been watched happen in this repository. What was tried and
what it did is on `#53`.

## CII-Best-Practices, score 0

Reported as no effort to earn an OpenSSF best practices badge detected.

Accepted, and not pursued. The badge is a separate self-certification programme that a
project applies to and answers a questionnaire for, and no part of this repository's
gate depends on it. Nothing in the tree raises this score.

## What stopped being reported, and why

`Security-Policy` had a heading here at score 9. It recorded that the file the check found
was the organisation's rather than this repository's, and that reading the nine as an
answer this repository had given was the specific mistake the entry existed to prevent.

That is no longer the position, because this repository answered:

```
git log --oneline --diff-filter=A -- SECURITY.md
dc57a5a Add a security policy that says what a vulnerability is in this repository (#209)
```

The audit reports no finding under that rule now. The rule is still one the run declares,
which is what separates a check that had nothing to warn about from a check that was
never asked:

```
jq -r '[.runs[].tool.driver.rules[].name] | any(. == "Security-Policy")' results.sarif
true
jq -r '([.runs[].tool.driver.rules[] | {key: .id, value: .name}] | from_entries) as $names
  | [.runs[].results[] | $names[.ruleId]] | any(. == "Security-Policy")' results.sarif
false
```

What that does not say is which score it carries now. A rule with no result is a rule this
run raised nothing under, and this document does not separate that from a check the tool
could not judge, so no number is claimed here. The tab is the one that treats it as
answered, and it closed its alert on the run that first read the new file:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&per_page=100" \
  --jq '.[] | select(.rule.description == "Security-Policy") | "\(.state) \(.fixed_at)"'
fixed 2026-08-19T22:36:17Z
```

The heading had to go, because a heading for a finding the run does not report is one of
the three things the comparison refuses, and it refused this page for exactly that. What
the heading held was the reasoning, which the comparison has no opinion about and which
deleting the heading alone would have thrown away, so it is written out here instead.
`#85` is where the rest of what that policy was part of is held.

## What reads this, and what it does not read

Something reads it now, and this section said nothing did. That was true and the reason
given for it was about the suite: the finding list comes from an API call, and every test
in this repository runs with the machine offline, which is the rule `CONTRIBUTING.md`
holds the suite to. It is not true of the audit itself. The run that produces the findings
already holds them, as the SARIF it uploads, so the comparison is a file against a file
and reaches nothing.

`.github/scripts/refuse-an-undisposed-finding.sh` makes it, in the audit's own job after
both uploads, and it refuses three things: a finding this run reported with no heading
here, a heading here for a finding this run did not report, and a heading whose score is
not the score the run gave. Each was proved by breaking it against the fixture pair in
`.github/fixtures/scorecard-dispositions/`, and the fixture is used rather than this page
because proving it on the real record means making the real record wrong for a moment.

Three things it does not do, and they are what a reader of this page still carries.

It has no opinion about the reasoning. Whether a disposition is right, or still applies,
is not a comparison any run makes, and a heading whose paragraph has gone stale under a
score that has not moved passes it.

It runs where the audit runs, which is a push to `master`, the weekly schedule and a
change to the ruleset. There is no pull request trigger, so a change that adds a finding
is refused after it lands rather than before, and the notice is a red run on the default
branch.

It reads the document that run produced rather than the code-scanning tab. Those are two
different populations, and the section near the top of this page measures how far apart
they are today: one is what a single run said, the other is what the tab holds open across
runs.
