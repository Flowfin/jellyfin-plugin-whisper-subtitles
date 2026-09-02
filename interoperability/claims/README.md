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

## What the scan does not answer

It compares records. It does not derive one from a running server, which is the
first condition of #64 and needs the boot in #63, so a plugin whose record says
one thing and whose running behaviour says another passes every leg here. It
sees no sibling until a record for that sibling is put in front of it, and this
repository holds one record.
