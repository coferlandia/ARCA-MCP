#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-onboard.sh --config <path> [--json] [--dry-run] [--apply]
Description: Build or validate the project manager onboarding state.
Examples:
  pm-onboard.sh --dry-run
  pm-onboard.sh --json
EOF
}

config_path=""
json_output=false
mode="dry-run"

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
      mode="dry-run"
      ;;
    --apply)
      mode="apply"
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
config_exists=false
config_preexisting=false
config_generated=false
readiness_json=""
projects_status="missing"
projects_count=0
[[ -f "${config_path}" ]] && config_exists=true
config_preexisting="${config_exists}"

if [[ "${config_exists}" == false && "${mode}" == "apply" ]]; then
  "${script_dir}/pm-generate-config.sh" --config "${config_path}" --apply --json >/dev/null
  config_exists=true
  config_generated=true
fi

# Ensure projects.json exists alongside the config when applying.
projects_file="$(pm_resolve_projects_path "" "${config_path}")"
if [[ "${mode}" == "apply" && ! -f "${projects_file}" ]]; then
  mkdir -p "$(dirname -- "${projects_file}")"
  cp "$(pm_projects_template_path)" "${projects_file}"
fi
if [[ -f "${projects_file}" ]]; then
  projects_status="populated"
  projects_count=$(pm_load_project_paths "${projects_file}" | wc -l)
  [[ "${projects_count}" -eq 0 ]] && projects_status="empty"
fi

if [[ "${config_exists}" == true ]]; then
  readiness_json="$("${script_dir}/pm-doctor.sh" --config "${config_path}" --json)"
fi

if [[ "${json_output}" == true ]]; then
  python_cmd="$(pm_python_cmd)"
  "${python_cmd}" - "${mode}" "${config_path}" "${config_exists}" "${config_preexisting}" "${config_generated}" "${readiness_json}" "${projects_file}" "${projects_status}" "${projects_count}" <<'PY'
import json
import sys

mode = sys.argv[1]
config_path = sys.argv[2]
config_exists = sys.argv[3].lower() == "true"
config_preexisting = sys.argv[4].lower() == "true"
config_generated = sys.argv[5].lower() == "true"
readiness = json.loads(sys.argv[6]) if sys.argv[6] else None
projects_file = sys.argv[7]
projects_status = sys.argv[8]
projects_count = int(sys.argv[9])
payload = {
    "status": "ok",
    "mode": mode,
    "config_path": config_path,
    "config_exists": config_exists,
    "config_preexisting": config_preexisting,
    "projects_file": projects_file,
    "projects_status": projects_status,
    "projects_count": projects_count,
}
if readiness is not None:
    payload["readiness"] = readiness
if config_generated:
    payload["config_generation"] = {"status": "applied"}
if not config_exists and mode != "apply":
    payload["readiness"] = {"status": "missing"}
    payload["next_approved_action"] = "generate_config"
elif projects_status == "empty":
    payload["next_approved_action"] = "add_first_project"
print(json.dumps(payload, indent=2))
PY
else
  printf 'status: ok\n'
  printf 'mode: %s\n' "${mode}"
  printf 'config_path: %s\n' "${config_path}"
  printf 'config_preexisting: %s\n' "${config_preexisting}"
  printf 'projects_file: %s\n' "${projects_file}"
  printf 'projects_status: %s (%s)\n' "${projects_status}" "${projects_count}"
  if [[ "${config_exists}" == true ]]; then
    if [[ "${config_generated}" == true ]]; then
      printf 'config_generation_status: applied\n'
    fi
    "${script_dir}/pm-doctor.sh" --config "${config_path}"
    if [[ "${projects_status}" == "empty" ]]; then
      printf 'next_approved_action: add_first_project\n'
    fi
  else
    printf 'config_status: missing\n'
    printf 'next_approved_action: generate_config\n'
  fi
fi
