#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-generate-config.sh --config <path> [--dry-run] [--apply] [--json]
Description: Copy the template config into a target path only when explicitly applied.
Examples:
  pm-generate-config.sh --dry-run
  pm-generate-config.sh --config .coferlandia/project-manager/config.json --apply --json
EOF
}

config_path=""
json_output=false
apply_changes=false
dry_run=true

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
    --apply)
      apply_changes=true
      dry_run=false
      ;;
    --dry-run)
      dry_run=true
      apply_changes=false
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
template_path="$(pm_config_template_path)"

if [[ "${apply_changes}" == true ]]; then
  mkdir -p "$(dirname -- "${config_path}")"
  cp "${template_path}" "${config_path}"
  status="applied"
else
  status="dry-run"
fi

if [[ "${json_output}" == true ]]; then
  printf '{\n'
  printf '  "status": "%s",\n' "${status}"
  printf '  "config_path": "%s",\n' "${config_path}"
  printf '  "template_path": "%s"\n' "${template_path}"
  printf '}\n'
else
  printf 'status: %s\n' "${status}"
  printf 'config_path: %s\n' "${config_path}"
  printf 'template_path: %s\n' "${template_path}"
fi
