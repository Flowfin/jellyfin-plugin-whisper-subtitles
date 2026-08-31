# Fixtures the page-reader census has to judge

Each file here is a release checklist somebody could plausibly write, read against a
fabricated set of two pages rather than against the real tree. The fabricated set is
what makes a fixture a fixture: `docs/alpha.md`, `docs/beta.md` and `docs/gamma.md`
are names no file in this repository carries, so a leg here cannot pass or fail for
something that happened to land under `docs/`.

They carry `.md.fixture` rather than `.md`, because they are documents about this
repository that are deliberately untrue and a plain extension puts them in front of
everything that walks the tree for markdown. The extension is checked rather than
trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause is
proven by a case that trips it **and no other**. Each refusing page therefore also
names a page correctly, so a reader that refused every backticked word would fail the
equality rather than pass the refusal.

`clean.md.fixture` is the neighbour that has to stay accepted.

`leaves-a-page-out.md.fixture` is the direction that costs. A page the suite reads
and the checklist does not name is a reader whose disappearance nobody would notice
from the checklist, which is the state the item was in: the count said four and the
suite read eight.

`names-a-page-nothing-reads.md.fixture` is the other direction. The checklist credits
a page with a reader it has not got, and the release item then rests on a guard that
does not exist.

`speaks-of-the-readers-without-naming-one.md.fixture` is the shape the real item
carried, word for word. A count with a list of reader class names reads as correct
against every population there could be, which is exactly why it says nothing, and it
is why a missing sentence is refused rather than read as an empty list.
