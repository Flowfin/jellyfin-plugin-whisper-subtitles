# Two conditions and the section that is about them

`.github/scripts/refuse-a-release-with-an-unanswered-item.sh` reads
`docs/release-checklist.md` before the publish run builds anything. Proving that it
bites means feeding it a checklist that is deliberately wrong, and doing that to
the real page would mean making the repository wrong for a moment.

Two conditions rather than one, because a set of one cannot tell an item that lost
its answer from a page that carries no item at all.

One in each state, because the two are answered differently: a run's verdict
answers the first, and a limitation written down with its reason answers the
second, and a check that had quietly started asking for only one of the two would
pass a fixture that held only that one.

The closing section is here for the direction nothing else would catch. It is
decided by no run and carries no answer, so a reader that did not stop at it would
refuse every release for a section that decides nothing - and it would do that on
the real page too.

The wrong checklists are not kept here. Each is this file with a single change made
where the run makes it, so what the change was is readable beside what it did.
