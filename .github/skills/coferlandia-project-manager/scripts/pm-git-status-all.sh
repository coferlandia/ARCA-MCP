#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/discovery.sh
source "${script_dir}/lib/discovery.sh"

print_help() {
  cat <<'EOF'
Usage: pm-git-status-all.sh --config <path> [--json] [--fail-on-dirty]
Description: Report git status for all detected projects without modifying repositories.
EOF
}

config_path=""
json_output=false
fail_on_dirty=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --config"
      config_path="$1"
      ;;
    --json)
      json_output=true
      ;;
    --fail-on-dirty)
      fail_on_dirty=true
      ;;
    --help|-h)
      print_help
      exit 0
      ;;
    *)
      die "Unknown argument: $1"
      ;;
  esac
  shift
done

config_path="$(pm_resolve_config_path "${config_path}")"
pm_require_file "${config_path}"

scan_args=(--config "${config_path}")
scan_args+=(--json)
scan_args+=(--include-dirty --include-remotes)

scan_output="$("${script_dir}/pm-scan-repos.sh" "${scan_args[@]}")"

if [[ "${json_output}" == true ]]; then
  printf '%s\n' "${scan_output}"
else
  python_cmd="$(pm_python_cmd)"
  "${python_cmd}" - "${scan_output}" <<'PY'
import json
import sys

payload = json.loads(sys.argv[1])
print("status: ok")
print(f"projects_detected: {payload.get('projects_detected', len(payload.get('projects', [])))}")
for project in payload.get("projects", []):
    git = project.get("git", {})
    print(
        f"{project.get('project_slug', '')}: "
        f"branch={git.get('current_branch', '')} "
        f"dirty={git.get('dirty', False)} "
        f"untracked={git.get('untracked', False)}"
    )
PY
fi

if [[ "${fail_on_dirty}" == true ]]; then
  python_cmd="$(pm_python_cmd)"
  "${python_cmd}" - "${scan_output}" <<'PY'
import json
import sys

payload = json.loads(sys.argv[1])
dirty_projects = [
    project["project_slug"]
    for project in payload.get("projects", [])
    if project.get("git", {}).get("dirty") or project.get("git", {}).get("untracked")
]

if dirty_projects:
    raise SystemExit(1)
PY
fi
