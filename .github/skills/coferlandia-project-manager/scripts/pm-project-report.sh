#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"
# shellcheck source=./lib/reporting.sh
source "${script_dir}/lib/reporting.sh"

print_help() {
  cat <<'EOF'
Usage: pm-project-report.sh --config <path> --project <slug> [--json] [--output-dir <dir>]
Description: Generate a report for one managed project.

Options:
  --config <path>   Required path to the PM config.json.
  --project <slug>  Required project slug (as listed in projects.json).
  --json            Emit machine-readable JSON instead of Markdown.
  --output-dir <dir> Write report to this directory (default: .coferlandia/project-manager/reports/).
                     Ignored when --json is used (prints to stdout).
  -h, --help        Show this help and exit.

Output location:
  Default: .coferlandia/project-manager/reports/
EOF
}

config_path=""
json_output=false
output_dir=""
project_slug=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --config"
      config_path="$1"
      ;;
    --project)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --project"
      project_slug="$1"
      ;;
    --json)
      json_output=true
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
[[ -n "${project_slug}" ]] || die "Missing required --project <slug>"
projects_file="$(pm_resolve_projects_path "" "${config_path}")"
[[ -n "${projects_file}" ]] || die "projects_file could not be resolved"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"

default_branch="$(pm_config_default_branch "${config_path}")"
[[ -n "${default_branch}" ]] || default_branch="main"

format_flag="json"
[[ "${json_output}" == false ]] && format_flag="markdown"

python_cmd="$(pm_python_cmd)"
report_output="$("${python_cmd}" "${script_dir}/lib/reporting.py" project-report \
  --projects-file "${projects_file}" \
  --format "${format_flag}" \
  --default-branch "${default_branch}" \
  --project "${project_slug}" \
)"

# Check for error status in JSON output.
if [[ "${json_output}" == true ]]; then
  printf '%s\n' "${report_output}"
else
  if [[ -n "${output_dir}" ]]; then
    resolved_dir="$(pm_report_output_dir "${output_dir}")"
    written_path="$(printf '%s\n' "${report_output}" | pm_write_report "${resolved_dir}" "project-report-${project_slug}" "md")"
    log_info "Report written to: ${written_path}"
  else
    printf '%s\n' "${report_output}"
  fi
fi
