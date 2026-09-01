# Fixtures the backend interface page reader has to refuse

Each file here is a version of `docs/backend-interface.md` somebody could
plausibly write, and each one breaks exactly one thing `BackendInterfacePageTests`
reads out of that page: a guard the section leaves out, a guard named there that
judges nothing, a section that speaks of the guards without naming one, and a
page carrying no such section at all.

Every leg that uses one of these is judged against a fabricated pair of class
names rather than against the classes this test project actually holds, so no
leg here can pass or fail because a guard landed beside a real surface.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown. The
extension is checked rather than trusted, in the leg that reads this directory.

`clean.md.fixture` is the neighbour that has to stay accepted. Without it a
reader refusing every pair would satisfy each refusal leg below and say nothing
about the real page. It also carries a third class name on a bullet of a later
section, which is the near miss: a reader matching bullets over the whole page
rather than over the section it was pointed at would credit the list with a guard
it never named.

`leaves-a-guard-out.md.fixture` is the direction the real page was wrong in, and
the expensive one. A surface whose guard the page never names is a surface an
author is not told about, which is exactly how somebody adds a backend and meets
a red suite naming a file they had no reason to open.

`names-a-guard-nothing-holds.md.fixture` is the other direction. The page credits
a surface with a guard that no longer judges the vocabulary, and an author reads
a machine as standing behind a file nothing compares.

`speaks-of-the-guards-without-naming-one.md.fixture` is the shape the page would
have taken if the repair had been a sentence rather than a list. It reads as
correct against every population there could be, which is why it says nothing.

`no-section-at-all.md.fixture` is the vacuity case and the state the page was in
before this class landed: the guards exist, the page does not mention them, and a
reader with no floor under it reports a clean page because there is nothing on
the page side to disagree with the suite.
