# A page that pastes a reading of a tree, and a tree to read

`tree/` is two files, one of each kind the pattern in these pages is about: a run
that reads a claim record and a run that installs a sibling plugin. It is small
and it does not change, which is what a fixture is for. The real subject is
`docs/release-checklist.md` against this repository, and that one moves.

`clean.md.fixture` pastes what the command returns over `tree/`, and the reader
accepts it. Everything else is that page with one thing changed:

- `a-line-the-paste-leaves-out.md.fixture` drops one returned line, which is the
  drift that reads as a smaller answer than the tree gives.
- `a-line-the-command-does-not-return.md.fixture` adds a line for a file that is
  not there, which is the drift that reads as a larger one.
- `a-paste-claiming-nothing-while-lines-exist.md.fixture` replaces the output
  with `exit=1`. That is the spelling this page uses for a command that returned
  nothing, and it is the exact state `docs/release-checklist.md` carried until
  the workflow in #64 landed, so it is the one worth having a fixture for.
- `a-paste-of-nothing-that-is-right.md.fixture` is `exit=1` under a pattern that
  really does match nothing, and the reader accepts it. Without that leg the one
  above is satisfied by a reader that refuses every `exit=1` it meets, which
  would refuse the honest empty reading too.
- `a-command-this-reader-cannot-run.md.fixture` writes the command in a spelling
  this reader does not parse. It is refused rather than skipped, because a paste
  nothing re-runs is what this exists against, and a reader that walks past the
  commands it does not understand grows a silent exemption every time somebody
  writes one differently.
