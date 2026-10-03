#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-sync-to-obsidian.sh --config <path> [--json] [--dry-run] [--apply]
Description: Create or update Obsidian PM project and task notes from the PM registry.
EOF
}

config_path=""
json_output=false
apply=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --help|-h)
      print_help
      exit 0
      ;;
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

python_cmd="$(pm_python_cmd)"
args=(
  "${script_dir}/lib/archivist.py"
  sync-to-obsidian
  --projects-file "${projects_file}"
  --config "${config_path}"
)
if [[ "${apply}" == true ]]; then
  args+=(--apply)
fi
if [[ "${json_output}" == true ]]; then
  args+=(--format json)
else
  args+=(--format text)
fi

exec "${python_cmd}" "${args[@]}"
