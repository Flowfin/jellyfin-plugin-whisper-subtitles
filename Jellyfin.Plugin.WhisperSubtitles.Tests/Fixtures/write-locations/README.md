# Fixtures the write-location reader has to refuse

`WriteLocationsTests` reads two sections of `docs/limits.md` against the sources
that put things on a disk: the list of what this plugin writes and where, and the
section saying what removing the plugin leaves behind. Each file here breaks
exactly one thing it checks.

The documents are versions of that page somebody could plausibly write. One lists
all three kinds and then answers for two of them on the way out, which is the
uninstall section written from memory. One answers for three and lists two, which
is what a change to the code produces when the page is not read again afterwards.
One accounts for every kind in both sections and then says the plugin removes
nothing at all, which is what the page said while four of its sources took a file
off a disk. `clean.md.fixture` is the neighbour that has to stay accepted, without
which a reader that found nothing in either section would pass every leg.

The three sources are the other half. One writes a record of its own beside the
plugin's other state, which is useful, plausible and a location the page never
learned about. One opens a file and sends it and creates nothing, which is what
the remote backend does with the extracted audio and is the near miss a rule as
coarse as the word file would fail. One names a write in its prose and makes none,
which the plugin also does, at the seam that explains why removing a file is not
the framework method it wraps.

They carry `.md.fixture` and `.cs.fixture` rather than plain extensions because
they are a page about this repository that is deliberately untrue and sources
claiming writes this plugin does not make, and a plain extension puts them in
front of anything that walks the tree for markdown or compiles what it finds. The
extension is checked rather than trusted, in the leg that reads this directory.
