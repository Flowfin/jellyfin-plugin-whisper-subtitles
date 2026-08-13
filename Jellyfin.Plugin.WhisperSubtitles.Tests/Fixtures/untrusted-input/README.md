# Fixtures the untrusted-input reader and scanner have to refuse

Two families, because the checks beside them fail differently.

The `.md.fixture` files are versions of `docs/untrusted-input.md` somebody could
plausibly write, each breaking exactly one thing the reader checks: an entry
naming a type this plugin does not carry, an entry naming a hostile case the suite
does not run, an entry naming neither, and a section with no entries left in it.
Each one ends with a second heading, so a reader that ran to the end of the file
is visible here rather than only against the real document.

The `.cs.fixture` files are sources the scanner has to object to, one per rule: a
process started outside the injected runner, a program handed one string instead
of a vector, an HTTP client built outside the backend that owns the endpoint, and
a media tool this plugin chose for itself rather than the one the server reports.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**. A file breaking every rule
cannot tell a scan that refuses the right thing from one that refuses everything.

`clean.md.fixture` and `clean.cs.fixture` are the neighbours that have to stay
accepted. Without them a reader that refused every section and a scanner that
refused every file would pass every leg above.

`takes-the-media-tool-it-was-given.cs.fixture` is the closer neighbour, and it
exists because the distant one is cheap to pass. It is
`finds-its-own-media-tool.cs.fixture` with the path arriving as an argument and
nothing else changed, so a rule coarse enough to refuse any source that reaches a
media tool at all fails here while its own fixture still says it works.

Neither family carries a plain extension. A `.md` here would be a second boundary
list saying things about this repository that are deliberately untrue, in front of
anything that walks the tree for markdown, and a `.cs` would be a permanently red
scan with no legal repair. The extensions are checked rather than trusted, in the
leg that reads this directory.
