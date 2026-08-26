# Fixtures the release-branch reader has to judge

Each file here is one side of the comparison `ReleaseBranchesTests` makes: a page
somebody could plausibly write about this repository, or a publish workflow somebody
could plausibly leave behind. A leg pairs one of each, so a case is a page and the
branch list it is read against rather than a file on its own.

The pages carry `.md.fixture` and the workflows carry `.yaml.fixture` rather than a
plain extension, because they are documents about this repository that are deliberately
untrue and a plain extension puts them in front of everything that walks the tree for
markdown or for workflows. The extension is checked rather than trusted, in the leg that
reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause is
proven by a case that trips it **and no other**. Each refusing page therefore also names
a branch correctly, so a reader that refused every backticked word would fail the
equality rather than pass the refusal.

`clean.md.fixture` against `two-release-branches.yaml.fixture` is the neighbour that has
to stay accepted. Without it a reader refusing every pair would satisfy each refusal leg
here and say nothing about the real files.

`leaves-a-release-branch-out.md.fixture` against the same workflow is the direction that
costs. The publish run allows a tag from a branch the page never names, so whoever
configures the gate from this page covers one branch of two and the page reads as
complete. A release then comes off a branch nothing checked.

`names-a-branch-no-release-is-cut-from.md.fixture` against
`one-release-branch.yaml.fixture` is the other direction. It asks for the gate on a
branch no release comes from, which is how a required set acquires an entry nobody can
explain and nobody dares remove.

`speaks-of-the-branches-without-naming-one.md.fixture` is the shape `docs/RELEASING.md`
actually had. A phrase where the list belongs reads as correct against every branch list
there could ever be, which is exactly why it says nothing, and it is why a missing
sentence is refused rather than read as an empty list.

`no-branch-list.yaml.fixture` is the same vacuity from the workflow side. A list that
was renamed away leaves the reader comparing a page against nothing, and a page agrees
with nothing perfectly.
