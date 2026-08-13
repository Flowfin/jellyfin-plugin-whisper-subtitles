# The untrusted input this plugin accepts, and what bounds each kind

This plugin launches a program the operator named, hands it a file, reads what it
prints, talks to a URL the operator typed, and writes files whose names come from
library metadata. Each of those is a boundary, and each is held by one type rather
than by care spread across the callers.

The list is here so that a third backend or a new surface is checked against
something written down. It is not a claim that the operator is an attacker: they
own the server and the media. It is a statement that metadata, a child process and
a network endpoint are not the operator, and that this plugin holds a boundary
between them and the file system it writes to.

Every line ends the same way and the ending is read rather than trusted.
`Bounded by` names the type that holds the bound, in backticks. `Hostile case in`
names the test class that feeds it the case it exists for, in backticks.
`UntrustedInputTests` resolves the first against the types this plugin's assembly
carries and the second against the classes this suite runs tests in, so an entry
naming something that was renamed, moved or never written turns the suite red
instead of going on standing for a bound nobody holds.

## The boundaries, and what bounds each one

- The executable path and the model path, which the operator supplies. They are
  carried as an executable and a list of arguments with no way to express a
  command line, so no quoting rule anywhere gets to decide what runs.
  Bounded by `ProcessInvocation`. Hostile case in `LocalWhisperBackendTests`.
- The argument vector built around them, which includes a path derived from a
  media item. Each element reaches the program as exactly one argument whatever
  it contains, because the launch fills `ArgumentList` and never a string.
  Bounded by `SystemProcessRunner`. Hostile case in `LocalWhisperBackendTests`.
- The output of that child process, which is text from a program this repository
  did not build. It answers instead of throwing, refuses a line it does not
  understand rather than skipping it, holds its own ceilings on line length and
  segment count, and reads timestamps by hand so there is no pattern to reason
  about over hostile bytes. Bounded by `WhisperOutputReader`. Hostile case in
  `WhisperOutputReaderTests`.
- The response from the remote endpoint, which is bounded in size before it is
  read, checked against its declared type, parsed without trusting any length
  the body announces about itself, refused where its text is not valid UTF-8,
  and refused where it times a segment past anything a library holds. Those last
  two were the two ways to make this reader stop answering instead of refusing,
  and a fuzzer found the second one. Bounded by
  `TranscriptionResponseReader`. Hostile case in
  `TranscriptionResponseReaderTests`.
- The item name that becomes a file name, which is where a separator or a
  traversal sequence in metadata would otherwise reach the file system. The
  destination is resolved and compared against the folder it must stay inside, so
  a name that would leave it is refused and nothing is written. Bounded by
  `SubtitleDestination`. Hostile case in `SubtitleDestinationTests`.
- The language code that becomes part of that file name, which arrives from a
  backend rather than from the operator. Its shape is refused before any table is
  read, so a string carrying a separator or a traversal sequence is answered as
  what it is rather than as a language nobody has. Bounded by
  `SubtitleLanguageCode`. Hostile case in `LanguageCodeTests`.
- The configuration file, which a person can edit by hand and which is read before
  anything else runs. Every value is decided once at load, a value that fails its
  rule falls back to the documented default with a complaint naming the field, and
  a file that will not parse at all is the defaults rather than an exception.
  Bounded by `ConfigurationValidation`. Hostile case in
  `ConfigurationValidationTests`.

Seven entries where the issue that asked for this named six. The item name and the
language code are split, because they are two bounds in two types with two
different hostile cases, and one line naming both would resolve against whichever
of them somebody wrote in it.

## The shapes this list forbids

Resolving the endings above says each bound exists. It says nothing about a second
route around one, and a second route is what a later change adds without meaning
to: a process launched where it is convenient, a command line assembled as a
string, an HTTP client built outside the backend that owns the endpoint, a media
tool found rather than reported. None of those would fail any test here, because
the code that used to be the only way in would still be there and would still
pass.

So four shapes are refused in the source itself, in the same
`UntrustedInputTests`, each with a fixture in the tree that trips it and a
neighbour that has to stay accepted.

- A process started anywhere but the injected runner. One launch exists and
  `IProcessRunner` is the seam in front of it; a second would be a program run
  with no test able to see the arguments it was given.
- A command line assembled as one string, or a launch through a shell. The bound
  on the first two entries above is the argument vector, and it is only a bound
  while there is no way to express the alternative.
- An HTTP client built outside the remote backend. The endpoint the operator typed
  is that backend's boundary, and a client made elsewhere reaches the network past
  the size ceiling, the declared-type check and the injected handler every test
  drives.
- A media tool path this plugin chose rather than the one the server reports. The
  server knows where a working encoder is and says so, and that value reaches the
  extractor as an argument. A setting for the tool, a variable read out of the
  environment the server was started in, or a bare name left to the search path
  each move the choice of which program runs to an operator, to whatever can write
  the configuration, or to whatever is first on a path, and the caller that passes
  the server's own value would still be there and would still pass.

What this does not do is read what the code means. It reads the source for the
tokens each shape is written with, so a launch reached through reflection or a
client built by a helper named something else walks past it. That is the same
bound `BackendIsolation` states about itself, and it is why the fixtures matter:
the rules are proven by cases in the tree rather than by the claim above.
