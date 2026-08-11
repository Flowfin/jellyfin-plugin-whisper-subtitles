# What this plugin says in the log, and at which level

The server log belongs to the server, and every installed plugin writes into the
same file. A task that writes a line per item turns a library of a hundred
thousand items into a hundred thousand lines somebody has to page through to
find the one that mattered, and the operator did not install this plugin to lose
their log.

This page is the table the first change that logs is measured against. It exists
before that change rather than after it, because the alternative is that the
scheme gets decided by whoever writes the first line and then copied.

## The table

| Event | Level | What the line carries |
| --- | --- | --- |
| A run starts | Information | The number of items selected and the estimate for them |
| A run ends | Information | The counts by outcome |
| An item is skipped | Warning | The item, and the typed reason it was skipped |
| An item fails | Warning | The item, and the typed reason it failed |
| An item succeeds | Debug | Whatever is useful to somebody debugging, and nothing an operator is expected to read |
| Anything at all | Never | A configured secret |

Two lines per run above debug, plus one per item that did not work. That is the
shape the rule reduces to: what an operator reads is a function of the failures
and not of the size of the library.

## Why each of them

**The two run lines.** They are what says a run happened and what it did. An
operator who reads nothing else should still be able to tell, from the log
alone, that the task ran last night, how much it took on and how it came out.
Splitting either of them across several lines defeats the purpose, because a
count assembled by the reader out of five lines is a count the reader can get
wrong.

**Warning for a skip as well as for a failure.** A skipped item is not an error,
and it is still the thing somebody opened the log to find: the question an
operator brings to the log is almost always why one particular item has no
subtitle. Putting a skip at information would bury it under the run lines of
every other run, and putting it at debug would hide it from the only person who
wants it.

**The typed reason rather than a stack trace.** The reasons are a closed
vocabulary with one sentence each, in
`Jellyfin.Plugin.WhisperSubtitles/Attempts/FailureReasonMessages.cs`, and that
switch has no fallback arm, so a reason added without a sentence fails the build
instead of reaching an operator as an enum name. A log line uses those sentences.
They say what happened and never what to do about it; what to do is
`docs/troubleshooting.md`, which is keyed by the same vocabulary.

**Debug for a success.** Not silence, because somebody debugging a run wants the
per-item trail, and not information, because there is one such line per item.

**A secret at no level.** The remote backend's key is the one this plugin holds,
and the rule is written once here rather than at each place a logger is used.
The backend already holds the half that needs no logger: nothing it produces on
any failure path carries the key, which
`The_key_reaches_no_message_this_backend_produces_on_any_failure_path` in
`RemoteWhisperBackendTests` asserts. The half that needs a logger is the one this
page is for.

## What the tree holds today

Nothing. This plugin does not log at all:

    git grep -c 'ILogger' -- '*.cs'
    exit=1

So there is no logger for this table to sit beside, and no line of it is held by
anything. It is a decision written down, not a property a machine refuses, and
the distinction matters here for the same reason it matters in `docs/limits.md`:
a table in a document that reads like a guarantee is worse than no table.

What would make it one is in #73's own done-when. A run of several hundred items
through a stubbed backend, asserting that the number of lines above debug follows
the failures rather than the items, is the check that refuses a per-item
information line. A test over the two run lines is what refuses a count that does
not match what the run did. A test over every captured line at every level is
what refuses a configured secret reaching one. None of the three can be written
before something logs, and the first change that logs is the one that owes them.
