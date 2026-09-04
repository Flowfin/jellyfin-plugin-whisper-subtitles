# Fixtures the label-sync reader has to judge

Each file here is a workflow somebody could plausibly add to this repository, and
each one turns exactly one thing `LabelSyncAbsentTests` reads out of a workflow: a
call to the reusable synchroniser in another organisation, a copy of the action
that reusable workflow runs, prose about the synchroniser with no call to it, and a
workflow that references other things entirely.

They carry the extension `.yaml.fixture` rather than `.yaml` because they are
workflows this repository must not carry, and a plain extension puts them in front
of anything that walks the tree for workflows. The extension is checked rather than
trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause is
proven by a case that trips it **and no other**. Each refusing fixture therefore
also carries references that are ordinary, so a reader that refused every `uses:`
would fail the equality rather than pass the refusal.

`clean.yaml.fixture` is the neighbour that has to stay accepted. Without it a reader
that refused everything would satisfy both refusal legs here and say nothing about
the real workflows.

`calls-the-reusable-label-sync-workflow.yaml.fixture` is the defect the class exists
for, in the bytes it actually had. It is `.github/workflows/sync-labels.yaml` as this
repository carried it until #308, kept here so the shape stays readable after the
file is gone from the tree.

`runs-the-label-sync-action-directly.yaml.fixture` is that defect one layer down, and
the reason the reader knows two spellings rather than one. The reusable workflow runs
`EndBug/label-sync`, and a job that runs that action here deletes exactly the same
labels while naming neither the workflow file nor the organisation the fixture above
names. Its `config-file` is the shape of the one the run of 2026-09-01 fetched: a URL
into another repository's default branch, resolved at run time.

`explains-the-synchroniser-in-a-comment.yaml.fixture` is the near miss, and the reason
the pattern reads a `uses:` value rather than the line. A workflow explaining why it
does **not** sync labels has to be able to say so, and a reader matching the word
anywhere would refuse the comment recording this decision - which is the one place a
later reader looks.
