# Fixtures the subtitle-format page reader has to refuse

Each file here is a version of `docs/subtitle-format.md` somebody could
plausibly write, and each one breaks exactly one thing
`SubtitleFormatPageTests` reads out of that page: the sample block promising
bytes the writer does not produce, the opening paragraph naming an extension the
writer does not report, and the opening paragraph dropping the claim that this
plugin writes one format and nothing else.

They carry the extension `.md.fixture` rather than `.md` because they are
documents about this repository that are deliberately untrue, and a plain
extension puts them in front of anything that walks the tree for markdown. The
extension is checked rather than trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause
is proven by a case that trips it **and no other**. `clean.md.fixture` is the
neighbour that has to stay accepted; without it a reader that refused every page
would pass every leg above.

Two of them carry the token under test in a LATER section on purpose. The reader
is scoped to the paragraph before the first heading, and a fixture that put its
mistake there and nothing anywhere else could not tell that reader from one that
searched the whole file. With `.srt` and the words "and nothing else" written
further down, a reader that stopped scoping finds the right token in the wrong
place and reports a page that disagrees with the tree as agreeing with it.

The sample fixture breaks the index base rather than a timestamp, because the
reader parses the timing lines out of the page in order to build the cues it
feeds the writer. A fixture that broke a timestamp would fail the parse rather
than the comparison, and a leg that cannot tell those two apart proves less than
it looks.

One thing this directory cannot hold. The direction where the assembly grows a
second format writer while the page goes on claiming one is a fact about a
compiled assembly rather than about text, so no page put in front of the reader
moves it. It was proved by adding a second implementation of the writer
interface under `Output/`, watching the leg go red, and taking it out again;
that run is in the pull request that landed this rather than in a file here.
