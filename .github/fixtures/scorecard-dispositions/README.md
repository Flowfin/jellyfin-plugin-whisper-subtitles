# Two findings and the page that decided about them

`.github/scripts/refuse-an-undisposed-finding.sh` compares what a Scorecard run
reported against `docs/scorecard-dispositions.md`. Proving that it bites means
feeding it a pair that disagree, and doing that to the real page and the real run
would mean making the repository wrong for a moment.

Two findings rather than one, because a set of one cannot tell an entry that was
dropped from a page that records nothing at all. They sit in two `runs` blocks
because the document the audit uploads groups its rules that way, and a reader
that only looked in the first block would find one of the two and call the other
undisposed.

`Token-Permissions` is declared as a rule and is not a result. A run declares
every rule it knows and reports only the findings, so a reader that took the rule
list for the finding list would refuse the real page for four checks that passed.

The disagreeing pages are not kept here. Each one is the fixture page above with a
single change made where the check runs, so what the change was is readable beside
what it did rather than in a file somebody has to diff.
