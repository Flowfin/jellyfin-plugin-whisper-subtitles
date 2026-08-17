#!/usr/bin/env bash
#
# Refuses a bill of materials that describes something other than this build.
#
# The document is only worth reading if it came off the artefact. A generator
# pointed at the project file produces a valid CycloneDX document describing the
# restore graph, and for this plugin that graph is a set of packages the server
# supplies and the operator never receives, because both package references
# exclude their runtime assets. Such a document is well formed, plausible, and
# about a different thing.
#
# So the comparison is against the files rather than against the generator that
# wrote it: every shipped name is a component, every component is a shipped
# name, and every hash is the hash of the bytes in the directory. Any of the
# three failing means the document and the package have parted company.
#
# Usage: refuse-a-bill-that-is-not-the-build.sh <bill> <directory> <manifest>

set -eu

bill=${1:?the bill of materials to read}
directory=${2:?the directory holding the built plugin}
manifest=${3:?the plugin manifest naming what ships}

for file in "$bill" "$manifest"; do
  if [ ! -f "$file" ]; then
    echo "There is no file at $file, so nothing was compared." >&2
    exit 1
  fi
done

if [ ! -d "$directory" ]; then
  echo "There is no directory at $directory, so nothing was compared." >&2
  exit 1
fi

shipped=$(sed -n '/^artifacts:/,/^[a-zA-Z]/ s/^- *"\(.*\)"$/\1/p' "$manifest" | sort)

if [ -z "$shipped" ]; then
  echo "No artifacts were read out of $manifest, so this run compared nothing." >&2
  exit 1
fi

# Read as JSON rather than by line, because the document this exists to catch is
# one another generator wrote and its formatting is not this repository's. A
# line-oriented reader would find no component in it, refuse for the wrong
# reason, and go on refusing for the wrong reason the day the formatting here
# changes.
#
# The names come out of the components array alone. The metadata block carries a
# name too, and reading that as a component is how a document describing nothing
# at all would pass as a document describing the plugin.
if ! jq -e . "$bill" >/dev/null 2>&1; then
  echo "REFUSED: $bill is not JSON, so it describes nothing that can be compared." >&2
  exit 1
fi

# The carriage returns go before the names are compared. A JSON document with
# CRLF endings is a legal one, and on a clone where jq writes them the first name
# would carry a byte the manifest's never does, so every comparison below would
# fail for a reason that is nothing to do with the package.
described=$(jq -r '[.components[]?.name] | sort | .[]' "$bill" | tr -d '\r')

if [ -z "$described" ]; then
  echo "REFUSED: $bill names no component, so it describes no package." >&2
  exit 1
fi

echo "The manifest ships:"
printf '%s\n' "$shipped" | sed 's/^/  /'
echo "The bill describes:"
printf '%s\n' "$described" | sed 's/^/  /'

refused=0

refuse() {
  echo "REFUSED: $1" >&2
  refused=1
}

# A component that is not shipped. This is the arm a document generated from the
# project file trips, and it trips it once per package in the restore graph.
for component in $described; do
  if ! printf '%s\n' "$shipped" | grep -qxF "$component"; then
    refuse "the bill describes $component, which this package does not contain"
  fi
done

# A shipped file with no component. A bill that omits what an operator receives
# is the same defect read from the other side.
for artifact in $shipped; do
  if ! printf '%s\n' "$described" | grep -qxF "$artifact"; then
    refuse "the package contains $artifact and the bill does not describe it"
  fi
done

# The hash, which is what separates a document read off the bytes from one that
# named the right files without looking at them.
#
# A file the arm above already refused is skipped rather than asked for a hash.
# Asking would print that the bill describes it and gives no hash, which is not
# what happened: the bill does not describe it at all, and one absence reported
# as two says the second thing wrongly.
for artifact in $shipped; do
  if ! printf '%s\n' "$described" | grep -qxF "$artifact"; then
    continue
  fi

  if [ ! -f "$directory/$artifact" ]; then
    refuse "the package should contain $artifact and there is no such file in $directory"
    continue
  fi

  built=$(sha256sum "$directory/$artifact" | cut -d' ' -f1)
  claimed=$(jq -r --arg name "$artifact" \
    'first(.components[]? | select(.name == $name) | .hashes[]? | select(.alg == "SHA-256") | .content) // ""' \
    "$bill" 2>/dev/null | tr -d '\r' || true)

  if [ -z "$claimed" ]; then
    refuse "the bill describes $artifact and carries no SHA-256 for it"
  elif [ "$claimed" != "$built" ]; then
    refuse "the bill gives $artifact as $claimed and the built file is $built"
  fi
done

if [ "$refused" -ne 0 ]; then
  echo "This bill of materials is not a description of the package this build produced." >&2
  exit 1
fi

echo "$(printf '%s\n' "$shipped" | grep -c .) shipped file(s), each described once and each hash the hash of the built bytes."
