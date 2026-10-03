#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-sync-from-repos.sh --config <path> [--json] [--dry-run] [--apply]
Description: Map repo documentation into PM state and persist the PM runtime artifacts.

Options:
  --config <path>   Required path to the PM config.json.
  --json            Emit machine-readable JSON instead of human-readable text.
  --dry-run         Explicit dry-run (default; accepted for clarity).
  --apply           Persist the PM runtime artifacts.
  -h, --help        Show this help and exit.
EOF
}

config_path=""
json_output=false
apply=false

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
    --dry-run)
      ;;
    --apply)
      apply=true
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

projects_file="$(pm_resolve_projects_path "" "${config_path}")"
[[ -n "${projects_file}" ]] || die "projects_file could not be resolved"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"

format_flag="text"
[[ "${json_output}" == true ]] && format_flag="json"

python_cmd="$(pm_python_cmd)"
args=(
  "${script_dir}/lib/archivist.py"
  sync-from-repos
  --projects-file "${projects_file}"
  --config "${config_path}"
  --format "${format_flag}"
)
if [[ "${apply}" == true ]]; then
  args+=(--apply)
fi

exec "${python_cmd}" "${args[@]}"
