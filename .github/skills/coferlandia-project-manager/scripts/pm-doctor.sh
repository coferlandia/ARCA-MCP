#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-doctor.sh --config <path> [--json]
Description: Report environment readiness for the project manager skill.
Examples:
  pm-doctor.sh --config skills/ops/coferlandia-project-manager/examples/config.sample.json
  pm-doctor.sh --json
EOF
}

config_path=""
json_output=false

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

python_cmd="$(pm_python_cmd)"
config_json="$("${script_dir}/pm-validate-config.sh" --config "${config_path}" --json)"
superpowers_json="$("${script_dir}/pm-detect-superpowers.sh" --json)"
skills_json="$("${script_dir}/pm-check-superpowers-skills.sh" --json)"
git_json="$("${script_dir}/pm-detect-git-capabilities.sh" --json)"
effective_vault_root="$(pm_effective_obsidian_vault_root "${config_path}")"

"${python_cmd}" - "${config_json}" "${superpowers_json}" "${skills_json}" "${git_json}" "${json_output}" "${effective_vault_root}" <<'PY'
import json
import shutil
import sys

config = json.loads(sys.argv[1])
superpowers = json.loads(sys.argv[2])
skills = json.loads(sys.argv[3])
git_capabilities = json.loads(sys.argv[4])
json_output = sys.argv[5].lower() == "true"
effective_vault_root = sys.argv[6]

environment = {
    "status": "ok",
    "bash": shutil.which("bash") is not None,
    "git": shutil.which("git") is not None,
    "python": shutil.which("python") is not None or shutil.which("python3") is not None,
}

payload = {
    "config": config,
    "environment": environment,
    "superpowers": superpowers,
    "superpowers_skills": skills,
    "git_capabilities": git_capabilities,
    "effective_vault_root": effective_vault_root,
    "next_approved_action": "review_phase_1_scope",
}

if json_output:
    print(json.dumps(payload, indent=2))
else:
    print("Phase 1 readiness report:")
    print(f"- config status: {config['status']}")
    print(f"- environment status: {environment['status']}")
    print(f"- superpowers status: {superpowers['status']}")
    print(f"- superpowers skills status: {skills['status']}")
    print(f"- git capability status: {git_capabilities['status']}")
    print(f"- effective vault root: {effective_vault_root}")
    print(f"- next approved action: {payload['next_approved_action']}")
PY
