#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"
# shellcheck source=./lib/reporting.sh
source "${script_dir}/lib/reporting.sh"

print_help() {
  cat <<'EOF'
Usage: pm-health-check.sh --config <path> [--json] [--output-dir <dir>] [--stale-days <n>]
Description: Summarize portfolio health, sync gaps, and maintenance needs.

Options:
  --config <path>      Required path to the PM config.json.
  --json               Emit machine-readable JSON instead of Markdown.
  --output-dir <dir>   Write report to this directory (default: .coferlandia/project-manager/reports/).
                        Ignored when --json is used (prints to stdout).
  --stale-days <n>     Consider repos stale if no commit in this many days (default: 30).
  -h, --help           Show this help and exit.

Output location:
  Default: .coferlandia/project-manager/reports/
EOF
}

config_path=""
json_output=false
output_dir=""
stale_days=""

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
    --output-dir)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --output-dir"
      output_dir="$1"
      ;;
    --stale-days)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --stale-days"
      stale_days="$1"
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

reporting_args=(
  --projects-file "${projects_file}"
  --format "${format_flag}"
  --default-branch "${default_branch}"
)

if [[ -n "${stale_days}" ]]; then
  reporting_args+=(--stale-days "${stale_days}")
fi

python_cmd="$(pm_python_cmd)"
report_output="$("${python_cmd}" "${script_dir}/lib/reporting.py" health-check "${reporting_args[@]}")"

if [[ "${json_output}" == true ]]; then
  printf '%s\n' "${report_output}"
else
  if [[ -n "${output_dir}" ]]; then
    resolved_dir="$(pm_report_output_dir "${output_dir}")"
    written_path="$(printf '%s\n' "${report_output}" | pm_write_report "${resolved_dir}" "health-check" "md")"
    log_info "Report written to: ${written_path}"
  else
    printf '%s\n' "${report_output}"
  fi
fi
