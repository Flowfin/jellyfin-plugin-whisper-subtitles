# Fixtures for the ABI floor context reader

Every file here is a release page, or an ABI floor workflow beside one, that is
deliberately wrong in exactly one way. `AbiFloorContextsTests` reads each pair and
has to refuse it for the reason its name gives, so the proof that a leg bites is
in the tree rather than in the memory of whoever last broke the real page on
purpose.

None of them describes this repository. `clean.md.fixture` with
`two-jobs.yml.fixture` is the pair that breaks no rule, and it exists so that a
reader refusing every pair cannot pass every leg by refusing this one too.

The extensions are the whole of what keeps these out of the way of anything that
walks the tree for markdown or for workflows, and one leg refuses a fixture here
under any other. The file you are reading is the one document in this directory
that is true, so it is excluded by name.
