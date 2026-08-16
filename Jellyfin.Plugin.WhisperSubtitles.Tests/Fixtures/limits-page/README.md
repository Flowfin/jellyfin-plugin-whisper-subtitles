# Fixtures the limits page reader has to refuse

Each file here is a version of `docs/limits.md` somebody could plausibly write,
and each one breaks exactly one thing `LimitsPageTests` checks: an entry in
neither of the two states that page keeps apart, an entry naming no issue for a
reader to argue with, an entry naming a file this tree does not have, an entry
naming a suite this assembly does not run, and a page with no entries left in
it.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown. The
extension is checked rather than trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**. Each one ends with the same
closing section the real page ends with, so a reader that counted that section
as a limit is visible here rather than only against the real page.

`clean.md.fixture` is the neighbour that has to stay accepted. Without it a
reader that refused every entry would pass every leg above it.

The first fixture is the one with a history. On 2026-08-13 the real page carried
a paragraph saying stale audio is swept before the next run begins, in the
present tense and in the one section carrying no state marker at all, while
nothing called the sweep. It was found by reading the page against the tree by
hand. `an-entry-in-neither-state.md.fixture` is that shape, and the leg it trips
is the one that would have found it.
