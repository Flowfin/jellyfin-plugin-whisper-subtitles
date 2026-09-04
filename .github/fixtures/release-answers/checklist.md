# Release checklist

Two conditions and the section that is about them.

## A condition a run decides

Decided by a run.

```
dotnet test -c Release --filter "FullyQualifiedName~FixtureTests"
```

## A condition nothing decides

Nothing decides this yet, and #1 is where a route is owed.

Answered as a known limitation: the release ships without this condition, and the
reason is that the thing that would decide it does not exist in the fixture.

## When an item has no answer

A release is not published while an item above is unanswered.

This section is about the list rather than a condition in it. It is decided by no
run and it carries no answer, and a reader that took it for an item would hold
every release for a section that decides nothing.
