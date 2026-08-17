#!/usr/bin/env bash
#
# Writes the bill of materials for what an operator installs, off the build.
#
# The archive an operator receives is not the publish directory. jprm copies the
# files `build.yaml` names under `artifacts`, adds `meta.json` and zips that, so
# the shipped set is those names and nothing else. This reads that set out of
# the directory the build produced and describes each file by its own bytes.
#
# Reading the build rather than the project file is the whole point of the
# document. A generator pointed at the project describes the restore graph,
# which for this plugin is a set of packages the server supplies and the
# operator never receives, because both package references exclude their runtime
# assets. Such a document names components nobody installs and omits the one
# file they do.
#
# Usage: write-bill-of-materials.sh <directory> <manifest> <output file>

set -eu

directory=${1:?the directory holding the built plugin}
manifest=${2:?the plugin manifest naming what ships}
output=${3:?the file to write the bill of materials to}

if [ ! -d "$directory" ]; then
  echo "There is no directory at $directory, so nothing was described." >&2
  exit 1
fi

if [ ! -f "$manifest" ]; then
  echo "There is no manifest at $manifest, so nothing said what ships." >&2
  exit 1
fi

artifacts=$(sed -n '/^artifacts:/,/^[a-zA-Z]/ s/^- *"\(.*\)"$/\1/p' "$manifest")

if [ -z "$artifacts" ]; then
  echo "No artifacts were read out of $manifest, so this run would describe an empty package." >&2
  exit 1
fi

name=$(sed -n 's/^name: *"\(.*\)"$/\1/p' "$manifest" | head -1)
version=$(sed -n 's/^version: *"\(.*\)"$/\1/p' "$manifest" | head -1)

if [ -z "$name" ] || [ -z "$version" ]; then
  echo "$manifest carries no name or no version, so the document would identify nothing." >&2
  exit 1
fi

# Every shipped name has to be a file this build produced. A document that
# quietly skipped a missing one would be a description of a package that was
# never built.
for artifact in $artifacts; do
  if [ ! -f "$directory/$artifact" ]; then
    echo "$manifest ships $artifact and there is no such file in $directory." >&2
    exit 1
  fi
done

components=""

for artifact in $artifacts; do
  bytes=$(wc -c < "$directory/$artifact" | tr -d ' ')
  digest=$(sha256sum "$directory/$artifact" | cut -d' ' -f1)

  [ -n "$components" ] && components="$components,"

  components=$(printf '%s\n    {\n      "type": "file",\n      "name": "%s",\n      "version": "%s",\n      "size": %s,\n      "hashes": [ { "alg": "SHA-256", "content": "%s" } ]\n    }' \
    "$components" "$artifact" "$version" "$bytes" "$digest")
done

cat > "$output" <<DOCUMENT
{
  "bomFormat": "CycloneDX",
  "specVersion": "1.5",
  "version": 1,
  "metadata": {
    "component": {
      "type": "application",
      "name": "$name",
      "version": "$version"
    }
  },
  "components": [$components
  ]
}
DOCUMENT

# What was described is printed before anything reads the document, so a run over
# a manifest shipping one file cannot be mistaken for a run over the whole build.
echo "Described $(printf '%s\n' "$artifacts" | grep -c .) shipped file(s) from $directory into $output:"
for artifact in $artifacts; do
  printf '  %10d  %s\n' "$(wc -c < "$directory/$artifact")" "$artifact"
done
