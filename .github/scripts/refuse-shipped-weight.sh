#!/usr/bin/env bash
#
# Refuses a built plugin that carries weight every operator would pay for.
#
# The plugin must work with a local backend, a remote endpoint, or none at all,
# and it must never make the server depend on a model being present. That is a
# property of what the build produced rather than of how the code behaves, so it
# is read off the files and not off the source: a package reference that pulls a
# native runtime in transitively appears here and appears nowhere a grep of the
# tree would find it.
#
# Usage: refuse-shipped-weight.sh <directory> <expected assembly file name>
#
# It prints what it compared before it says anything about the result, so a run
# that looked at an empty directory cannot be read as a run that looked at a
# package and found nothing wrong.

set -eu

directory=${1:?the directory holding the built plugin}
assembly=${2:?the file name of the assembly this plugin builds}

# The per-file ceiling, and the reason for it.
#
# The largest file this build produces today is the documentation XML at about
# 90 KiB, and the assembly itself is around 50 KiB, so four mebibytes is more
# than forty times the largest thing that legitimately ships. It sits far below
# the smallest thing this check exists to catch: whisper.cpp's smallest
# published model is 75 MiB on disk and its largest is 2.9 GiB, and a native
# inference library is tens of mebibytes. Anything landing between those two is
# something nobody chose, which is exactly the case worth stopping.
ceiling_bytes=$((4 * 1024 * 1024))

if [ ! -d "$directory" ]; then
  echo "There is no directory at $directory, so nothing was examined." >&2
  exit 1
fi

files=$(cd "$directory" && find . -type f | sed 's|^\./||' | sort)

if [ -z "$files" ]; then
  echo "The directory $directory holds no files, so this run compared nothing." >&2
  exit 1
fi

count=$(printf '%s\n' "$files" | wc -l | tr -d ' ')

echo "Examining $count file(s) in $directory:"
(cd "$directory" && printf '%s\n' "$files" | while IFS= read -r file; do
  printf '  %10d  %s\n' "$(wc -c < "$file")" "$file"
done)

refused=0

refuse() {
  echo "REFUSED: $1" >&2
  refused=1
}

# One assembly, and it is this plugin's.
#
# The server supplies the Jellyfin assemblies, which is why the project excludes
# their runtime assets, so a second assembly here is either a dependency nobody
# decided to ship or a native library wearing a managed extension. Counting them
# catches both without having to tell a managed PE file from a native one.
assemblies=$(printf '%s\n' "$files" | grep -i '\.dll$' || true)
assembly_count=$(printf '%s' "$assemblies" | grep -c . || true)

if [ "$assembly_count" -ne 1 ]; then
  refuse "the build produced $assembly_count assemblies and exactly one is allowed:"
  printf '%s\n' "$assemblies" >&2
elif [ "$assemblies" != "$assembly" ]; then
  refuse "the one assembly is $assemblies and it should be $assembly"
fi

# Native libraries, by the extensions each platform uses for one.
for pattern in '\.so$' '\.so\.[0-9]' '\.dylib$' '\.a$' '\.lib$' '\.node$'; do
  found=$(printf '%s\n' "$files" | grep -i "$pattern" || true)
  if [ -n "$found" ]; then
    refuse "a native library ships in this package:"
    printf '%s\n' "$found" >&2
  fi
done

# Model weights, by the extensions the projects in this space use.
for pattern in '\.bin$' '\.gguf$' '\.ggml$' '\.pt$' '\.pth$' '\.onnx$' '\.safetensors$'; do
  found=$(printf '%s\n' "$files" | grep -i "$pattern" || true)
  if [ -n "$found" ]; then
    refuse "a model file ships in this package:"
    printf '%s\n' "$found" >&2
  fi
done

# The ceiling, which is the arm that catches the shape the two lists above have
# not been taught yet. Asked of find rather than measured file by file, so the
# answer comes from one traversal and the empty case costs nothing.
oversized=$(cd "$directory" && find . -type f -size +"$ceiling_bytes"c | sed 's|^\./||' | sort)

if [ -n "$oversized" ]; then
  refuse "a file is over the ceiling of $ceiling_bytes bytes:"
  for file in $oversized; do
    printf '  %10d  %s\n' "$(wc -c < "$directory/$file")" "$file" >&2
  done
fi

if [ "$refused" -ne 0 ]; then
  echo "This package carries weight every operator pays for, including the ones using a remote endpoint or nothing." >&2
  exit 1
fi

echo "$count file(s) examined, one assembly, no native library, no model, nothing over $ceiling_bytes bytes."
