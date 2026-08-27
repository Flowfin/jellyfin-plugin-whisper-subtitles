# Fixtures for the release checklist reader

Every file here is a release checklist, or a page beside one, that is deliberately
wrong in exactly one way. `ReleaseChecklistTests` reads each of them and has to
refuse it for the reason its name gives, so the proof that a leg bites is in the
tree rather than in the memory of whoever last broke the real page on purpose.

None of them describes this repository. `clean.md.fixture` is the neighbour that
breaks no rule and it exists so that a reader refusing every item cannot pass every
leg by refusing this one too.

The extension is the whole of what keeps these out of the way of anything that
walks the tree for markdown, and one leg of the suite refuses a fixture here that
acquired a plain one. The file you are reading is the one document in this
directory that is true, so it is excluded by name.
