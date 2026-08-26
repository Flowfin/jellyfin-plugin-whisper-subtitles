# Fixtures the named-check reader has to judge

Each file here is a page somebody could plausibly write about this repository, and
each one turns exactly one thing `NamedChecksTests` reads out of a page: a check
named by the title of the workflow file instead of by the job, a composed name
whose first half runs steps of its own and therefore never reports a composed
context, a composed name that is correct, and a name wrapped across a line break.

They carry the extension `.md.fixture` rather than `.md` because they are documents
about this repository that are deliberately untrue, and a plain extension puts them
in front of anything that walks the tree for markdown. The extension is checked
rather than trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause is
proven by a case that trips it **and no other**. Each refusing fixture therefore
also carries a name that is correct, so a reader that refused every backticked word
would fail the equality rather than pass the refusal.

`clean.md.fixture` is the neighbour that has to stay accepted. Without it a reader
that refused everything would satisfy every refusal leg here and say nothing about
the real pages.

`names-the-workflow-instead-of-the-job.md.fixture` is the defect the class exists
for, in the shape it had on `docs/RELEASING.md`: the workflow file's title where the
job's own name belongs. Nothing reports under a workflow title, so a ruleset entry
carrying one waits for a run that is never created, and the branch it guards stops
merging rather than merging slowly.

`names-a-called-workflows-check.md.fixture` is why the reader knows about the
composed shape at all. A job that hands its work to a workflow in another repository
reports under `job / inner`, the inner half is declared where this tree cannot read
it, and two of the contexts this repository requires today are of that shape.

`composes-a-name-off-a-job-that-calls-nothing.md.fixture` is the near miss beside
it, and the reason a composed name is not simply accepted on its shape. A job that
runs steps of its own reports one context with no slash in it, so a page composing a
name off such a job describes a check that does not exist while looking exactly like
the fixture above.

`wraps-the-name-across-a-line-break.md.fixture` is a bound written down as a case
rather than a property. The reader takes a name on one line, so this page passes
while naming a check nothing reports. Widening the pattern across line breaks is not
the repair: it would take a backtick opened in one paragraph and closed in the next
as a check name, which is a refusal nobody can act on. Keeping the name on one line
is.
