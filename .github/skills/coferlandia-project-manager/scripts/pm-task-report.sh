#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"
# shellcheck source=./lib/reporting.sh
source "${script_dir}/lib/reporting.sh"

print_help() {
  cat <<'EOF'
Usage: pm-task-report.sh --config <path> --task <issue-ref> [--json] [--output-dir <dir>]
Description: Generate a report for one GitHub Issue in the managed portfolio.

Issue references:
  project-slug#142   Preferred and unambiguous across a portfolio.
  #142 or 142        Allowed only when that issue number resolves in one managed repo.
  GitHub Issue URL   Accepted when the issue number resolves unambiguously.

Options:
  --config <path>    Required path to the PM config.json.
  --task <issue-ref> Required GitHub Issue reference.
  --json             Emit machine-readable JSON instead of Markdown.
  --output-dir <dir> Write report to this directory.
  -h, --help         Show this help and exit.
EOF
}

config_path=""
json_output=false
output_dir=""
task_ref=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config) shift; [[ $# -gt 0 ]] || die "Missing value for --config"; config_path="$1" ;;
    --task) shift; [[ $# -gt 0 ]] || die "Missing value for --task"; task_ref="$1" ;;
    --json) json_output=true ;;
    --output-dir) shift; [[ $# -gt 0 ]] || die "Missing value for --output-dir"; output_dir="$1" ;;
    --help|-h) print_help; exit 0 ;;
    *) die "Unknown argument: $1" ;;
  esac
  shift
done

config_path="$(pm_resolve_config_path "${config_path}")"
pm_require_file "${config_path}"
[[ -n "${task_ref}" ]] || die "Missing required --task <issue-ref>"
projects_file="$(pm_resolve_projects_path "" "${config_path}")"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"
default_branch="$(pm_config_default_branch "${config_path}")"
[[ -n "${default_branch}" ]] || default_branch="main"
format_flag="json"
[[ "${json_output}" == false ]] && format_flag="markdown"
python_cmd="$(pm_python_cmd)"
report_output="$("${python_cmd}" "${script_dir}/lib/reporting.py" task-report \
  --projects-file "${projects_file}" --format "${format_flag}" \
  --default-branch "${default_branch}" --task "${task_ref}")"

if [[ "${json_output}" == true ]]; then
  printf '%s\n' "${report_output}"
elif [[ -n "${output_dir}" ]]; then
  resolved_dir="$(pm_report_output_dir "${output_dir}")"
  safe_ref="${task_ref//[^A-Za-z0-9._-]/-}"
  written_path="$(printf '%s\n' "${report_output}" | pm_write_report "${resolved_dir}" "issue-report-${safe_ref}" "md")"
  log_info "Report written to: ${written_path}"
else
  printf '%s\n' "${report_output}"
fi
