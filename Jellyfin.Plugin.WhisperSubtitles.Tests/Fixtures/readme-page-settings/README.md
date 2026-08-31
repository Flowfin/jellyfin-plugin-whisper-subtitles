# Fixtures the README's settings list has to be read against

Each file here is a version of the step in `README.md` that tells a reader which
settings the configuration page carries, and each one breaks exactly one thing
`ReadmePageSettingsTests` reads out of it: a setting the page carries that the
list leaves out, a setting the list names that the page carries under no name, a
sentence that speaks of the settings and names none of them, and a step with no
such sentence at all.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**.

`clean.md.fixture` is the neighbour that has to stay accepted. Without it a
reader that complained about whatever it was shown would satisfy the refusal legs
and say nothing about the real page.

The last of the five is the one worth reading twice. A README that carries no
such sentence is refused rather than read as a list of nothing, because deleting
the sentence would otherwise be the cheapest way to make this file green, and a
document that has stopped saying anything about the page is exactly the state the
reader exists to notice.

The settings these are compared against are fixed in the test rather than read
from the page, so a fixture leg is about the README it varies and never about
what the tree happens to hold on the day it runs.
