# Fixtures the one-format reader has to refuse

Each file here is a version of `docs/limits.md` somebody could plausibly write,
and each one breaks exactly one thing `SubtitleFormatLimitTests` reads out of
that page: the entry naming the format limit disappearing, the entry keeping the
claim while losing the one token a reader could compare, and the entry promising
a format the writer in this tree does not produce.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown. The
extension is checked rather than trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**. `clean.md.fixture` is the
neighbour that has to stay accepted; without it a reader that refused every page
would pass every leg above.

Two of them carry an extension in a DIFFERENT entry on purpose, and that is the
part with a history rather than a convention. The reader is scoped to one entry,
and the first version of these fixtures could not tell that from a reader scoped
to the whole page: with the scoping deleted, so that the extension was matched
anywhere in the file, every leg here stayed green. That was found by deleting it
and running them, which is the only reason it is not still true. Both fixtures
now put a `.srt` outside the entry under test, so a reader that stopped looking
inside the entry finds the wrong one and the leg goes red.

One thing this directory cannot hold, and it is the leg that matters most. The
census of format writers reads a compiled assembly rather than text, so no page
put in front of it moves the answer. It was proved by adding a second
implementation of the writer interface under `Output/`, watching the leg go red,
and taking it out again; that run is in the pull request that landed this rather
than in a file here, and there is nothing in this directory that would catch its
deletion.
