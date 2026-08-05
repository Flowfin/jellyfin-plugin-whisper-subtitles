# The subtitle format this plugin writes

This plugin writes SubRip, and nothing else, for the first release. Files carry
the `.srt` extension.

## Why SubRip

Every client in the ecosystem reads it. It carries exactly what a transcription
produces, which is timed plain text. It has no styling model, so there is nothing
in it to get wrong.

WebVTT would add a second writer and a second set of tests without adding a
capability a transcription needs. The styled formats would invite a styling
surface this plugin has no business owning: a transcription is words and the
times they were said, and a format that can express fonts and positions is an
invitation to start deciding those things on the operator's behalf.

## What the files look like

Blocks are separated by a blank line. Each block is an index counting from one, a
timing line, and the text.

    1
    00:00:00,000 --> 00:00:01,500
    First line.

    2
    00:00:01,500 --> 01:02:03,456
    Second line.

Two properties of the bytes are worth stating because they are deliberate and
because tests hold them.

The encoding is UTF-8 with no byte order mark. Text outside ASCII survives
unchanged, and the mark is left out because it is not needed to read the file and
some readers show it as a stray character on the first cue.

Lines end with a carriage return and a line feed, everywhere, whatever the server
runs on. The server's own SubRip writer uses the platform's newline, so the same
subtitle comes out differently from a Linux server and a Windows one. This plugin
does not do that. A file written on one machine and read on another is identical
byte for byte, which is also what lets a test assert on the bytes rather than on
whatever the machine happened to produce.

A segment whose text carries a line break is flattened onto one line. A blank
line ends a cue, so text containing one would end its cue early and every cue
after it would be read as part of the wrong block.

## Adding a second format later

The writer takes cues and returns bytes, behind `ISubtitleFormatWriter`, so a
second format is a second implementation and not a rewrite of the first. Nothing
above is a reason a second format cannot be added; it is the reason there is one
today.
