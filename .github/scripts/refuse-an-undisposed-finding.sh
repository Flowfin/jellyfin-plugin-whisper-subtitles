#!/usr/bin/env bash
#
# Refuses a Scorecard finding that nothing in this repository decided about.
#
# The audit is a self-audit rather than a gate, and what makes it worth running
# is that every finding it reports ends somewhere: fixed, or accepted with the
# reason written down. A score with no disposition behind it is a badge, which is
# the thing this repository is trying not to have.
#
# Until this existed nothing compared the two. `docs/scorecard-dispositions.md`
# said so in its own closing section, and gave the reason: the finding list comes
# from an API call and every test in this repository runs with the machine
# offline. That is true of the suite and it is not true here. The run that
# produces the findings already holds them, as the SARIF it uploads, so the
# comparison is a file against a file and reaches nothing.
#
# It has already mattered once. A seventh finding arrived on its own with a change
# that was about something else, and nothing said so.
#
# Usage: refuse-an-undisposed-finding.sh <results.sarif> <dispositions page>

set -eu

sarif=${1:?the SARIF the audit produced}
page=${2:?the page recording what was decided about each finding}

for file in "$sarif" "$page"; do
  if [ ! -f "$file" ]; then
    echo "There is no file at $file, so nothing was compared." >&2
    exit 1
  fi
done

if ! jq -e . "$sarif" >/dev/null 2>&1; then
  echo "REFUSED: $sarif is not JSON, so no finding was read out of it." >&2
  exit 1
fi

# A finding is a result, and its name is the rule the result names. The document
# carries several runs, one per set of rules the tool grouped, so the rules are
# collected across all of them before a result is resolved against them.
reported=$(jq -r '
  ([.runs[]?.tool.driver.rules[]? | {key: .id, value: .name}] | from_entries) as $names
  | [ .runs[]?.results[]?
      | { name: ($names[.ruleId] // .ruleId),
          score: ((.message.text // "") | capture("score is (?<n>[0-9]+)") | .n) }
    ]
  | sort_by(.name)[] | "\(.name)\t\(.score)"
' "$sarif" | tr -d '\r')

if [ -z "$reported" ]; then
  echo "REFUSED: $sarif reports no finding, so this run compared nothing." >&2
  exit 1
fi

# The headings this page keeps one finding under, and the score each one records.
# Read as `## <name>, score <n>` because that is what the page writes, and a
# heading in any other shape is not a disposition this can resolve.
recorded=$(tr -d '\r' < "$page" | sed -n 's/^## \([A-Za-z-]*\), score \([0-9]*\)$/\1\t\2/p' | sort)

if [ -z "$recorded" ]; then
  echo "REFUSED: $page records no disposition, so this run compared nothing." >&2
  exit 1
fi

echo "The audit reported:"
printf '%s\n' "$reported" | sed 's/^/  /'
echo "The page records:"
printf '%s\n' "$recorded" | sed 's/^/  /'

refused=0

refuse() {
  echo "REFUSED: $1" >&2
  refused=1
}

reported_names=$(printf '%s\n' "$reported" | cut -f1)
recorded_names=$(printf '%s\n' "$recorded" | cut -f1)

# The direction this exists for. A finding nobody decided about passes silently,
# and the page reads afterwards as though the set were complete.
for name in $reported_names; do
  if ! printf '%s\n' "$recorded_names" | grep -qxF "$name"; then
    refuse "the audit reports $name and $page records no disposition for it"
  fi
done

# The other direction. An entry for a finding the audit no longer reports is a
# reason that has outlived what it was about, and it is the state the page's own
# closing section says stays until somebody moves it.
for name in $recorded_names; do
  if ! printf '%s\n' "$reported_names" | grep -qxF "$name"; then
    refuse "$page records $name and this run of the audit did not report it"
  fi
done

# The score, because the reasoning under a heading is written against a number
# and a page whose numbers have moved is a page nobody can compare.
for name in $reported_names; do
  scored=$(printf '%s\n' "$reported" | grep -F "$name	" | cut -f2 | head -1)
  claimed=$(printf '%s\n' "$recorded" | grep -F "$name	" | cut -f2 | head -1)

  [ -z "$claimed" ] && continue

  if [ -z "$scored" ]; then
    refuse "the audit reports $name with no score in its message, so the page's $claimed cannot be compared"
  elif [ "$scored" != "$claimed" ]; then
    refuse "the audit scores $name at $scored and $page records it at $claimed"
  fi
done

if [ "$refused" -ne 0 ]; then
  echo "This audit and this record are not about the same set of findings." >&2
  exit 1
fi

echo "$(printf '%s\n' "$reported" | grep -c .) finding(s) reported, each with a disposition, each at the score the page records."
