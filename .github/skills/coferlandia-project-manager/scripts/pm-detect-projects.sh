#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/discovery.sh
source "${script_dir}/lib/discovery.sh"

print_help() {
  cat <<'EOF'
Usage: pm-detect-projects.sh --config <path> [--projects-file <path>] [--json]
Description: List the active projects registered in projects.json.
EOF
}

config_path=""
projects_file_arg=""
json_output=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --config"
      config_path="$1"
      ;;
    --projects-file)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --projects-file"
      projects_file_arg="$1"
      ;;
    --json)
      json_output=true
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
projects_file="$(pm_resolve_projects_path "${projects_file_arg}" "${config_path}")"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"

mapfile -t project_paths < <(pm_projects_paths "${projects_file}")

if [[ "${json_output}" == true ]]; then
  python_cmd="$(pm_python_cmd)"
  "${python_cmd}" - "${projects_file}" "${project_paths[@]}" <<'PY'
import json
import sys
from pathlib import Path

projects_file = sys.argv[1]
project_paths = sys.argv[2:]
projects = [
    {
        "project_slug": Path(path).name,
        "repo_path": path,
    }
    for path in project_paths
]
print(json.dumps({
    "status": "ok",
    "projects_file": projects_file,
    "projects_detected": len(projects),
    "projects": projects,
}, indent=2))
PY
else
  printf 'status: ok\n'
  printf 'projects_file: %s\n' "${projects_file}"
  printf 'projects_detected: %s\n' "${#project_paths[@]}"
  for project_path in "${project_paths[@]}"; do
    printf '%s\n' "${project_path}"
  done
fi
