# Code-scanning rules, and what was decided about each

Every code-scanning rule open on this board ends somewhere. Read and set aside with
the reason written down, or owed a repair, or owed a dismissal, or already decided on
another page. A rule with nothing behind it is a number on a dashboard, and a number
nobody reads is the thing this repository is trying not to have.

This page is that record, one entry per rule id. It judges no single alert: what it
holds is the class, the reason, and the population the reason covers.

## Why the register rather than the repairs

The count on this board rises with the suite rather than with the risk. Most guards
here are readers that open a document and compare it against the tree, and each one
needs a repository root joined to a directory and a file name to find what it judges,
so every guard that lands mints alerts of the same shape.

Both other ways out were tried and priced. Dismissal was run three times, and every
alert open at the last reading postdated the last pass. The repair was priced three
times, each price larger than the last, and the direction of that number is the
argument rather than the number:

```
git grep -c 'Path\.Combine' -- 'Jellyfin.Plugin.WhisperSubtitles.Tests/*.cs' \
  | awk -F: '{s+=$NF; f++} END {print s" call sites across "f" files"}'
```

The choice between them was taken on `#296` on 2026-09-04, and the arguments it was
taken on are on `#244`, rule by rule, rather than restated here.

## The four states, and what each one claims

`set aside` is a rule read to its end and accepted as it stands. Nothing is owed and
nothing is pending; the entry is the whole answer.

`repair owed` is a rule whose pattern is to be changed in this tree. The entry names
what the change is. It is a debt rather than an acceptance, and the alerts stay open
while it stands.

`dismissal owed` is a rule where the alert state on the platform is what has to move
rather than the tree. Nothing on a branch changes an alert's state, so the entry
records the reason and leaves the act to whoever holds the tab.

`decided elsewhere` is a rule another page already accepted, named so that this page
is not a second home for the same reason.

A fifth state is a decision nobody took. `.github/scripts/refuse-an-undisposed-alert.sh`
refuses an entry outside these four, refuses a rule the scan reports that no entry
covers, and refuses an entry for a rule the scan no longer reports.

## What the set is, read rather than frozen

No count is written on this page. Every count here moved between one reading of this
board and the next, and a number pasted beside a rule is stale the day after it is
written. The set is read instead:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?state=open&per_page=100" \
  --paginate --jq '.[] | .rule.id' | sort | uniq -c | sort -rn
```

and the entries below are the rule ids that returns. `.github/workflows/code-scanning-dispositions.yml`
runs that fetch on every push to `master`, on every pull request, and weekly, and hands
what it read to the script above. A rule arriving with nothing landing here is refused
by the weekly run.

## cs/path-combine, set aside

`Path.Combine` discards its earlier arguments when a later one is rooted. That is a
real defect where a later argument comes from outside the source, and outside the
source is where none of these sites takes it from.

THE POPULATION IS NO LONGER ALL IN THE TEST PROJECT, AND THIS PARAGRAPH SAID IT WAS.
The population is every open alert of this rule, and one of them is in the shipped
plugin, in `PublishedSubtitleRecordFile`. It arrived after this entry landed, and no
reading is pasted here for the reason the section above gives: the set moves, and a
paste of it ages in silence. Read it:

```
gh api "repos/Flowfin/jellyfin-plugin-whisper-subtitles/code-scanning/alerts?state=open&per_page=100" \
  --paginate --jq '.[] | select(.rule.id=="cs/path-combine") | .most_recent_instance.location.path' \
  | cut -d/ -f1 | sort | uniq -c
```

WHAT THAT DOES NOT MOVE IS THE DISPOSITION, and the two are worth separating. The
reason below is about what a site's later argument is rather than about which project
the site lives in, and it covers the new one: the later argument there is `FileName`,
a `const string` on the same class, and the directory it is joined to is the one the
server hands this plugin. What the wrong sentence cost is a reader who took "all of
them are in the test project" for a bound on the reach of this entry, and went on
believing the shipped plugin was outside it.

At every site the later argument is a relative string literal, a relative constant
naming a directory or a file in this tree, or a fixture name the test itself supplies.
One site takes text out of a document, in `ReleaseChecklistTests`, and the name it
takes is already filtered by an anchored expression that cannot match a rooted first
segment. That last one is named rather than counted, because the safety there is
incidental to a neighbouring filter rather than local to the call, and it stops holding
the day that filter is loosened.

What this entry does not do is take the rule out of view. It stays on, and it stays on
over the shipped plugin, where there is a site that does take a name from a backend:

```
git grep -n 'A_rooted_name_is_refused_rather_than_followed' -- Jellyfin.Plugin.WhisperSubtitles.Tests/SubtitleDestinationTests.cs
```

That site is the one this plugin's containment check was built against, so a filter or
a disabled rule would take the next real one out of view along with these.

## What this page does not do

It judges no individual alert. An entry is about a rule and the class of site it points
at here, and a site inside that class that is genuinely wrong is not separated from its
neighbours by anything on this page.

It closes nothing, AND IT NO LONGER CARRIES A DEBT, WHICH THIS PARAGRAPH USED TO
SAY IT DID. Two entries stood here as `repair owed`, each held by an issue opened
on this board on 2026-09-06; both repairs landed, the mainline scan closed their
alerts, and the entries left with them, because this register refuses an entry for
a rule the scan no longer reports. A `repair owed` entry that arrives tomorrow is
still a debt rather than a repair, and a reader who takes one for the other is
still reading the opposite of what it says.

It is not a suppression. No rule here is filtered, disabled, or narrowed in scope, and
every one of them goes on reporting against the shipped plugin as well as against the
suite.
