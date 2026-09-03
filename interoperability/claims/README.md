# What this plugin claims from a server it shares

A plugin is a guest on a server it does not own. It claims a scheduled task key,
a name an operator reads in a list, a page in the dashboard, the API paths the
server answers on its behalf, the locations it writes to, and the file names it
puts on disk. Two plugins are each correct alone and wrong together when they
claim one of those, and the second claimant is the defect.

`jellyfin-plugin-whisper-subtitles.json` is what this plugin claims, in a form a
program can load. `.github/scripts/refuse-a-claim-collision.sh` is the program:
it reads a directory of such records, one per plugin, and refuses a value two of
them claim, naming both claimants and the value.

## Why a file rather than the assertions that were already here

The set was recorded before this file existed, and it was recorded as literals
inside test classes: the task key in `SubtitleGenerationTaskTests`, the task
name, the marker and the subtitle file name in `ClaimedNamesTests`, the empty
route set in `RouteClaimsTests`, the written locations in `docs/limits.md` and
`WriteLocationsTests`. Each of those turns a change to a claim into a red suite,
which is most of what recording it is for, and none of them is a thing a scan
comparing this plugin against ten siblings can read.

So this file is the same set in the form the comparison needs, and the tests are
what keep it from becoming a second, drifting copy: `ClaimRecordTests` reads it
and compares every entry against the value the plugin actually produces, in both
directions, so a claim that moves without this file moving is a red suite and a
line added here that the plugin does not claim is one too.

## The fields

`plugin` is the repository name, and it is what the scan prints as a claimant.
`pluginId` is the GUID the server identifies the plugin by, which is a claim of
its own: two plugins sharing one is a server that loads whichever came first.

`taskKeys`, `taskNames`, `configurationPages`, `routes`, `paths` and
`subtitleFileNames` are the claimed values, compared as exact strings. A key is
case sensitive on the server, so it is compared that way here.

`routes` is empty, and an empty set is the one that grows in silence. What holds
it empty is not this file: `RouteClaimsTests` reads every source of the plugin
and refuses any shape that claims a path from the server, so the first controller
added to this plugin is a red suite before it is a stale line here.

`paths` is empty for a different reason, and it is worth reading before a sibling
record is written against this one as a model. This plugin writes three kinds of
thing and fixes the location of none of them: the subtitle goes where the
library's own setting says, the temporary audio goes into a directory it is
handed, and its configuration is written by the server in the place the server
keeps plugin data. There is no literal for another plugin to collide with. What
it writes, rather than where, is the list in `docs/limits.md`, which
`WriteLocationsTests` reads against the sources.

`subtitleFileNames` carries a shape rather than a name, because the base name
comes off the media file. `<media file base name>`, `<language>` and
`<subtitle extension>` are the parts that vary; everything else is what this
plugin decides, and the marker sitting between the language and the extension is
the part a second plugin writing subtitles could claim.

## What the record scan does not answer, and what the server scan does

`refuse-a-claim-collision.sh` compares records. It sees no sibling until a
record for that sibling is put in front of it, and this repository holds one
record.

`.github/scripts/scan-a-booted-server.sh` is the other half, and it reads a
server rather than a file. Handed what a booted server answered on its plugin
listing, its task list, its configuration page listing and its route document,
it refuses a value that server lists twice, naming both claimants and the value:
a task key or a task name registered by two tasks, a page name registered by two
plugins, a configuration file kept by two plugins, a plugin id listed twice, and
a route document the server could not build, which is what a server holding two
controllers on one path answers with. It also derives what this plugin claims
from what the server attributes to it, and compares that against this record in
both directions.

Its bound is the server's own. The server attributes a page and a configuration
file to a plugin, and attributes a task and a path to nobody, so the pages are
derived and compared as a set while for task keys, task names and routes what is
read is that nothing registered them twice and that every value this record
claims is registered. A task this plugin registers that this record omits is not
seen there; `ClaimRecordTests` holds that from the plugin's own type.

Where it reads a server is `.github/workflows/booted-server.yml`, which hands it
the captures of a booted 10.11 server carrying the shipped build, on every run.
`.github/workflows/claim-collision.yml` proves it against captures under
`.github/fixtures/booted-server-claims/` and `.github/fixtures/booted-server/`,
and the comparison against a server carrying a sibling as well is the matrix in
#66.
