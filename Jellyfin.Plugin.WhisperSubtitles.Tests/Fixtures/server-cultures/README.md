# The rows the server resolves a language code against

`cultures.txt` holds rows copied out of the server's own culture table, in the
server's own format, so a test asking what a file name parses back to is answered
by the server's data rather than by this repository's opinion of it.

## Where the rows come from

The table lives in the server tree at
`Emby.Server.Implementations/Localization/iso6392.txt` and is read by
`LocalizationManager.LoadCultures`, which is also where the format is decided:
five fields separated by `|`, being the ISO 639-2/T code, the ISO 639-2/B code
where the language has a second one, the two letter code, the English name and
the French name. A row whose two letter field is empty is skipped by that loader
and can never be resolved.

    gh api "repos/jellyfin/jellyfin/contents/Emby.Server.Implementations/Localization/iso6392.txt?ref=release-10.11.z" --jq '"\(.sha) \(.size)"'
    4ce739c7e033ccbd014f478cd0c6ef5928e887c8 15918

    gh api "repos/jellyfin/jellyfin/contents/Emby.Server.Implementations/Localization/iso6392.txt?ref=master" --jq '"\(.sha) \(.size)"'
    d5a7e866b8314a2018518c81d902ff0612224f63 15918

The two supported lines do not carry the same file. They differ in two rows and
in nothing else:

    diff <10.11 file> <master file>
    350,351c350,351
    < pop||pt-pt|Portuguese (Portugal)|portugais (pt-pt)
    < pob||pt-br|Portuguese (Brazil)|portugais (pt-br)
    ---
    > por||pt-pt|Portuguese (Portugal)|portugais (pt-pt)
    > por||pt-br|Portuguese (Brazil)|portugais (pt-br)

Neither row is here, and neither can be reached by anything this plugin writes:
`SubtitleLanguageCode` produces `por` for Portuguese, which both lines resolve to
`por` off the row above these two. So one file serves both lines, and the
difference between them is recorded here rather than carried twice.

## What is here and what is not

Every row this file holds is a whole upstream row, unedited. What is left out is
every row no code this plugin can produce resolves against. The selection is not
arbitrary and it is not a judgement: `FindLanguageInfo` returns the FIRST row that
matches, so for each code the row that answers is the row upstream would have
answered with, and every earlier row that could have shadowed it did not, or it
would be the answer here as well.

`haw|||Hawaiian|hawaïen` is here for the opposite reason. Its two letter field is
empty, so the loader drops it and Hawaiian cannot be named on either supported
line. Carrying the row is what lets a test show the refusal comes from the
server's data and not from a row somebody forgot to copy.

## What nothing here checks

That these rows still match upstream. The suite is offline by rule, so no test
re-runs the commands above, and a row that changed in the server tree changes
nothing here until a person runs them. The commands are written out so that
running them is the whole of the work.
