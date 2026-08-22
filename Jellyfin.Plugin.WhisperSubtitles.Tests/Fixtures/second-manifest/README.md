# Fixtures the second-manifest reader has to refuse

Each pair here is a `Directory.Build.props` and a `build.yaml` somebody could
plausibly write, and each one breaks exactly one thing
`SecondServerLineManifestTests` checks: a line left undescribed whose server has
since been released, a paragraph that never says what the missing manifest would
promise, a paragraph that never names the package the undescribed line compiles
against, and a tree with nothing left undescribed for that paragraph to be about.

They carry `.props.fixture` and `.yaml.fixture` rather than the plain extensions
because they are descriptions of this repository that are deliberately untrue,
and a plain extension puts them in front of anything that walks the tree for a
project file or a manifest. The reader loads them by that name, so a fixture
renamed to a plain extension fails the leg reading it rather than being read as
the real thing.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**. Each refusal leg asserts the
single complaint it is about, so a reader that started complaining about
everything is visible here rather than only against the real tree.

`clean.props.fixture` and `clean.yaml.fixture` are the neighbour that has to stay
accepted, and `a-paragraph-reworded-around-both-numbers.yaml.fixture` is the
second one: the sentence around the two numbers rewritten without either of them
moving. Without those a reader refusing every pair would pass every leg above.

`a-line-with-a-released-server.props.fixture` is the fixture with a reason to
exist rather than a shape invented for the guard. It is the only way that leg can
be proved: moving the real pin to a released 12.0 package fails the restore
before any test runs, because no such package is published, so the case the leg
exists for cannot be produced in the tree itself.
