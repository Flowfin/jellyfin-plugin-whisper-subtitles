# Scorecard findings, and what was decided about each

The Scorecard workflow is a self-audit and not a gate. `.github/workflows/scorecard.yml`
says so at the top and `#49` keeps it out of the required set, because the score moves
for reasons that have nothing to do with any one pull request.

What makes it worth running anyway is that every finding it reports ends somewhere.
Fixed, or accepted with the reason written down. A score with no disposition behind it
is a badge, and a badge is the thing this repository is trying not to have.

The findings are read rather than remembered:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&state=open&per_page=100" \
  --jq '.[] | "\(.rule.description) \(.rule.security_severity_level)"'
Pinned-Dependencies medium
CII-Best-Practices low
Maintained high
Code-Review high
Security-Policy medium
Fuzzing medium
Branch-Protection high
```

Seven, and they are the seven below, taken worst first here rather than in the order the
API answers in. The audit now runs on each push to `master` rather than on the weekly
schedule alone, so the list is re-read rather than dating from one run:

```
gh run list --workflow scorecard.yml --limit 20 --json event,headBranch,conclusion,headSha \
  --jq '.[] | "\(.event) \(.headBranch) \(.conclusion) \(.headSha[0:7])"'
push master success 9014814
push master success 94fbe65
push master success 4a7eebc
push master success d8a8fb9
push master success 617e3a9
push master success 6234f7f
push master success b6b43c1
push master success 88456c7
schedule master success f14ff3c
```

Six of the seven were the whole list when this record was written. The seventh arrived
on its own, with a change that was about something else, and nothing said so. That is
the closing section of this page happening rather than being predicted, and the entry
for it says which change produced it.

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
{"bypass":[],"required":["call / build","call / test","Reject Trojan Source Unicode"]}
```

`#54` is where the gate is tightened and where each of these five is either taken or
refused with a reason. One of them has a prerequisite in the tree rather than in the
settings: codeowners review cannot be required while there is no codeowners file, and
that file is `#85`.

The half of this check that is already answered is worth separating from the half that
is not. The ruleset has no bypass actors, so what it does require, it requires of
everybody.

## Security-Policy, score 9

Reported as a security policy file detected, with one warning that it carries one or no
descriptive hint of disclosure, vulnerability or timelines.

Recorded rather than fixed, and the reason is that the file scoring nine is not in this
repository:

```
git ls-files | grep -i security ; echo "exit=$?"
exit=1

gh api repos/Flowfin/.github/contents --jq '.[].name'
.github
SECURITY.md
profile
```

The policy the check found is the organisation's, inherited by every repository under
it. That is a real policy and a reporter following it reaches somewhere, so this is not
a hole. What it is not is an answer this repository gave, and `#85` is where the
question of a policy of its own is held. Reading the nine as this repository having
answered is the specific mistake this record exists to prevent.

## Code-Review, score 0

Reported as nought of fourteen approved changesets.

Accepted, and it is the same setting as Branch-Protection rather than a second thing.
The check counts approving reviews on the pull requests it sampled and found none, and
whether an approving review becomes a condition of merge is the ruleset above, which is
`#54`. Nothing in the tree moves this number.

Left as a finding rather than dismissed. A repository whose changes carry no approving
review is a fact about that repository, and the disposition for it is that the decision
is open and named, not that the finding is wrong.

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

## Pinned-Dependencies, score 9

Reported against one line, with the warning that the NuGet command is not pinned by
hash:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?tool_name=scorecard&state=open&per_page=100" \
  --jq '.[] | select(.rule.description == "Pinned-Dependencies") | "\(.created_at) \(.most_recent_instance.location.path):\(.most_recent_instance.location.start_line) \(.most_recent_instance.message.text | split("\n")[0])"'
2026-08-11T22:33:49Z .github/workflows/scan-codeql.yaml:97 score is 9: nugetCommand not pinned by hash
```

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

It reads the document that run produced rather than the code-scanning tab. The command at
the top is still the route for a reader, and the two are different populations: one is
what a single run said, the other is what the dashboard holds open across runs.
