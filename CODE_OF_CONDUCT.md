# Code of conduct

This is a small repository with one maintainer, and this page says what that
means for anybody who takes part: what is expected, where a report about
somebody's behaviour goes, and what I can and cannot do about one. It is
written to be accurate about the second and third rather than reassuring,
because a page that names a channel nobody reads is worse than one that says
plainly what exists.

## Where it applies

Everything that happens under this repository's name: issues, pull requests,
reviews, and discussion on any of them. It does not reach into other projects
this plugin is a guest of, and a report about conduct on Jellyfin's own
tracker belongs to that project.

## What is expected

Argue about the work, from evidence. A change here is refused by a check or by
a reading of the tree, and a disagreement about a rule is an argument with the
reasoning behind it, which every rule in this repository is written to carry.
That kind of disagreement is welcome and is most of what the tracker is for.

What is not welcome is the same thing every code of conduct names: attacks on a
person rather than on a change, harassment in any form, publishing somebody's
private details, and sexualised language or imagery. None of it has a place
here and none of it is a matter of tone.

## Where a report goes

This repository keeps one private channel, and it is for vulnerabilities:
`SECURITY.md` names it. A report about somebody's behaviour is not a
vulnerability and does not belong in the advisory queue, where it would be read
as one.

No second private channel is promised, and that is a decision rather than a
gap. It was taken on #8, whose answer to where a report goes is that the
private advisory route stays the one security channel, with no second named
mailbox, because a mailbox is a second standing promise to read and answer:

    gh issue view 8 --repo Flowfin/jellyfin-plugin-whisper-subtitles --json comments --jq '.comments[] | select(.body | contains("Answer to question 7")) | .body'

So a report about conduct here is one of two things:

- an issue on this tracker, where I read it like every other issue; or,
- where it must not be public, a report to GitHub through the report option on
  the comment or the account concerned. That route is GitHub's, it reaches
  GitHub's own staff and not me, and what it promises is theirs to state.

Nothing on this page names a contact that does not exist, and a reader who
finds one added later without the decision above being revisited has found a
stale page rather than a channel.

## What happens to a report

I read it and I answer once I have read it properly. No deadline is promised,
for the reason `SECURITY.md` gives for the same absence: a number I published
would eventually be missed, and a reporter cannot tell a missed deadline from a
report that never arrived.

What I can do is bounded by what a repository owner can do, and it is worth
listing so nobody expects more: edit or delete a comment, lock a thread, and
block an account from this repository. I cannot act on anything outside this
repository, and I do not try to.

## What this page is not

It is not a promise of moderation at any hour, and it is not an assurance that
this tracker is a safe place for anybody in particular. It is a statement of
what is expected, what exists to report to, and what I will do, in that order,
with each of the three no larger than it is.
