#!/usr/bin/env bash
# Reads a Cobertura report produced by the test run and refuses when the pure
# logic named in .github/coverage/pure-logic.txt falls below the floor written
# there.
#
# It fails closed in three directions rather than one. A listed file that is not
# in the tree is a list that has drifted, and a list that names nothing real
# would otherwise pass by covering an empty set. A listed file the report does
# not mention is a file that stopped being compiled into the assembly under
# test, which reads exactly like full coverage if only the ratio is checked. A
# ratio below the floor is the case the check is named for.
#
# Usage: refuse-uncovered-logic.sh <cobertura.xml> <list> <project-directory>
set -euo pipefail

report=${1:?the Cobertura report to read}
list=${2:?the file listing the pure logic and its floor}
project=${3:?the project directory the report filenames are relative to}

if [ ! -f "$report" ]; then
    echo "No coverage report at $report. A run that produced no report says nothing about coverage." >&2
    exit 1
fi

line_floor=$(sed -n 's/^# *line-floor: *\([0-9]*\).*/\1/p' "$list")
branch_floor=$(sed -n 's/^# *branch-floor: *\([0-9]*\).*/\1/p' "$list")

if [ -z "$line_floor" ] || [ -z "$branch_floor" ]; then
    echo "$list carries no line-floor or no branch-floor, so there is no number to hold anything to." >&2
    exit 1
fi

wanted=$(grep -v '^[[:space:]]*#' "$list" | grep -v '^[[:space:]]*$' || true)

if [ -z "$wanted" ]; then
    echo "$list names no file. An empty set is above every floor and proves nothing." >&2
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
    echo "A list that names a file nobody can find measures less than it claims to." >&2
    exit 1
fi

# The report writes filenames with the separator of the machine that produced
# it, so both spellings are folded to one before anything is compared.
printf '%s\n' "$wanted" | tr '\\' '/' > /tmp/pure-logic-wanted.$$
trap 'rm -f /tmp/pure-logic-wanted.$$' EXIT

awk -v wantedfile="/tmp/pure-logic-wanted.$$" \
    -v line_floor="$line_floor" \
    -v branch_floor="$branch_floor" '
BEGIN {
    while ((getline path < wantedfile) > 0) {
        if (path != "") { wanted[path] = 1 }
    }
}
/<class / {
    inside = 0
    inmethod = 0
    if (match($0, /filename="[^"]*"/)) {
        file = substr($0, RSTART + 10, RLENGTH - 11)
        gsub(/\\/, "/", file)
        if (file in wanted) { inside = 1; seen[file] = 1 }
    }
    next
}
/<\/class>/ { inside = 0; next }
# Every line of a class is written twice, once under the method it belongs to
# and once in the class-level block beneath them. Only the class-level block is
# counted, so a file with many small methods is not weighted differently from
# one with few, and the printed counts are the lines the file has.
/<method / { inmethod = 1; next }
/<\/method>/ { inmethod = 0; next }
inside && !inmethod && /<line / {
    hits = 0
    if (match($0, /hits="[0-9]+"/)) {
        hits = substr($0, RSTART + 6, RLENGTH - 7) + 0
    }
    lines_valid[file]++
    total_lines_valid++
    if (hits > 0) { lines_covered[file]++; total_lines_covered++ }

    if (match($0, /condition-coverage="[0-9]+% \([0-9]+\/[0-9]+\)"/)) {
        pair = substr($0, RSTART, RLENGTH)
        sub(/.*\(/, "", pair)
        sub(/\).*/, "", pair)
        split(pair, halves, "/")
        branches_covered[file] += halves[1]
        branches_valid[file] += halves[2]
        total_branches_covered += halves[1]
        total_branches_valid += halves[2]
    }
}
END {
    absent = ""
    for (path in wanted) {
        if (!(path in seen)) { absent = absent "  " path "\n" }
    }
    if (absent != "") {
        printf "These are listed for the floor and appear nowhere in the coverage report:\n%s", absent > "/dev/stderr"
        print "A file the report does not mention was not compiled into the assembly under test, which is not the same thing as a file with no uncovered line." > "/dev/stderr"
        exit 1
    }

    n = 0
    for (path in seen) { order[++n] = path }
    for (i = 1; i < n; i++) {
        for (j = i + 1; j <= n; j++) {
            if (order[j] < order[i]) { t = order[i]; order[i] = order[j]; order[j] = t }
        }
    }

    printf "%-52s %-16s %s\n", "file", "lines", "branches"
    for (i = 1; i <= n; i++) {
        path = order[i]
        lr = lines_valid[path] > 0 ? 100 * lines_covered[path] / lines_valid[path] : 100
        br = branches_valid[path] > 0 ? 100 * branches_covered[path] / branches_valid[path] : 100
        printf "%-52s %6.2f%% (%3d/%3d) %6.2f%% (%3d/%3d)\n", path, \
            lr, lines_covered[path], lines_valid[path], \
            br, branches_covered[path], branches_valid[path]
    }

    line_rate = total_lines_valid > 0 ? 100 * total_lines_covered / total_lines_valid : 100
    branch_rate = total_branches_valid > 0 ? 100 * total_branches_covered / total_branches_valid : 100

    printf "\n%-52s %6.2f%% (%3d/%3d) %6.2f%% (%3d/%3d)\n", "the set, against floors " line_floor "% and " branch_floor "%", \
        line_rate, total_lines_covered, total_lines_valid, \
        branch_rate, total_branches_covered, total_branches_valid

    failed = 0
    if (line_rate < line_floor) {
        printf "Line coverage over the pure logic is %.2f%%, below the floor of %d%%.\n", line_rate, line_floor > "/dev/stderr"
        failed = 1
    }
    if (branch_rate < branch_floor) {
        printf "Branch coverage over the pure logic is %.2f%%, below the floor of %d%%.\n", branch_rate, branch_floor > "/dev/stderr"
        failed = 1
    }
    if (failed) {
        print "The per-file numbers above say which file to look at." > "/dev/stderr"
        exit 1
    }
}
' "$report"
