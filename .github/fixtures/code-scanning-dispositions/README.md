# Two rules and the register that decided about them

`.github/scripts/refuse-an-undisposed-alert.sh` compares the open code-scanning
alerts against `docs/code-scanning-dispositions.md`. Proving that it bites means
feeding it a pair that disagree, and doing that to the real page and the real
alert set would mean making the repository wrong for a moment.

Two rules rather than one, because a set of one cannot tell an entry that was
dropped from a register that records nothing at all.

Three alerts under two rule ids, because the register keys on the rule and never
on a count, and a fixture with one alert per rule would pass a check that had
quietly started comparing counts.

The two disposition states here are two of the four the page declares. The other
two are exercised where a wrong one is: the refusal leg edits this file to a state
the register does not declare, so what the change was is readable beside what it
did rather than in a file somebody has to diff.

The disagreeing registers are not kept here, for the same reason.
