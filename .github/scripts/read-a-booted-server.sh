#!/usr/bin/env bash
#
# Reads what a booted server said about this plugin, and refuses a boot that did
# not load it.
#
# The plugin is built against a server package and every test in the suite drives
# it through doubles, so the moment it meets a real server is the one moment the
# suite cannot see. The server loads the assembly under its own rules, builds the
# scheduled task out of its own container, and lists what it loaded on its own
# routes. This reads four captures of that moment, taken from a server the
# workflow started with the built plugin installed, and compares each against
# what this tree says the plugin is.
#
# Usage: read-a-booted-server.sh <directory of captures>
#
# The directory holds:
#   plugins.json   what GET /Plugins returned
#   tasks.json     what GET /ScheduledTasks returned
#   page.html      what GET /web/ConfigurationPage?name=<the page> returned
#   server.log     the server's console output from the boot
#
# What each is compared against is read from the tree rather than typed here:
# the identity, the version and the assembly from build.yaml, the task key, the
# task name and the page name from the claim record under
# interoperability/claims/, and the page's bytes from the embedded resource the
# plugin ships. The record is resolved here rather than handed in, so the workflow
# that captures a boot does not carry a second copy of what the plugin claims.
#
# It states how much it read before it says anything about the result, so a run
# handed an empty listing, or a log that never mentions the plugin, cannot be read
# as a server that loaded it and had nothing to complain about.

set -eu

captures=${1:?the directory holding the captures from a booted server}
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)

manifest="$root/build.yaml"
record="$root/interoperability/claims/jellyfin-plugin-whisper-subtitles.json"
page_source="$root/Jellyfin.Plugin.WhisperSubtitles/Configuration/configPage.html"

if [ ! -d "$captures" ]; then
  echo "There is no directory at $captures, so nothing a server said was read." >&2
  exit 1
fi

for capture in plugins.json tasks.json page.html server.log; do
  if [ ! -f "$captures/$capture" ]; then
    echo "There is no $capture under $captures, so this run did not read what the server said." >&2
    exit 1
  fi
done

for source in "$manifest" "$record" "$page_source"; do
  if [ ! -f "$source" ]; then
    echo "There is no $source, so there is nothing to compare the server's answer against." >&2
    exit 1
  fi
done

# The same reader the publish route uses on build.yaml: a quoted scalar ends at
# its closing quote, an unquoted one at a comment or the end of the line.
read_scalar() {
  awk -v key="$1" '
    index($0, key ":") == 1 {
      value = substr($0, length(key) + 2)
      sub(/\r$/, "", value)
      sub(/^[[:space:]]+/, "", value)
      sub(/[[:space:]]+$/, "", value)
      if (substr(value, 1, 1) == "\"") {
        value = substr(value, 2)
        end = index(value, "\"")
        if (end > 0) { value = substr(value, 1, end - 1) }
      } else if (substr(value, 1, 1) == "'\''") {
        value = substr(value, 2)
        end = index(value, "'\''")
        if (end > 0) { value = substr(value, 1, end - 1) }
      } else {
        sub(/[[:space:]]*#.*$/, "", value)
        sub(/[[:space:]]+$/, "", value)
      }
      print value
      exit
    }' "$manifest"
}

name=$(read_scalar name)
guid=$(read_scalar guid | tr '[:upper:]' '[:lower:]')
version=$(read_scalar version)
assembly=$(awk '
  /^artifacts:/ { inside = 1; next }
  inside && /^- / {
    value = $0
    sub(/^- *"?/, "", value)
    sub(/"?[[:space:]]*\r?$/, "", value)
    sub(/\.dll$/, "", value)
    print value
    exit
  }' "$manifest")

for pair in "name=$name" "guid=$guid" "version=$version" "artifacts=$assembly"; do
  if [ -z "${pair#*=}" ]; then
    echo "build.yaml carries no ${pair%%=*}, so there is nothing to compare the server's answer against." >&2
    exit 1
  fi
done

task_key=$(jq -r '.taskKeys[0] // empty' "$record")
task_name=$(jq -r '.taskNames[0] // empty' "$record")
page_name=$(jq -r '.configurationPages[0] // empty' "$record")

for pair in "taskKeys=$task_key" "taskNames=$task_name" "configurationPages=$page_name"; do
  if [ -z "${pair#*=}" ]; then
    echo "The claim record carries no ${pair%%=*}, so there is nothing to compare the server's answer against." >&2
    exit 1
  fi
done

# How much was read, before anything is said about it.

if ! plugin_count=$(jq 'if type == "array" then length else -1 end' "$captures/plugins.json" 2>&1); then
  echo "plugins.json is not JSON, so what the server listed was not read: $plugin_count" >&2
  exit 1
fi
if ! task_count=$(jq 'if type == "array" then length else -1 end' "$captures/tasks.json" 2>&1); then
  echo "tasks.json is not JSON, so what the server listed was not read: $task_count" >&2
  exit 1
fi
if [ "$plugin_count" -lt 0 ]; then
  echo "plugins.json is not a list, so what the server listed was not read." >&2
  exit 1
fi
if [ "$task_count" -lt 0 ]; then
  echo "tasks.json is not a list, so what the server listed was not read." >&2
  exit 1
fi

page_bytes=$(wc -c < "$captures/page.html" | tr -d ' ')
entry_count=$(grep -cE '^\[[0-9]{2}:[0-9]{2}:[0-9]{2}\] \[[A-Z]{3}\]' "$captures/server.log" || true)

echo "read $plugin_count plugin(s), $task_count task(s), a page of $page_bytes byte(s) and $entry_count log entr$([ "$entry_count" -eq 1 ] && echo y || echo ies)"

# The log first. A boot that never loaded the assembly makes every other capture
# a statement about a server this plugin was not part of.

if [ "$entry_count" -eq 0 ]; then
  echo "server.log carries no log entry, so nothing here says the server started." >&2
  exit 1
fi

loaded="Loaded assembly $assembly, Version=$version"
if ! grep -qF "$loaded" "$captures/server.log"; then
  echo "The log never says \"$loaded\" was loaded, so a log carrying no error about the plugin says nothing about a boot that included it." >&2
  exit 1
fi

# An entry is a timestamped line and every line up to the next one, which is
# where the server prints an exception. The level is read off the entry's first
# line and the plugin is looked for anywhere in it, so an error whose message is
# generic and whose stack trace names this plugin is still an error naming it.
errors=$(awk -v assembly="$assembly" -v name="$name" '
  function flush() {
    if (entry != "" && (level == "ERR" || level == "FTL") && (index(entry, assembly) > 0 || index(entry, name) > 0)) {
      print entry
    }
    entry = ""
    level = ""
  }
  /^\[[0-9][0-9]:[0-9][0-9]:[0-9][0-9]\] \[[A-Z][A-Z][A-Z]\]/ {
    flush()
    level = substr($0, 13, 3)
    entry = $0
    next
  }
  { if (entry != "") { entry = entry "\n" $0 } }
  END { flush() }' "$captures/server.log")

if [ -n "$errors" ]; then
  echo "The log carries an error that names the plugin:" >&2
  echo "$errors" >&2
  exit 1
fi

# Then what the server lists.

if [ "$plugin_count" -eq 0 ]; then
  echo "The server listed no plugin at all, so this run read nothing about whether it loaded this one." >&2
  exit 1
fi

# The server writes the id without its dashes, build.yaml with them, so both are
# read without before they are compared.
bare_guid=$(printf '%s' "$guid" | tr -d '-')
listed=$(jq --arg id "$bare_guid" '[.[] | select(((.Id // "") | ascii_downcase | gsub("-"; "")) == $id)]' "$captures/plugins.json")
listed_count=$(printf '%s' "$listed" | jq 'length')

if [ "$listed_count" -eq 0 ]; then
  echo "The server lists $plugin_count plugin(s) and none carries the id $guid, so the plugin was not loaded. Listed:" >&2
  jq -r '.[] | "  \(.Name // "?") \(.Version // "?") \(.Status // "?")"' "$captures/plugins.json" >&2
  exit 1
fi
if [ "$listed_count" -gt 1 ]; then
  echo "The server lists the id $guid $listed_count times, so two versions of the plugin are installed side by side." >&2
  exit 1
fi

listed_status=$(printf '%s' "$listed" | jq -r '.[0].Status // ""')
listed_version=$(printf '%s' "$listed" | jq -r '.[0].Version // ""')
listed_name=$(printf '%s' "$listed" | jq -r '.[0].Name // ""')

if [ "$listed_status" != "Active" ]; then
  echo "The server lists the plugin with status \"$listed_status\" rather than Active, so it is installed and not running." >&2
  exit 1
fi
if [ "$listed_version" != "$version" ]; then
  echo "The server lists the plugin at version \"$listed_version\" and build.yaml says \"$version\", so what loaded is not what this tree built." >&2
  exit 1
fi
if [ "$listed_name" != "$name" ]; then
  echo "The server lists the plugin as \"$listed_name\" and build.yaml says \"$name\"." >&2
  exit 1
fi

if [ "$task_count" -eq 0 ]; then
  echo "The server listed no scheduled task at all, so this run read nothing about whether it built this plugin's." >&2
  exit 1
fi

task=$(jq --arg key "$task_key" '[.[] | select((.Key // "") == $key)]' "$captures/tasks.json")
task_found=$(printf '%s' "$task" | jq 'length')

if [ "$task_found" -eq 0 ]; then
  echo "The server lists $task_count scheduled task(s) and no task carries the key $task_key, so the server did not build this plugin's task. Listed:" >&2
  jq -r '.[] | "  \(.Key // "?") \(.Name // "?")"' "$captures/tasks.json" >&2
  exit 1
fi

listed_task_name=$(printf '%s' "$task" | jq -r '.[0].Name // ""')
if [ "$listed_task_name" != "$task_name" ]; then
  echo "The server lists the task $task_key as \"$listed_task_name\" and the claim record says \"$task_name\"." >&2
  exit 1
fi

# Then the page. The server serves the embedded resource as it is, so what came
# back is compared against the file in this tree byte for byte, with only the
# line endings a checkout may have changed set aside.

if [ "$page_bytes" -eq 0 ]; then
  echo "The server answered the page \"$page_name\" with no bytes, so the page did not load." >&2
  exit 1
fi

if ! difference=$(diff <(tr -d '\r' < "$page_source") <(tr -d '\r' < "$captures/page.html")); then
  echo "The page the server served as \"$page_name\" differs from the one this tree ships:" >&2
  printf '%s\n' "$difference" | head -n 20 >&2
  exit 1
fi

echo "the server loaded $name $version as Active, lists its task $task_key as \"$task_name\", served its page \"$page_name\" as shipped, and logged no error naming it"
