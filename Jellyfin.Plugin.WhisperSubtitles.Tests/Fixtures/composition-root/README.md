# Fixtures the composition-root scan has to judge

`CompositionRootTests` refuses a plugin source that constructs one of the real
implementations the registrator names for the container. These two files are what
proves it bites and what proves it is not simply refusing everything.

`builds-a-real-implementation.cs.fixture` is the shape this rule exists against,
and it is the one that was really here: a type holding the real removal as a
static and handing it to a caller that asked for nothing. It has to be refused.

`takes-the-implementation-it-was-given.cs.fixture` is the one-change neighbour. It
names the same type and receives one through its constructor instead of building
one, which is what every type under the task already does, and it has to stay
accepted. Without it a scan that refused any source naming an implementation at
all would pass the leg above and nothing here would say so.

Both carry the extension `.cs.fixture` so the compiler never sees them and no scan
over the sources counts them as sources.
