#!/usr/bin/env bash
#
# Refuses a set of plugins that claim one thing twice.
#
# A plugin is a guest on a server it shares with others. The scheduled task key,
# the task name an operator reads, the dashboard page, the API paths the server
# answers on its behalf, the locations written to and the subtitle file names
# produced are all claimed from that server, and two plugins claiming one of them
# are each correct alone and wrong together. The second claimant is the defect,
# so the output names both claimants and the claimed value rather than only
# saying that something clashed.
#
# Usage: refuse-a-claim-collision.sh <directory of claim records>
#
# One record per plugin, as JSON. interoperability/claims/README.md says what the
# fields are and what this plugin claims.
#
# It states how many values of each kind it compared before it says anything
# about the result, so a run that read one record, or none, cannot be read as a
# set of plugins that were compared and found clean.

set -eu

directory=${1:?the directory holding one claim record per plugin}

if [ ! -d "$directory" ]; then
  echo "There is no directory at $directory, so nothing was compared." >&2
  exit 1
fi

records=$(find "$directory" -maxdepth 1 -type f -name '*.json' | sort)

if [ -z "$records" ]; then
  echo "The directory $directory holds no claim record, so this run compared nothing." >&2
  exit 1
fi

# The kinds compared, and the field each one is read out of. A kind added here
# is added to the record format and to its README in the same change.
kinds="taskKeys taskNames configurationPages routes paths subtitleFileNames"

refused=0

refuse() {
  echo "REFUSED: $1" >&2
  refused=1
}

# Every claim as one line: kind, value, claimant. Tab separated, because a task
# name is a sentence with spaces in it and a route is a path with slashes.
claims=$(mktemp)
plugins=$(mktemp)
trap 'rm -f "$claims" "$plugins"' EXIT

while IFS= read -r record; do
  if ! jq empty "$record" 2>/dev/null; then
    refuse "$record is not readable as JSON, so what that plugin claims is unknown."
    continue
  fi

  plugin=$(jq -r '.plugin // empty' "$record")

  if [ -z "$plugin" ]; then
    refuse "$record names no plugin, so a collision it is part of could not say who claimed it."
    continue
  fi

  # A record that is missing a field is refused rather than read as a plugin
  # claiming nothing of that kind. The two are opposite statements, and the one
  # this fails towards is the one that cannot pass a collision.
  for kind in $kinds; do
    if [ "$(jq -r --arg kind "$kind" 'has($kind) | tostring' "$record")" != "true" ]; then
      refuse "$record has no $kind, so it does not say what $plugin claims of that kind."
      continue 2
    fi

    if [ "$(jq -r --arg kind "$kind" '.[$kind] | type' "$record")" != "array" ]; then
      refuse "$record gives $kind as something other than a list of claimed values."
      continue 2
    fi
  done

  printf '%s\n' "$plugin" >> "$plugins"

  # The plugin id is claimed like anything else: two plugins carrying one GUID
  # are a server that loads whichever arrived first.
  id=$(jq -r '.pluginId // empty' "$record")
  if [ -n "$id" ]; then
    printf 'pluginId\t%s\t%s\n' "$id" "$plugin" >> "$claims"
  fi

  for kind in $kinds; do
    # Deduplicated within the record, so a plugin that lists one value twice is
    # not reported as colliding with itself.
    jq -r --arg kind "$kind" --arg plugin "$plugin" \
      '.[$kind] | unique | .[] | [$kind, ., $plugin] | @tsv' "$record" >> "$claims"
  done
done <<< "$records"

record_count=$(printf '%s\n' "$records" | grep -c . || true)
plugin_count=$(sort -u "$plugins" | grep -c . || true)

echo "Read $record_count claim record(s) from $directory, naming $plugin_count plugin(s)."

for kind in pluginId $kinds; do
  compared=$(awk -F'\t' -v kind="$kind" '$1 == kind' "$claims" | wc -l | tr -d ' ')
  echo "  compared $compared $kind value(s)"
  awk -F'\t' -v kind="$kind" '$1 == kind { printf "    %s claims %s\n", $3, $2 }' "$claims"
done

# Two records under one plugin name would make every value that plugin claims
# collide with itself, and the report would name it on both sides of every line.
duplicate_plugins=$(sort "$plugins" | uniq -d)
if [ -n "$duplicate_plugins" ]; then
  refuse "more than one record claims to be the same plugin, so no line below says which file it came from:"
  printf '%s\n' "$duplicate_plugins" >&2
fi

# The comparison. One value of one kind claimed by more than one plugin.
collisions=$(sort "$claims" | awk -F'\t' '
  { key = $1 "\t" $2; if (key in seen) { seen[key] = seen[key] ", " $3 } else { seen[key] = $3; order[++n] = key } count[key]++ }
  END { for (i = 1; i <= n; i++) if (count[order[i]] > 1) { split(order[i], part, "\t"); printf "%s \"%s\" is claimed by %s\n", part[1], part[2], seen[order[i]] } }
')

if [ -n "$collisions" ]; then
  refuse "two plugins claim one thing, and the second claimant is the defect:"
  printf '%s\n' "$collisions" >&2
fi

if [ "$refused" -ne 0 ]; then
  echo "This set of plugins does not install beside itself without a fight." >&2
  exit 1
fi

echo "$plugin_count plugin(s) compared across $(printf '%s ' pluginId $kinds | wc -w | tr -d ' ') kinds of claim, and no value is claimed twice."
