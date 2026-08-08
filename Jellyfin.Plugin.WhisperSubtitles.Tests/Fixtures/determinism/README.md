# Fixtures the determinism scan has to refuse

Each file here is a test somebody could plausibly write, and each one breaks
exactly one of the rules in `DeterminismTests`. They carry the extension
`.cs.fixture` so the compiler never sees them and the scan never counts them as
sources, and `DeterminismTests` reads them to prove each rule bites.

A fixture per rule rather than one file breaking all of them, because a rule is
proven by a case that trips it **and no other**. One file breaking three rules
cannot tell a scan that refuses everything from one that refuses the right thing.

`clean.cs.fixture` is the neighbour that has to stay accepted. Without it a scan
that refused every file would pass every leg here.
