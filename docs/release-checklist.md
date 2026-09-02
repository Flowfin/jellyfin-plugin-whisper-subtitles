# Release checklist

A release is the moment every gate in this repository either mattered or did not.
This page is what makes that explicit and reviewable rather than remembered: one
section per condition, and each one names the thing that answers it rather than
asking somebody to be satisfied.

Two states are kept apart at every item, because they are not the same promise.
An item that says **Decided by a run** names a command or a status check whose
verdict is the answer, so the result is recorded by the run rather than typed by
whoever cut the release. An item that says **Nothing decides this yet** has no
such route, names the issue where one is owed, and is a condition somebody has to
answer by hand until that issue lands.

A decided item can still carry a bound, and where it does the bound is written at
the item rather than left for a reader to discover. The two states are about who
produces the answer, not about how much the answer covers.

Where an item writes `<commit>`, that is the commit the tag points at.

## The full merge gate is green on the release commit

Every check this repository runs has finished on the commit being released, and
none of them is red.

Decided by a run. What each check concluded on that commit is what the tracker
reports, and an empty list is the pass:

```
gh api repos/Flowfin/jellyfin-plugin-whisper-subtitles/commits/<commit>/check-runs \
  --jq '[.check_runs[] | select(.conclusion != "success") | .name]'
```

The gate this repository is measured against is #49, and the milestone that
produced these checks is where each one was argued.

What it does not decide: whether any of those names is *required*. That is the
ruleset, which is a repository setting no test in this tree reads, and #54 is
where the comparison between the two is owed. So a check deleted between one
release and the next disappears from the list above rather than failing it, and
the list is what ran and not what had to run.

It also does not decide whether a red name has anything to do with the release. A
job that is red on every mainline commit for a standing reason is red on the
release commit too, and it fails this item exactly the way a regression would. So
read the list rather than assuming which names are in it, and read each red name
against the issue that records it before deciding this item is unanswerable. The
draft the changelog workflow in `.github/workflows/changelog.yaml` builds is the
one this repository stands red on, and #59 is where that is recorded and where
what it waits on is written.

## The interoperability matrix is green on both server lines

The supported set of sibling plugins installed together, on each supported server
line, with the collision scan over the result.

Nothing decides this yet. No matrix exists in this repository, and nothing here
installs a sibling plugin:

```
git grep -nEi 'interoperab|jellyfin-plugin-(sso|requests|stats)' -- .github/workflows/
.github/workflows/claim-collision.yml:44:        run: .github/scripts/refuse-a-claim-collision.sh interoperability/claims
.github/workflows/claim-collision.yml:113:          jq 'del(.taskKeys)' interoperability/claims/jellyfin-plugin-whisper-subtitles.json > incomplete/whisper.json
.github/workflows/claim-collision.yml:127:          jq 'del(.plugin)' interoperability/claims/jellyfin-plugin-whisper-subtitles.json > anonymous/whisper.json
```

That command returned nothing until #64's recording half landed, and the three
lines it returns now are one workflow reading this repository's own claim record
out of `interoperability/claims/`. None of them installs a sibling, boots a
server, or compares this plugin against anything but itself, so the sentence
above is unchanged and only its evidence moved. What a matrix would look like
here is a job that fetches a released sibling and installs it, and there is none.

The harness that boots a server per line is #63, the collision scan is #64, and
the wiring that makes the matrix a standing answer rather than an experiment
somebody once ran is #66. Until those land, this condition is answered by nobody
rather than by hand, and a release cut today ships without it.

A red matrix holds the release until the collision is fixed or the
incompatibility is written down as a known limitation with its reason. That is
the condition the Interoperability milestone states, and #66 is where the refusal
belongs.

## The coverage floor is held

The pure logic named in `.github/coverage/pure-logic.txt` is above the line and
branch floors written in that same file.

Decided by a run. The `The pure logic stays above its floor` check is the verdict,
and `.github/scripts/refuse-uncovered-logic.sh` is what it runs. That script fails
closed in three directions rather than one: a listed file this tree does not have,
a listed file the report never mentions, and a ratio below the floor. Recorded in
#47.

What it does not decide: whether the list names the logic worth covering. A file
nobody added to that list is not measured, and no run reports the omission.

## The package carries no native runtime and no model

The archive an operator installs holds this plugin's assembly and nothing else.

Decided by a run. The `The build carries nothing an operator did not choose` check
reads the files a build produced and refuses a second assembly, a native library
extension, a model extension, or anything over its size ceiling, and the
`And it refuses a build that carries all three` check is that guard proved against
a fixture carrying all three. The script behind both is
`.github/scripts/refuse-shipped-weight.sh`. Recorded in #16, and the same promise
is made to operators in `docs/limits.md`.

## The documentation matches what the build produces

Nothing decides this yet, and what is missing is a decision rather than a check.

The pages read against the tree on every run rather than trusted are
`docs/RELEASING.md`, `docs/backend-interface.md`, `docs/choosing-a-backend.md`,
`docs/limits.md`, `docs/logging.md`, `docs/release-checklist.md`,
`docs/scorecard-dispositions.md`, `docs/subtitle-format.md`,
`docs/troubleshooting.md` and `docs/untrusted-input.md`.

That list is compared against what this repository's test project reads, in both
directions, by `PagesReadOnEveryRunTests`, so a page that gains a reader and a
page that loses one are both a red suite rather than a sentence going quietly out
of date. It replaces a hand count of four, naming four reader classes, which was
wrong about the population by half on the day anybody read it against the tree.

That list and the comparison behind it stop at `docs/`. Documents at the
repository root are read against the tree on every run as well, and this list
names none of them, so what this item rests on is the documentation under
`docs/` rather than every document this repository holds.

None of those readers reads anything the build emits, and that half is a reading
rather than a comparison. So this item is a choice between two readings: either it
is those readers being green on the release commit, which the merge gate item
above already covers and which means this item says nothing new, or it is a
comparison against the published archive, which nothing here makes and which
needs the artefact to exist first.

Which of the two it is belongs in #62, because an item naming the weaker one while
its words promise the stronger one is the failure this checklist is written
against.

## The changelog is not the placeholder

The manifest the catalogue reads carries a changelog describing this release.

Decided by a run, in the narrow sense that the value is a reading of one field of
one tracked file:

```
git show <commit>:build.yaml | sed -n '/^changelog:/,$p'
```

The condition is that what comes back is not the word `changelog`, which is what
the template shipped and what `build.yaml` still carries. On the way in, the rule
`version-bump-carries-the-changelog` in `checks/pr-hygiene/HygieneRules.cs`
refuses a change that moves the manifest's version without moving its changelog,
and the `Pull request hygiene` check is where that verdict is reported.

What it does not decide: whether the text in that field is what regenerating the
changelog would produce. That is #59, and this item asks for a non-placeholder
value rather than a reproducible one.

## The artefacts and their provenance are published

Decided by a run. The publish run refuses an incomplete set before it writes
anything: it expects exactly one archive and exactly one packaging metadata file,
and it stops if a release already exists for the tag. It signs a build provenance
statement for the archive it produced, in a job that downloads that archive and
runs no build tooling, and a downloaded archive is checked against it:

```
gh attestation verify <archive>.zip --repo Flowfin/jellyfin-plugin-whisper-subtitles
```

What each of those refusals is, one by one, is the "What fails the run" section of
`docs/RELEASING.md` rather than a second copy here. The supply chain conditions
behind the attestation are #53, and the first release this route has to produce is
#130.

## The manifest describes the server lines the release ships

Nothing decides this yet. `build.yaml` is one manifest and it describes the 10.11
line, which is the line with a released server behind it. The second manifest
cannot be written today: it would promise servers from 12.0.0.0 upward while the
build compiles against a release candidate, and
`.github/scripts/read-abi-floor.sh` refuses that shape.
`SecondServerLineManifestTests` reads the pin rather than trusting the paragraph
that explains it, so the day the pin stops being a candidate is the day that
explanation goes red instead of quietly going stale.

The second manifest is #51 and the second artefact is #60. What ends the wait is a
released 12.0 server, which is outside this repository, so this item is answered
by hand at every release until then and the answer is that the release ships one
line.

## The limits page has been re-read against the code

`docs/limits.md` tells an operator what this plugin will not do, and files every
entry either as a limit something holds today or as a decision taken and not yet
built. At a release, an entry in the second state is either built by then and
moves up, or it is still open and says so.

Decided by a run. `LimitsPageTests` refuses an entry that is in neither state, one
that names no issue, one that points a reader at a file this tree does not have,
and one that says a suite refuses it when this assembly runs no such class. It
runs with the rest of the suite on the release commit, and on its own:

```
dotnet test -c Release --filter "FullyQualifiedName~LimitsPageTests"
```

What it does not decide: whether a marker is true. That suite reaches no tracker,
so an entry filed as decided and not yet built whose issue closed yesterday stays
green until a person moves it, and whether a named thing really holds a limit is a
reading rather than a comparison. `docs/limits.md` hands that reading to this page,
and it is the one item here whose remaining half is a person's.

## When an item has no answer

A release is not published while an item above is unanswered. Three of the items
above have no route that answers them at all, so a release cut from this
repository today is one whose checklist is incomplete by construction, and saying
which ones is the point of keeping two states rather than an apology for them.

Nothing enforces that. `.github/workflows/publish.yaml` creates the release from a
pushed tag and reads none of this page, so the last condition of #62 - that a
release cannot be published without every item having a recorded result - is not
built, and this page is a list somebody follows rather than a gate.

What is held on every run is smaller and is worth having separately.
`ReleaseChecklistTests` reads this page and refuses an item that is in neither of
the two states above, an item in the second state that names no issue, an item in
the first state that names neither a command nor a status check, and an item that
points a reader at a file this tree does not have. It also refuses a page under
`docs/` that speaks of the release checklist while no item here names that page,
so a condition handed to this list arrives rather than being remembered.

The figure this section opens with is held as well, and that is why it is written
as a count of the items above rather than as a word in passing. That reader counts
the items in that state and refuses a count naming a different figure, and refuses
a closing section that speaks of them and states no figure at all, so an item that
gains a route and an item added with none are both a red suite. A figure this page
states about itself is the shape this page has already been wrong in twice, both
times in the item above about documentation, and both times the figure was right
on the day it was written.
