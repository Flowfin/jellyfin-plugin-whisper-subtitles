# Fixtures the refused-tests reader has to refuse

Each file here is a version of the contributor guide's list of refused tests that
somebody could plausibly write, and each one breaks exactly one thing
`RefusedTestsTests` checks. Which ones exist is the listing of this directory
rather than a sentence here, because a list in a document drifts against the
thing it describes and this one already had.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown. The
extension is checked rather than trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**. Each fixture ends with a
second heading, so the reader has something to stop at and a reader that ran to
the end of the file is visible here rather than only against the real guide.

`clean.md.fixture` is the neighbour that has to stay accepted. Without it a
reader that refused every section would pass every leg.
