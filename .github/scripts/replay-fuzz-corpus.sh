#!/usr/bin/env bash
#
# Replays every input this repository keeps for the fuzz harness, once each.
#
# This is not fuzzing and it discovers nothing. What it is for is that a change
# to the harness or to a parser is checkable by somebody with no fuzzer
# installed, and that the inputs cannot rot unnoticed between scheduled runs: a
# seed that stopped being readable, or a target that stopped starting, shows up
# here in seconds rather than in a run somebody reads next week.
#
# Usage: replay-fuzz-corpus.sh <harness assembly> <corpus root> <reported root>
#
# Two roots, because the inputs have two jobs and one directory holding both
# would be a directory where nobody could say what a clean answer meant.
#
#   corpus    what the fuzzer starts from. Every input has to come back clean,
#             and one that does not is a finding in the parser.
#   reported  what the harness has to report. Every input has to come back
#             reported, and one that comes back clean means findings are being
#             swallowed somewhere between the property and the exit code.
#
# Under each root, one directory per target, named as the target is.

set -eu

harness=${1:?the harness assembly}
corpus=${2:?the directory holding one corpus per target}
reported=${3:?the directory holding one set of inputs that must be reported per target}

if [ ! -f "$harness" ]; then
  echo "There is no harness at $harness, so nothing was replayed." >&2
  exit 1
fi

replayed=0

replay_root() {
  root=$1
  expected=$2

  if [ ! -d "$root" ]; then
    echo "There is no directory at $root, so nothing was replayed out of it." >&2
    exit 1
  fi

  found=0

  for directory in "$root"/*/; do
    [ -d "$directory" ] || continue

    target=$(basename "$directory")
    inputs=$(find "$directory" -type f | sort)

    if [ -z "$inputs" ]; then
      echo "$directory holds no inputs, so $target was not exercised from it." >&2
      exit 1
    fi

    echo "$root/$target:"

    for input in $inputs; do
      if dotnet "$harness" "$target" --once < "$input" > /dev/null 2>&1; then
        outcome=clean
      else
        outcome=reported
      fi

      if [ "$outcome" != "$expected" ]; then
        echo "  $input came back $outcome and everything under $root has to come back $expected." >&2
        dotnet "$harness" "$target" --once < "$input" >&2 || true
        exit 1
      fi

      printf '  %-10s %s\n' "$outcome" "$input"
      replayed=$((replayed + 1))
      found=$((found + 1))
    done
  done

  if [ "$found" -eq 0 ]; then
    echo "$root holds no target directories, so this run compared nothing." >&2
    exit 1
  fi
}

replay_root "$corpus" clean
replay_root "$reported" reported

echo "replayed $replayed input(s)"
