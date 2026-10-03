#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-validate-task-transition.sh --config <path> --task <task-id> --target-status <status> [--json]
Description: Validate whether a PM task transition is authorized and safe to action.
EOF
}

config_path=""
task_id=""
target_status=""
json_output=false

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
    --target-status)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --target-status"
      target_status="$1"
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
[[ -n "${task_id}" ]] || die "Missing required --task <task-id>"
[[ -n "${target_status}" ]] || die "Missing required --target-status <status>"

projects_file="$(pm_resolve_projects_path "" "${config_path}")"
[[ -n "${projects_file}" ]] || die "projects_file could not be resolved"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"

python_cmd="$(pm_python_cmd)"
payload="$("${python_cmd}" "${script_dir}/lib/board_actions.py" validate-task-transition \
  --projects-file "${projects_file}" \
  --task "${task_id}" \
  --target-status "${target_status}" \
)"

printf '%s\n' "${payload}"
