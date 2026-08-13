<!--
What this asks for is what CONTRIBUTING.md already asks for and what the checks
on this pull request already read, so filling it in is not a second set of rules.
The two headings the check decides are marked below; the rest is what a reviewer
needs and no check reads it.

There is deliberately no example issue reference anywhere in this file. `Pull
request hygiene` looks for a hash followed by a digit in the body, and a template
carrying one as an illustration would answer that check for a body nobody filled
in. `CommunityFilesTests` refuses that, using the rule's own function.
-->

## The issue

<!-- Read by `Pull request hygiene`. Write the hash and the number, so the
     tracker links it. Every commit subject in this branch carries the same
     reference; the check reads those too, and it walks only the commits this
     pull request adds. -->

## What changed, and what failure it prevents

<!-- The failure rather than the feature. Where this corrects something, say what
     was wrong and how it was found. -->

## Evidence

<!-- Every number carries the command that produced it, run at the commit being
     pushed rather than in a working tree. A claim no command backs is written as
     a claim.

     Where a guard lands: the edit that broke it, which tests went red, and that
     the edit was undone before anything else ran. A guard nobody has watched
     fail is a guard nobody has tested. -->

## What this does not cover

<!-- Skipped tests, a clause of the issue this leaves open, a path the suite does
     not reach. A negative disclosure here stays negative: if something was not
     done, say so, and keep saying so through every edit to this body. -->

<!--
Before you push: run the suite, and sign off. `DCO sign-off` refuses a commit
with no `Signed-off-by` line.
-->
