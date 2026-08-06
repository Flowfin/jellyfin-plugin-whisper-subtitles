# A build carrying what must never ship

Four files, one per rule in `.github/scripts/refuse-shipped-weight.sh`, so the
check is proved against a build that is wrong rather than by making the real one
wrong for a moment.

- `Jellyfin.Plugin.WhisperSubtitles.dll` is the assembly that legitimately ships,
  so the fixture is a real package with three things added rather than a
  directory of nothing but mistakes. Without it the assembly rule would refuse
  for a second reason and the other rules would never be reached.
- `Second.Assembly.dll` is the transitive dependency nobody decided to ship. It
  is the one a grep of the source would not find.
- `libwhisper.so` is the native inference library.
- `ggml-tiny.bin` is a model.

The file over the ceiling is not here. A file large enough to trip a four
mebibyte ceiling is not a thing to keep in a repository, so the job writes one
into a copy of this directory before it runs the check, and says so where it
does it.

Every file here is text. What the check reads is the name and the size, never
the bytes, so a real binary would prove nothing more and would be a binary in
the tree.
