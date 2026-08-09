#!/usr/bin/env bash
#
# Derives the lowest Jellyfin package version each supported server line has to
# compile against, from the manifest that promises it and from the project that
# names the lines.
#
# The floor is not a number written down here. A manifest says which servers it
# will install on, in targetAbi, and that promise is the floor: a symbol that
# arrived after it compiles against the pinned package, ships, and is missing on
# a server the manifest claims. Reading the promise rather than restating it is
# what keeps the two from drifting, because raising targetAbi then raises the
# floor with no second edit.
#
# Usage: read-abi-floor.sh <manifest> <project>
#
# What it writes, and where:
#
#   stdout  one line per server line that has a floor, as
#           "<framework> <floor package version> <pinned package version>",
#           which is what a caller builds from
#   stderr  what it examined, including every supported line with no manifest,
#           so a run that derived one floor out of two cannot be read as a run
#           that covered both
#
# It refuses rather than printing nothing, because an empty stdout and a clean
# exit is how a floor check that examined nothing looks exactly like one that
# examined everything.

set -eu

manifest=${1:?the manifest naming the servers this build promises to install on}
project=${2:?the project that names the supported server lines}

if [ ! -f "$manifest" ]; then
  echo "There is no manifest at $manifest, so no floor was derived." >&2
  exit 1
fi

if [ ! -f "$project" ]; then
  echo "There is no project at $project, so the supported server lines are unknown." >&2
  exit 1
fi

# The promise. Quoted or bare, because a manifest is hand written and both are
# valid YAML for the same string.
target_abi=$(sed -n 's/^targetAbi:[[:space:]]*"\{0,1\}\([0-9][0-9.]*\)"\{0,1\}[[:space:]]*$/\1/p' "$manifest" | head -n 1)

if [ -z "$target_abi" ]; then
  echo "No targetAbi was read out of $manifest, so there is no promise to derive a floor from." >&2
  exit 1
fi

# A server line is the first two components and a package version is the first
# three, which is how both are written in this tree: targetAbi 10.11.0.0 is the
# 10.11 line at package 10.11.0.
floor=$(printf '%s' "$target_abi" | cut -d. -f1-3)
manifest_line=$(printf '%s' "$target_abi" | cut -d. -f1-2)

echo "Manifest $manifest promises servers from $target_abi upward." >&2
echo "  that is server line $manifest_line, at package version $floor" >&2

read_property() {
  dotnet msbuild "$project" -nologo -getProperty:"$1" ${2:+-p:TargetFramework="$2"} | tr -d '\r' | sed '/^[[:space:]]*$/d' | tail -n 1
}

supported=$(read_property SupportedServerLines | tr ';' ' ')

if [ -z "$supported" ]; then
  echo "$project names no supported server lines, so there was nothing to hold to a floor." >&2
  exit 1
fi

covered=0
uncovered=""
output=""

for framework in $supported; do
  line=$(read_property JellyfinServerLine "$framework")
  pin=$(read_property JellyfinPackageVersion "$framework")

  if [ "$line" != "$manifest_line" ]; then
    echo "  $framework is server line $line, which $manifest does not describe" >&2
    uncovered="$uncovered $line ($framework)"
    continue
  fi

  # A floor above the pin is a manifest promising a server newer than the
  # package this tree compiles against, which is the promise being raised
  # without the build following it. Nothing else in the tree would notice.
  lowest=$(printf '%s\n%s\n' "$floor" "$pin" | sort -V | head -n 1)
  if [ "$lowest" != "$floor" ]; then
    echo "$manifest promises servers from $target_abi upward and $framework compiles against $pin, which is older than the promise." >&2
    exit 1
  fi

  echo "  $framework is server line $line, pinned at $pin, floor $floor" >&2
  output="${output}${framework} ${floor} ${pin}
"
  covered=$((covered + 1))
done

total=$(printf '%s\n' $supported | wc -l | tr -d ' ')

if [ "$covered" -eq 0 ]; then
  echo "None of the $total supported server line(s) is described by $manifest, so no floor was derived." >&2
  exit 1
fi

if [ -n "$uncovered" ]; then
  echo "Not covered, because no manifest in this tree describes them:$uncovered" >&2
fi

echo "$covered of $total supported server line(s) has a declared floor." >&2

printf '%s' "$output"
