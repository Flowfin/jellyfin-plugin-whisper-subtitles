# Two sources, one of which binds an inference runtime into its own process

`OutOfProcessBackendTests` scans the plugin's sources for a declaration that loads
native code into the process that declares it, because that is one of the two ways
the limits page's out-of-process entry stops being true. These prove the scan bites
without making the plugin wrong for a moment.

- `binds-an-inference-runtime.cs.fixture` carries the declaration. It is the case
  the scan exists for: no new backend, no new file in the package, one attribute
  inside a source that is already there.
- `reaches-a-child-process.cs.fixture` is the neighbour that has to stay accepted.
  It reaches the same tool through the seam the backend was handed, which is what
  the plugin really does, and without it a scan that refused every source in the
  project would pass the leg above.

The extension is what keeps them out of the compilation and out of the way of
anything that walks the tree for sources. Neither file has to compile: what the
scan reads is text, so a fixture that compiled would prove nothing more.

The census in that suite has no fixture here, because its subject is a compiled
assembly rather than text. What proves it is the fabricated backends inside the
suite, which are in this project and never in the population the census reads, and
one run against the real tree recorded in the pull request that added it.
