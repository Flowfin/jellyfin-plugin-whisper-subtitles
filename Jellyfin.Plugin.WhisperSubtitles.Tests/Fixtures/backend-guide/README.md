# Fixtures the backend guide reader has to refuse

Each file here is a version of `docs/choosing-a-backend.md` somebody could
plausibly write, and each one breaks exactly one thing `BackendGuidePageTests`
reads out of that page: a value offered that this plugin answers to under no
name, a value this plugin answers to that the table leaves out, a table of
backend-shaped values under a heading the reader was never given, and a section
whose rows are gone.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown. The
extension is checked rather than trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**.

`clean.md.fixture` is the neighbour that has to stay accepted. Without it a
reader that returned nothing whatever it was shown would satisfy the two refusal
legs and say nothing about the real page.

`a-table-in-another-section.md.fixture` is the near miss and the reason the
reader is bounded by a heading at all. Its first table is correct and a second
table further down lists a backend that does not exist, in the same shape. A
reader that matched rows over the whole page rather than over the section it was
pointed at accepts the real page today and refuses it the day somebody adds a
comparison table, which is the failure that arrives long after the check is
written and looks like the page's fault.

`no-table-at-all.md.fixture` is the vacuity case. It keeps the heading and loses
the rows, which is what a reformat or a rename leaves behind, and a reader with
no floor under it reports a clean page because there is nothing on the page side
to disagree with the code.
