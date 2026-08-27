# Fixtures the plugin clock scan has to judge

`PluginClockTests` refuses a wall clock read anywhere in the plugin's own
sources. These two files are what proves it bites and what proves it is not
simply refusing everything.

`reads-the-wall-clock.cs.fixture` is the shape somebody writes when a type wants
to know when something happened and reaches for the clock rather than for a
parameter. It has to be refused.

`takes-the-moment-it-was-given.cs.fixture` is the one-change neighbour. It wants
the same fact and receives it, which is what `CalibratedThroughput` already does,
and it has to stay accepted. Without it a scan that refused any source naming a
moment at all would pass the leg above and nothing here would say so.

Both carry the extension `.cs.fixture` so the compiler never sees them and no
scan over the sources counts them as sources.
