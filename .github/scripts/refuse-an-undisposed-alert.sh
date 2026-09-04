#!/usr/bin/env bash
#
# Refuses a code-scanning rule that nothing in this repository decided about.
#
# The alert count on this board rises with every guard it adds, because most
# guards here are document readers and each one mints alerts of the same shape.
# Dismissing them one at a time was tried three times and lost; repairing the
# call sites was priced three times and each price was larger than the last. What
# ends a count nobody reads any more is a register: one entry per rule id, with
# the reason and the population it covers, and something that refuses the two
# ways it can go quietly wrong.
#
# The register keys on the rule id and never on a count. A number pasted beside a
# rule here would be stale the day after it was written, which is the drift this
# repository hunts elsewhere, so what a reader gets instead is the command.
#
# The comparison is a file against a file. The alert set arrives over the network
# and every test in this repository runs with the machine offline, so the fetch is
# the caller's job and this reads what it saved.
#
# Usage: refuse-an-undisposed-alert.sh <alerts json> <register page>

set -eu

alerts=${1:?the open alerts, as the API returned them}
page=${2:?the page recording what was decided about each rule}

for file in "$alerts" "$page"; do
  if [ ! -f "$file" ]; then
    echo "There is no file at $file, so nothing was compared." >&2
    exit 1
  fi
done

if ! jq -e . "$alerts" >/dev/null 2>&1; then
  echo "REFUSED: $alerts is not JSON, so no alert was read out of it." >&2
  exit 1
fi

# The rule ids the fetch reports, deduplicated. The count each one carries is
# deliberately dropped here: it moves every hour and the register does not record
# it, so comparing it would refuse the page for the world moving rather than for
# the page being wrong.
reported=$(jq -r '[ .[]? | .rule.id ] | unique | .[]' "$alerts" | tr -d '\r')

if [ -z "$reported" ]; then
  echo "REFUSED: $alerts reports no open alert. A fetch that returned nothing is a fetch that failed or a filter that matched nothing, and neither is a board with nothing left to decide about." >&2
  exit 1
fi

# The headings this page keeps one rule under, and the disposition each records.
# Read as `## <rule id>, <disposition>` because that is what the page writes, and
# a heading in any other shape is not an entry this can resolve. A rule id carries
# no space, which is what separates an entry from a prose heading that happens to
# have a comma in it - and the page has several of those.
recorded=$(tr -d '\r' < "$page" | sed -n 's/^## \([^ ,]*\), \(.*\)$/\1\t\2/p' | sort)

if [ -z "$recorded" ]; then
  echo "REFUSED: $page records no disposition, so this run compared nothing." >&2
  exit 1
fi

echo "The fetch reports:"
printf '%s\n' "$reported" | sed 's/^/  /'
echo "The page records:"
printf '%s\n' "$recorded" | sed 's/^/  /'

refused=0

refuse() {
  echo "REFUSED: $1" >&2
  refused=1
}

recorded_rules=$(printf '%s
' "$recorded" | cut -f1)

# The direction this exists for. A rule nobody decided about is invisible in a
# count, and the register reads afterwards as though the set were complete.
for rule in $reported; do
  if ! printf '%s
' "$recorded_rules" | grep -qxF "$rule"; then
    refuse "the scan reports $rule and $page records no disposition for it"
  fi
done

# The other direction. An entry for a rule nothing reports is a reason that has
# outlived what it was about, and it is what makes a register believable when it
# has none.
for rule in $recorded_rules; do
  if ! printf '%s
' "$reported" | grep -qxF "$rule"; then
    refuse "$page records $rule and this scan reports no open alert of it"
  fi
done

# The disposition itself, because an entry saying nothing about what was decided
# is a heading rather than a decision, and this register declares the four states
# it writes. The states are the vocabulary the page argues in; a fifth one is a
# decision nobody took here, not a wording choice.
for rule in $recorded_rules; do
  state=$(printf '%s
' "$recorded" | grep -F "$rule	" | cut -f2 | head -1)
  case "$state" in
    "set aside"|"repair owed"|"dismissal owed"|"decided elsewhere") ;;
    *)
      refuse "$page records $rule as \"$state\", which is not one of the four states this register declares"
      ;;
  esac
done

if [ "$refused" -ne 0 ]; then
  echo "This scan and this register are not about the same set of rules." >&2
  exit 1
fi

echo "$(printf '%s\n' "$reported" | grep -c .) rule id(s) open, each with a disposition in $page."
