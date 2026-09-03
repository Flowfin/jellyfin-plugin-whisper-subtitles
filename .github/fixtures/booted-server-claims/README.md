# What a booted server answers, for the scan to be proved against

`.github/scripts/scan-a-booted-server.sh` reads four captures from a server the
workflow started: the plugin listing, the task list, the configuration page
listing and the route document. Two of the four already stand in for a real boot
under `.github/fixtures/booted-server/`, written from what a server answered,
and the job that proves this scan reads them from there rather than keeping a
second copy. The two here are the captures that job adds.

`pages.json` is what `GET /web/ConfigurationPages` answers on a server carrying
this plugin alone: one page, attributed to this plugin's id. The server lists
plugin pages and no page of its own on that route, which is read off
`DashboardController` at the pinned server tag rather than assumed, so a listing
of one is the shape a clean boot produces.

`openapi.json` is the route document cut down to what the scan reads, which is
the set of paths. A real server answers some three hundred; five is enough to be
a document and few enough to read. The scan says how many it read, so a run
handed this fixture and a run handed a real server print different numbers and
neither can be mistaken for the other.

`record-claiming-a-route.json` is this plugin's own claim record with one route
added, for the leg that proves a claimed route the server does not answer is
refused. The real record claims no route, and `RouteClaimsTests` refuses the
source gaining one, so the case can only be reached with a record that is not
the real one.

The mutations the job applies to these are made in the job, one at a time, from
the clean set, so the refusal each leg asserts is against exactly one change and
the neighbour that stays green is the same set with nothing changed.
