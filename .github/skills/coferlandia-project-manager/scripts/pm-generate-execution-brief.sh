#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-generate-execution-brief.sh --config <path> --task <task-id> [--json] [--dry-run]
Description: Generate a brief for the next approved Superpowers-guided action without executing the work.
EOF
}

config_path=""
task_id=""
json_output=false
dry_run=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --config"
      config_path="$1"
      ;;
    --task)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --task"
      task_id="$1"
      ;;
    --json)
      json_output=true
      ;;
    --dry-run)
      dry_run=true
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
[[ -n "${task_id}" ]] || die "Missing required --task <task-id>"

projects_file="$(pm_resolve_projects_path "" "${config_path}")"
[[ -n "${projects_file}" ]] || die "projects_file could not be resolved"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"

python_cmd="$(pm_python_cmd)"
args=(
  "${script_dir}/lib/board_actions.py"
  generate-execution-brief
  --projects-file "${projects_file}"
  --task "${task_id}"
)
if [[ "${dry_run}" == true ]]; then
  args+=(--dry-run)
fi

payload="$("${python_cmd}" "${args[@]}")"
printf '%s\n' "${payload}"
