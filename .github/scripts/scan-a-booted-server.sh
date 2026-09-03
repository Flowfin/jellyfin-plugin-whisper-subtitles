#!/usr/bin/env bash
#
# Scans a booted server for what is claimed twice on it, and derives what this
# plugin claims from the server's own answer rather than from the source.
#
# A plugin is a guest on a server it shares. What it claims there is a scheduled
# task key and a name, a page in the dashboard, the paths the server answers on
# its behalf, and the file the server keeps its configuration in. Two plugins are
# each correct alone and wrong together when they claim one of those, and the
# only place that shows is a server with both of them on it. So this reads what
# such a server lists, and refuses a value it lists twice, naming both claimants
# and the value rather than only saying that something clashed.
#
# It also does a second job on a server carrying this plugin alone: it derives the
# set this plugin claims from what the server attributes to it, and compares that
# against the record under interoperability/claims/ in both directions. A page the
# server attributes to this plugin that the record does not claim is a claim that
# arrived in silence; a page the record claims that the server does not attribute
# to it is a record describing a plugin that is not the one running.
#
# Usage: scan-a-booted-server.sh <directory of captures> [claim record]
#
# The directory holds what a server the workflow started answered:
#   plugins.json   GET /Plugins
#   tasks.json     GET /ScheduledTasks
#   pages.json     GET /web/ConfigurationPages
#   openapi.json   GET /api-docs/openapi.json
#
# The claim record defaults to this plugin's own under interoperability/claims/,
# and the identity the server is asked about is the guid in build.yaml, so the
# workflow carries no second copy of either.
#
# It states how much it read before it says anything about the result, so a run
# handed an empty listing cannot be read as a server that was scanned and had
# nothing claimed twice.
#
# THE BOUND, STATED ONCE HERE AND ON EVERY LINE THAT MEETS IT. The server
# attributes a page to a plugin and a configuration file to a plugin, and
# attributes a task and a path to nobody: the task list and the route document say
# what is registered and not whose it is. So pages and the configuration file are
# derived and compared as sets, while for task keys, task names and routes what is
# read is that a value two things registered is refused and that every value the
# record claims is registered. A task this plugin registers and the record omits
# is not seen here; ClaimRecordTests holds that from the plugin's own type.

set -eu

captures=${1:?the directory holding the captures from a booted server}
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
record=${2:-"$root/interoperability/claims/jellyfin-plugin-whisper-subtitles.json"}
manifest="$root/build.yaml"

if [ ! -d "$captures" ]; then
  echo "There is no directory at $captures, so nothing a server said was scanned." >&2
  exit 1
fi

for capture in plugins.json tasks.json pages.json openapi.json; do
  if [ ! -f "$captures/$capture" ]; then
    echo "There is no $capture under $captures, so this run did not read what the server said." >&2
    exit 1
  fi
done

for source in "$manifest" "$record"; do
  if [ ! -f "$source" ]; then
    echo "There is no $source, so there is nothing to compare the server's answer against." >&2
    exit 1
  fi
done

# The same reader read-a-booted-server.sh uses on build.yaml: a quoted scalar ends
# at its closing quote, an unquoted one at a comment or the end of the line.
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

# The server writes an id without its dashes and build.yaml with them, so every id
# is read the same way before it is compared: lower case, no dashes.
guid=$(read_scalar guid | tr '[:upper:]' '[:lower:]' | tr -d '-')
if [ -z "$guid" ]; then
  echo "build.yaml carries no guid, so the server cannot be asked which plugin is this one." >&2
  exit 1
fi

if ! jq empty "$record" 2>/dev/null; then
  echo "$record is not readable as JSON, so what this plugin claims is unknown." >&2
  exit 1
fi
plugin=$(jq -r '.plugin // empty' "$record")
if [ -z "$plugin" ]; then
  echo "$record names no plugin, so nothing the server attributes could be compared against it." >&2
  exit 1
fi
for kind in taskKeys taskNames configurationPages routes; do
  if [ "$(jq -r --arg kind "$kind" 'has($kind) and (.[$kind] | type == "array") | tostring' "$record")" != "true" ]; then
    echo "$record has no $kind list, so it does not say what $plugin claims of that kind." >&2
    exit 1
  fi
done

# How much was read, before anything is said about it.

count_of_list() {
  local file=$1 what=$2 count
  if ! count=$(jq 'if type == "array" then length else -1 end' "$file" 2>&1); then
    echo "$what is not JSON, so what the server listed was not read: $count" >&2
    exit 1
  fi
  if [ "$count" -lt 0 ]; then
    echo "$what is not a list, so what the server listed was not read." >&2
    exit 1
  fi
  echo "$count"
}

plugin_count=$(count_of_list "$captures/plugins.json" plugins.json)
task_count=$(count_of_list "$captures/tasks.json" tasks.json)
page_count=$(count_of_list "$captures/pages.json" pages.json)

# The route document is the one capture whose absence is itself a reading. A
# server holding two controllers on one path cannot build its own document and
# answers the request with an error page instead, so a capture that is not a
# document is read as the route collision it would be, and quoted, rather than
# as a capture to skip.
if ! route_count=$(jq 'if (type == "object") and ((.paths // null) | type == "object") then (.paths | length) else -1 end' "$captures/openapi.json" 2>&1); then
  echo "openapi.json is not a route document the server could build, which is what a server holding two controllers on one path answers with. It answered:" >&2
  head -c 300 "$captures/openapi.json" >&2
  echo >&2
  exit 1
fi
if [ "$route_count" -lt 0 ]; then
  echo "openapi.json carries no paths object, so it is not the route document the server builds and nothing about the routes was read." >&2
  exit 1
fi

echo "read $plugin_count plugin(s), $task_count task(s), $page_count page(s) and $route_count route(s) from the booted server"

if [ "$plugin_count" -eq 0 ]; then
  echo "The server listed no plugin at all, so nothing here is about a server this plugin was on." >&2
  exit 1
fi
if [ "$task_count" -eq 0 ]; then
  echo "The server listed no scheduled task at all, which no started server does, so nothing about task keys was read." >&2
  exit 1
fi
if [ "$page_count" -eq 0 ]; then
  echo "The server lists no configuration page at all, so nothing about this plugin's pages could be derived from it." >&2
  exit 1
fi
if [ "$route_count" -eq 0 ]; then
  echo "The server answered a route document naming no path, which no started server does, so nothing about routes was read." >&2
  exit 1
fi

refused=0
refuse() {
  echo "REFUSED: $1" >&2
  refused=1
}

# What the server lists twice. Each kind names both claimants and the value.

bare='ascii_downcase | gsub("-"; "")'

duplicate_ids=$(jq -r "group_by((.Id // \"\") | $bare) | map(select(length > 1)) | .[] | \"pluginId \\\"\\(.[0].Id)\\\" is listed \\(length) times, by \" + (map(\"\\\"\\(.Name // \"?\")\\\" \\(.Version // \"?\")\") | join(\" and \"))" "$captures/plugins.json")
if [ -n "$duplicate_ids" ]; then
  refuse "one plugin id is listed by more than one plugin, so the server loaded whichever came first:"
  printf '%s\n' "$duplicate_ids" >&2
fi

duplicate_files=$(jq -r 'map(select((.ConfigurationFileName // "") != "")) | group_by(.ConfigurationFileName) | map(select(length > 1)) | .[] | "configurationFileNames \"\(.[0].ConfigurationFileName)\" is kept by " + (map("\"\(.Name // "?")\"") | join(" and "))' "$captures/plugins.json")
if [ -n "$duplicate_files" ]; then
  refuse "two plugins keep their configuration in one file, so each overwrites the other's settings:"
  printf '%s\n' "$duplicate_files" >&2
fi

duplicate_keys=$(jq -r 'group_by(.Key // "") | map(select(length > 1)) | .[] | "taskKeys \"\(.[0].Key)\" is registered \(length) times, by " + (map("\"\(.Name // "?")\" (\(.Id // "?"))") | join(" and "))' "$captures/tasks.json")
if [ -n "$duplicate_keys" ]; then
  refuse "one scheduled task key is registered more than once, which is a fight over one row of the dashboard; the server does not say whose each task is, so they are named by name and id:"
  printf '%s\n' "$duplicate_keys" >&2
fi

duplicate_names=$(jq -r 'group_by(.Name // "") | map(select(length > 1)) | .[] | "taskNames \"\(.[0].Name)\" is registered \(length) times, under " + (map("\(.Key // "?") (\(.Id // "?"))") | join(" and "))' "$captures/tasks.json")
if [ -n "$duplicate_names" ]; then
  refuse "one scheduled task name is registered more than once, so an operator reading the list cannot tell which is which:"
  printf '%s\n' "$duplicate_names" >&2
fi

# A page is fetched by name, so two plugins registering one name are answered by
# whichever the server finds first. The server attributes each page to a plugin
# id, which is resolved to the plugin's name off the plugin listing.
duplicate_pages=$(jq -r --slurpfile plugins "$captures/plugins.json" "
  (\$plugins[0] | map({key: ((.Id // \"\") | $bare), value: (.Name // \"?\")}) | from_entries) as \$names
  | group_by(.Name // \"\") | map(select(length > 1)) | .[]
  | \"configurationPages \\\"\\(.[0].Name)\\\" is listed \\(length) times, by \" + (map(((.PluginId // \"\") | $bare) as \$id | (\$names[\$id] // (if \$id == \"\" then \"no plugin at all\" else \"an unlisted plugin \" + \$id end))) | join(\" and \"))" "$captures/pages.json")
if [ -n "$duplicate_pages" ]; then
  refuse "one configuration page name is registered by more than one plugin, so the dashboard serves whichever the server finds first:"
  printf '%s\n' "$duplicate_pages" >&2
fi

# What the server attributes to this plugin, derived and compared against the
# record in both directions.

mine=$(jq --arg id "$guid" "[.[] | select(((.Id // \"\") | $bare) == \$id)]" "$captures/plugins.json")
mine_count=$(printf '%s' "$mine" | jq 'length')
if [ "$mine_count" -eq 0 ]; then
  refuse "the server lists $plugin_count plugin(s) and none carries the id $guid, so nothing it attributes could be derived for $plugin. Listed:"
  jq -r '.[] | "  \(.Name // "?") \(.Version // "?") \(.Status // "?")"' "$captures/plugins.json" >&2
  echo "This booted server does not carry $plugin, so the scan stops at what it lists twice." >&2
  exit 1
fi
mine_name=$(printf '%s' "$mine" | jq -r '.[0].Name // "?"')
mine_file=$(printf '%s' "$mine" | jq -r '.[0].ConfigurationFileName // ""')

server_pages=$(jq -r --arg id "$guid" "[.[] | select(((.PluginId // \"\") | $bare) == \$id) | .Name // \"\"] | unique | .[]" "$captures/pages.json")
record_pages=$(jq -r '.configurationPages | unique | .[]' "$record")

unrecorded_pages=$(comm -23 <(printf '%s\n' "$server_pages" | sed '/^$/d' | sort) <(printf '%s\n' "$record_pages" | sed '/^$/d' | sort))
if [ -n "$unrecorded_pages" ]; then
  refuse "the server attributes a page to \"$mine_name\" that the record for $plugin does not claim, so a claim arrived without the record moving:"
  printf '%s\n' "$unrecorded_pages" | sed 's/^/  /' >&2
fi
unattributed_pages=$(comm -13 <(printf '%s\n' "$server_pages" | sed '/^$/d' | sort) <(printf '%s\n' "$record_pages" | sed '/^$/d' | sort))
if [ -n "$unattributed_pages" ]; then
  refuse "the record for $plugin claims a page that the server attributes to no plugin with the id $guid, so the record describes a plugin that is not the one running:"
  printf '%s\n' "$unattributed_pages" | sed 's/^/  /' >&2
  echo "The server attributes to \"$mine_name\": $(printf '%s\n' "$server_pages" | sed '/^$/d' | paste -sd ',' - | sed 's/,/, /g; s/^$/nothing/')" >&2
fi

# Task keys, task names and routes: the server does not say whose they are, so
# what is read is that every value the record claims is registered there.
server_keys=$(jq -r '.[] | .Key // ""' "$captures/tasks.json" | sed '/^$/d' | sort -u)
server_task_names=$(jq -r '.[] | .Name // ""' "$captures/tasks.json" | sed '/^$/d' | sort -u)
server_routes=$(jq -r '.paths | keys[]' "$captures/openapi.json" | sort -u)

missing_keys=$(comm -13 <(printf '%s\n' "$server_keys") <(jq -r '.taskKeys | unique | .[]' "$record" | sed '/^$/d' | sort))
if [ -n "$missing_keys" ]; then
  refuse "the record for $plugin claims a task key the server registers no task under, so the server did not build that task. Claimed and not registered:"
  printf '%s\n' "$missing_keys" | sed 's/^/  /' >&2
fi
missing_task_names=$(comm -13 <(printf '%s\n' "$server_task_names") <(jq -r '.taskNames | unique | .[]' "$record" | sed '/^$/d' | sort))
if [ -n "$missing_task_names" ]; then
  refuse "the record for $plugin claims a task name the server registers no task under. Claimed and not registered:"
  printf '%s\n' "$missing_task_names" | sed 's/^/  /' >&2
fi
missing_routes=$(comm -13 <(printf '%s\n' "$server_routes") <(jq -r '.routes | unique | .[]' "$record" | sed '/^$/d' | sort))
if [ -n "$missing_routes" ]; then
  refuse "the record for $plugin claims a route the server answers no path under, out of $route_count it does answer. Claimed and not answered:"
  printf '%s\n' "$missing_routes" | sed 's/^/  /' >&2
fi

if [ "$refused" -ne 0 ]; then
  echo "This booted server does not carry what it carries without a fight, or the record does not describe the plugin that is on it." >&2
  exit 1
fi

recorded_keys=$(jq '.taskKeys | unique | length' "$record")
recorded_task_names=$(jq '.taskNames | unique | length' "$record")
recorded_routes=$(jq '.routes | unique | length' "$record")
recorded_pages=$(jq '.configurationPages | unique | length' "$record")
file_count=$(jq 'map(select((.ConfigurationFileName // "") != "")) | length' "$captures/plugins.json")

echo "derived for $plugin from the server: it is listed as \"$mine_name\", keeps its configuration in \"${mine_file:-no file the server names}\", and is attributed $recorded_pages page(s): $(printf '%s\n' "$server_pages" | sed '/^$/d' | paste -sd ',' - | sed 's/,/, /g')"
echo "scanned $plugin_count plugin id(s), $file_count configuration file name(s), $task_count task key(s), $task_count task name(s), $page_count page name(s) and $route_count route(s) the booted server answers, and none is claimed twice; the page(s) the server attributes to $plugin are the page(s) the record claims, and its $recorded_keys task key(s), $recorded_task_names task name(s) and $recorded_routes route(s) are all registered there"
