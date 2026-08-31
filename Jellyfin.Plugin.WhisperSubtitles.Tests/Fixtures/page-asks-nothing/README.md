# Fixtures the configuration page's own denial has to judge

Each file here is a configuration page somebody could plausibly write, cut down to the
two things `ConfigurationPageAsksNothingTests` reads: the sentence saying nothing on
the page asks the backend anything, and the calls the page makes on the server.

They carry `.html.fixture` rather than `.html`, because they are pages this plugin
could not ship and a plain extension puts them in front of the page format reader and
everything else that walks this plugin's markup. The extension is checked rather than
trusted, in the leg that reads this directory.

A fixture per clause rather than one file breaking all of them, because a clause is
proven by a case that trips it **and no other**. Each refusing page therefore keeps
everything the other clauses ask for, so a reader complaining about every page would
fail the equality rather than pass the refusal.

`clean.html.fixture` is the neighbour that has to stay accepted.

`asks-something-new.html.fixture` is the loud direction. The page acquires one call
that asks the server something and keeps the sentence denying it, so the page denies a
check it makes. That is worse than no sentence: a reader who has been told nothing is
asked stops looking for the answer.

`dropped-the-denial.html.fixture` is the quiet direction and the one nobody would
notice. The page still asks nothing about a backend and no longer says so, and two
unvalidated paths then look validated because an operator typed them and was shown no
complaint.

`lost-a-recorded-call.html.fixture` is the direction a record that only ever grows
would miss. The page stops reading the library list, which is a page asking less
rather than more, and a floor rather than a set would read that as an improvement.
