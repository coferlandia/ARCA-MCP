#!/usr/bin/env bash

reporting_lib_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./config.sh
source "${reporting_lib_dir}/config.sh"
# shellcheck source=./logging.sh
source "${reporting_lib_dir}/logging.sh"

# Resolve the default report output directory relative to the working directory.
# Creates the directory if it does not exist.
pm_report_output_dir() {
  local output_dir="${1:-$(pm_repo_root)/.coferlandia/project-manager/reports}"
  mkdir -p "${output_dir}" || die "Failed to create report output directory: ${output_dir}"
  printf '%s\n' "${output_dir}"
}

# Generate an ISO-style timestamp for report filenames (e.g. 2026-07-08T143000Z).
pm_report_timestamp() {
  date -u +"%Y-%m-%dT%H%M%SZ" 2>/dev/null || date +"%Y-%m-%dT%H%M%SZ"
}

# Write a report to the output directory with a timestamp suffix.
# Reads content from stdin.
# Usage: echo "$content" | pm_write_report <output_dir> <prefix> <extension>
# Prints the written file path to stdout.
pm_write_report() {
  local output_dir="$1"
  local prefix="$2"
  local extension="$3"

  local timestamp
  timestamp="$(pm_report_timestamp)"
  local filename="${prefix}-${timestamp}.${extension}"
  local target_path="${output_dir}/${filename}"

  mkdir -p "${output_dir}" || die "Failed to create report output directory: ${output_dir}"

  cat > "${target_path}" || die "Failed to write report: ${target_path}"

  printf '%s\n' "${target_path}"
}
