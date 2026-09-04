#!/usr/bin/env bash
#
# Refuses a release whose checklist carries an item nobody answered.
#
# `docs/release-checklist.md` is one section per condition, and each section is in
# one of two states: a run decides it, or nothing does. Until this existed the page
# said in its own closing section that nothing enforced the last condition of #62 -
# that a release cannot be published without every item having a recorded result -
# because the publish workflow read none of it.
#
# WHAT THIS ASKS FOR IS AN ANSWER AND NOT A GREEN ITEM. #62 decided that reading on
# 2026-09-04: an item a run decides is answered by that run's verdict, and an item
# nothing decides is answered by a paragraph saying what the release ships without
# and why. A limitation written down with its reason is an answer; an empty item is
# not. So this holds a release for silence and never for bad news, and an item whose
# answer is wrong passes exactly like one whose answer is right.
#
# It runs BEFORE anything is built, because the only refusal available here is one
# that stops the tag producing a release: a published release cannot be unpublished
# without burning its tag permanently, so a check after the fact would be a report
# rather than a gate.
#
# Usage: refuse-a-release-with-an-unanswered-item.sh <release checklist>

set -eu

page=${1:?the release checklist}

if [ ! -f "$page" ]; then
  echo "There is no file at $page, so no item was read." >&2
  exit 1
fi

# The section that is about the list rather than a condition in it. Everything from
# it on is the page talking about itself, and reading it as an item would refuse
# every release for a section that decides nothing.
closing="When an item has no answer"

items=$(tr -d '\r' < "$page" | sed -n "/^## ${closing}\$/q;s/^## \(.*\)\$/\1/p")

if [ -z "$items" ]; then
  echo "REFUSED: $page carries no item before \"$closing\", so this run compared nothing. A checklist that reads as empty is not a checklist every item of which is answered." >&2
  exit 1
fi

echo "The checklist carries:"
printf '%s\n' "$items" | sed 's/^/  /'

refused=0

# One item's body: from its own heading to the next one, or to the closing section.
body_of() {
  tr -d '\r' < "$page" | awk -v want="## $1" '
    $0 == want { inside = 1; next }
    inside && /^## / { exit }
    inside { print }
  '
}

# Read line by line rather than by word, because an item title carries spaces.
while IFS= read -r title; do
  [ -z "$title" ] && continue

  body=$(body_of "$title")

  if printf '%s' "$body" | grep -q 'Decided by a run'; then
    continue
  fi

  if printf '%s' "$body" | grep -q '^Answered as a known limitation:'; then
    continue
  fi

  echo "REFUSED: the item \"$title\" is decided by no run and carries no paragraph opening \"Answered as a known limitation:\", so the release would go out with that condition unanswered" >&2
  refused=1
done <<EOF
$items
EOF

if [ "$refused" -ne 0 ]; then
  echo "This release is not published while an item on $page is unanswered." >&2
  exit 1
fi

echo "$(printf '%s\n' "$items" | grep -c .) item(s), each answered by a run or by a limitation written down with its reason."
