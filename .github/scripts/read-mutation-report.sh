#!/usr/bin/env bash
# Reads the JSON report a mutation run produced and refuses when it says less
# than it appears to.
#
# Coverage says which lines ran. It does not say whether any test would have
# noticed the line being wrong, and that is what a mutation run measures. The
# score itself is printed rather than enforced: a threshold on a score becomes a
# number people tune, and what a reader acts on is the list of survivors under
# it, each carrying the file and the line a test could be written against.
#
# What is enforced is that the run happened and looked where it was told to. A
# mutation tool that fails to start, writes its report somewhere the job does not
# look, or quietly mutates nothing leaves a job that ends green with nothing to
# say, and that reads exactly like a clean run. So this fails closed in five
# directions: no report, a file that is not a report, a report in which nothing
# was scored, a scored mutant in a file the list does not name, and a listed file
# the report never mentions.
#
# The last two are the scope. The set is .github/coverage/pure-logic.txt, read
# rather than restated, because a second copy of that list drifts against the
# first and the drift is invisible from either side.
#
# Usage: read-mutation-report.sh <mutation-report.json> <list> <project-directory>
set -euo pipefail

report=${1:?the mutation report to read}
list=${2:?the file listing the pure logic}
project=${3:?the project directory the report filenames are under}

if [ ! -f "$report" ]; then
    echo "No mutation report at $report. A run that produced no report says nothing about the suite." >&2
    exit 1
fi

if [ ! -s "$report" ]; then
    echo "The mutation report at $report is empty. A run that produced no report says nothing about the suite." >&2
    exit 1
fi

wanted=$(grep -v '^[[:space:]]*#' "$list" | grep -v '^[[:space:]]*$' || true)

if [ -z "$wanted" ]; then
    echo "$list names no file. An empty scope is mutated perfectly and proves nothing." >&2
    exit 1
fi

missing_from_tree=""
for path in $wanted; do
    if [ ! -f "$project/$path" ]; then
        missing_from_tree="$missing_from_tree $path"
    fi
done

if [ -n "$missing_from_tree" ]; then
    echo "These are listed in $list and are not in the tree:" >&2
    for path in $missing_from_tree; do echo "  $path" >&2; done
    echo "A list that names a file nobody can find scopes the run to less than it claims." >&2
    exit 1
fi

printf '%s\n' "$wanted" > "$report.wanted"
trap 'rm -f "$report.wanted"' EXIT

# The report is one line of JSON carrying the whole source of every mutated file
# inside it, so it is walked a character at a time rather than matched with a
# pattern: a brace or a quote inside a source string is not structure, and an
# expression that cannot tell the difference reads a file's own text as a mutant.
# awk rather than a JSON tool, because the check next to this one is awk over the
# coverage report and this adds no second means to the tree.
#
# The report writes paths with the separator and the root of the machine that
# produced it, so both are folded to the path the list uses before anything is
# compared.
awk -v wantedfile="$report.wanted" -v listname="$list" -v projectdir="$project" '
function tail(path,   marker, at, cut) {
    gsub(/\\\\/, "/", path)
    gsub(/\\/, "/", path)
    marker = projectdir "/"
    cut = 0
    while ((at = index(substr(path, cut + 1), marker)) > 0) { cut = cut + at + length(marker) - 1 }
    return cut > 0 ? substr(path, cut + 1) : path
}

function opened(d) {
    if (d == 2 && keyat[2] == "files") { sawfiles = 1 }
    if (d == 3 && keyat[2] == "files") { file = tail(keyat[3]) }
    if (d == 5 && keyat[2] == "files" && keyat[4] == "mutants") { status = ""; mutator = ""; line = 0 }
}

function closed(d) {
    if (d != 5 || keyat[2] != "files" || keyat[4] != "mutants" || status == "") { return }
    seen[file] = 1
    byfile[file, status]++
    if (status == "Killed")     { killed++;    scoredin[file] = 1 }
    if (status == "Timeout")    { timedout++;  scoredin[file] = 1 }
    if (status == "NoCoverage") { uncovered++; scoredin[file] = 1 }
    if (status == "Survived")   { survived++;  scoredin[file] = 1; lived[++livedcount] = file ":" line "  " mutator }
    status = ""
}

function emit(d, key, value) {
    if (keyat[2] != "files") { return }
    if (d == 5 && keyat[4] == "mutants" && key == "status")      { status = value }
    if (d == 5 && keyat[4] == "mutants" && key == "mutatorName") { mutator = value }
    if (d == 7 && keyat[6] == "location" && keyat[7] == "start" && key == "line") { line = value + 0 }
}

BEGIN {
    while ((getline path < wantedfile) > 0) {
        if (path != "") { wanted[path] = 1; order[++listed] = path }
    }
}

{ json = json $0 }

END {
    n = length(json)
    depth = 0
    i = 1

    while (i <= n) {
        c = substr(json, i, 1)

        if (c == "\"") {
            start = i + 1
            i++
            while (i <= n) {
                c = substr(json, i, 1)
                if (c == "\\") { i += 2; continue }
                if (c == "\"") { break }
                i++
            }
            lasttok = substr(json, start, i - start)
            if (curkey[depth] != "") { emit(depth, curkey[depth], lasttok); curkey[depth] = "" }
            i++
            continue
        }

        if (c ~ /[-0-9]/) {
            start = i
            while (i <= n && substr(json, i, 1) ~ /[-+.eE0-9]/) { i++ }
            if (curkey[depth] != "") { emit(depth, curkey[depth], substr(json, start, i - start)); curkey[depth] = "" }
            continue
        }

        if (c == ":") { curkey[depth] = lasttok; i++; continue }

        if (c == "{" || c == "[") {
            depth++
            keyat[depth] = curkey[depth - 1]
            curkey[depth - 1] = ""
            curkey[depth] = ""
            opened(depth)
            i++
            continue
        }

        if (c == "}" || c == "]") {
            closed(depth)
            curkey[depth] = ""
            depth--
            i++
            continue
        }

        i++
    }

    if (!sawfiles) {
        print "The file given carries no files object, so it is not a mutation report." > "/dev/stderr"
        exit 1
    }

    scored = killed + timedout + survived + uncovered

    if (scored == 0) {
        print "The report scores no mutant. A run that mutated nothing ends the same way as one whose mutants were all killed, and those are not the same result." > "/dev/stderr"
        exit 1
    }

    strayed = ""
    for (path in scoredin) {
        if (!(path in wanted)) { strayed = strayed "  " path "\n" }
    }
    if (strayed != "") {
        printf "These were mutated and scored and are named nowhere in %s:\n%s", listname, strayed > "/dev/stderr"
        print "The run reached outside the pure logic, where a score measures the injected seams rather than the plugin." > "/dev/stderr"
        exit 1
    }

    absent = ""
    for (i = 1; i <= listed; i++) {
        if (!(order[i] in seen)) { absent = absent "  " order[i] "\n" }
    }
    if (absent != "") {
        printf "These are listed for the run and appear nowhere in the report:\n%s", absent > "/dev/stderr"
        print "A file the report does not mention was not mutated at all, which is not the same thing as a file whose mutants were all killed." > "/dev/stderr"
        exit 1
    }

    printf "%-46s %7s %8s %7s %8s %8s %8s\n", "file", "killed", "timeout", "lived", "nocover", "noncomp", "ignored"
    for (i = 1; i <= listed; i++) {
        path = order[i]
        printf "%-46s %7d %8d %7d %8d %8d %8d\n", path, \
            byfile[path, "Killed"], byfile[path, "Timeout"], \
            byfile[path, "Survived"], byfile[path, "NoCoverage"], \
            byfile[path, "CompileError"], byfile[path, "Ignored"]
    }

    printf "\n%d mutant(s) survived, each with the line it is on:\n", survived
    if (survived == 0) { print "  none" }
    for (i = 1; i <= livedcount; i++) { print "  " lived[i] }

    printf "\nmutation score %.2f%% (%d detected of %d scored: %d killed, %d timed out, %d survived, %d never reached)\n", \
        100 * (killed + timedout) / scored, killed + timedout, scored, killed, timedout, survived, uncovered
    print "The score is reported and not enforced. What a reader acts on is the list above it."
}
' "$report"
