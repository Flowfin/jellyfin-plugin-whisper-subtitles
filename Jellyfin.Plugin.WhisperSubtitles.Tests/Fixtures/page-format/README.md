# Fixtures the page-format check has to refuse

Each file here is a configuration page somebody could plausibly write, and each
one breaks exactly one of the rules in `ConfigurationPageFormatTests`. They carry
the extension `.html.fixture` rather than `.html` so nothing walking the tree for
pages finds a page that is deliberately misformatted, and the extension is
checked rather than trusted, in the leg that reads this directory.

A fixture per rule rather than one file breaking all of them, because a rule is
proven by a case that trips it **and no other**. A file breaking three rules
cannot tell a check that refuses the right thing from one that refuses
everything it is shown.

The differences from `clean.html.fixture` are one edit each, and they are the
edits a hand change to a page actually makes: an indent taken from a different
editor, a line pushed two levels in one step, a space left at the end of a line,
a blank line duplicated, and a script body that drifted out from under its own
tag.

`clean.html.fixture` is the neighbour that has to stay accepted. Without it a
check that refused every page would pass every leg above. It carries a script
block and nesting several levels deep, because a neighbour with neither would be
accepted by rules that never looked at anything, and that is asserted rather
than assumed.
