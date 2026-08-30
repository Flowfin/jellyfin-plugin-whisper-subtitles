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

`a-kind-with-no-state-of-its-own.md.fixture` is the same accident one grain
finer, and it is the shape the fixture above cannot hold. Its entry is a list
rather than a single claim, it carries both spellings of a state, and one of the
three kinds inside it says nothing about which state it is in. So the leg that
asks the question once per heading passes it. That is how a claim about a record
of what the plugin produced stood in the present tense until 2026-08-16, and how
the subtitle file stood in neither state until the leg this fixture proves
landed. Both were found by a person and neither by a check.

It is the one fixture here that carries its own neighbour rather than leaning on
`clean.md.fixture`: the leg that refuses it also asserts that the other two kinds
in the same entry are accepted, because a reader that answered "no state" for
every kind would refuse this fixture for the wrong reason and pass.

`a-way-out-kind-with-no-state-of-its-own.md.fixture` is that same accident in the
other section that lists kinds, the one saying what removing the plugin does not
delete. That section carried a single marker at its end covering three kinds in
one sentence until 2026-08-22, and two of the three were stating unbuilt things
in the present tense underneath it: that removal takes away a record of what the
plugin produced, when nothing writes one, and that temporary audio never
survives a run, when what a process that died mid-run left behind is collected
by nothing. Both had already been repaired one section up and neither was
visible here, because the leg that asks the question once per heading was
answered by the marker at the end.

The proof that the leg bites is the page itself rather than only this fixture:
with the uninstall section put back to the one `origin/master` carried, all
three of its kinds are refused and every other leg stays green, which is the
whole of what the per-heading question could not see. This fixture carries its
own neighbour for the same reason the one above it does.

`a-question-named-as-though-it-were-answered.md.fixture` is a different accident
from the four above it. Its entry is in a state, names an issue, and points at
nothing that has moved; what it does is name #8, which is where the decisions
this plan has not taken are collected, in a sentence saying that issue decided
something. The real page had the quiet half of that until 2026-08-30: the
out-of-process entry rested on #8's first question and named only the issue that
built the branch the plan assumes, while the entry directly above it said in so
many words that the question it rests on is open. Two entries resting on two
questions of one issue, and one of them saying so. It was found by reading each
entry's claim against the issue it names, by hand, and no leg here would have
found it.

The leg that fixture proves refuses the loud half rather than the quiet one, and
the difference is worth knowing before trusting it. It reads a paragraph that
names #8 and asks whether that paragraph also says the question is open, so an
entry that rests on one of those questions and names no issue for it passes.
Whether a limit rests on an unanswered question is a reading of the tracker, and
this suite reaches none.
