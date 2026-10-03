#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"
# shellcheck source=./lib/reporting.sh
source "${script_dir}/lib/reporting.sh"

print_help() {
  cat <<'EOF'
Usage: pm-clean-worktrees.sh --config <path> [--json] [--dry-run] [--apply] [--output-dir <dir>]
Description: List worktrees, classify them, and suggest safe cleanup actions.

Cleanup stays suggestion-first and approval-gated. The PM never deletes dirty
worktrees, worktrees it cannot associate safely, branches, or anything by force.
Branch and worktree lifecycle remain delegated to Superpowers.

Options:
  --config <path>    Required path to the PM config.json.
  --json             Emit machine-readable JSON instead of Markdown.
  --dry-run          Explicit dry-run (default; accepted for clarity).
  --apply            Rejected. Cleanup remains advisory-only and delegated to Superpowers.
  --output-dir <dir> Write report to this directory (default: .coferlandia/project-manager/reports/).
                     Ignored when --json is used (prints to stdout).
  -h, --help         Show this help and exit.

Output location:
  Default: .coferlandia/project-manager/reports/
EOF
}

config_path=""
json_output=false
output_dir=""
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
    --output-dir)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --output-dir"
      output_dir="$1"
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

default_branch="$(pm_config_default_branch "${config_path}")"
[[ -n "${default_branch}" ]] || default_branch="main"

format_flag="json"
[[ "${json_output}" == false ]] && format_flag="markdown"

python_cmd="$(pm_python_cmd)"
args=(
  "${script_dir}/lib/reporting.py"
  worktree-cleanup
  --projects-file "${projects_file}"
  --format "${format_flag}"
  --default-branch "${default_branch}"
  --mode dry-run
)
if [[ "${apply}" == true ]]; then
  die "pm-clean-worktrees.sh is advisory-only; apply mode is disabled and cleanup stays delegated to Superpowers"
fi

report_output="$("${python_cmd}" "${args[@]}")"

if [[ "${json_output}" == true ]]; then
  printf '%s\n' "${report_output}"
else
  if [[ -n "${output_dir}" ]]; then
    resolved_dir="$(pm_report_output_dir "${output_dir}")"
    written_path="$(printf '%s\n' "${report_output}" | pm_write_report "${resolved_dir}" "worktree-cleanup" "md")"
    log_info "Report written to: ${written_path}"
  else
    printf '%s\n' "${report_output}"
  fi
fi
