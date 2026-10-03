#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-validate-config.sh --config <path> [--json]
Description: Validate that the config file is readable and structurally complete.
Examples:
  pm-validate-config.sh --config skills/ops/coferlandia-project-manager/examples/config.sample.json
  pm-validate-config.sh --json
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
if "${python_cmd}" - "${config_path}" "$(pm_config_template_path)" "${json_output}" <<'PY'
import json
import sys
from pathlib import Path

config_path = Path(sys.argv[1])
template_path = Path(sys.argv[2])
json_output = sys.argv[3].lower() == "true"

def collect_missing(template, candidate, prefix=""):
    missing = []
    if isinstance(template, dict):
        if not isinstance(candidate, dict):
            missing.append(prefix.rstrip(".") or "<root>")
            return missing
        for key, value in template.items():
            path = f"{prefix}{key}"
            if key not in candidate:
                missing.append(path)
                continue
            missing.extend(collect_missing(value, candidate[key], f"{path}."))
    elif isinstance(template, list):
        if not isinstance(candidate, list):
            missing.append(prefix.rstrip(".") or "<root>")
    return missing

template = json.loads(template_path.read_text(encoding="utf-8"))
candidate = json.loads(config_path.read_text(encoding="utf-8"))
missing = collect_missing(template, candidate)

if missing:
    payload = {
        "status": "error",
        "config_path": str(config_path),
        "missing_keys": missing,
    }
    if json_output:
        print(json.dumps(payload, indent=2))
    else:
        print(f"status: {payload['status']}")
        print(f"config_path: {payload['config_path']}")
        print("missing_keys:")
        for item in missing:
            print(f"- {item}")
    raise SystemExit(1)

payload = {
    "status": "ok",
    "config_path": str(config_path),
    "missing_keys": [],
}
if json_output:
    print(json.dumps(payload, indent=2))
else:
    print(f"status: {payload['status']}")
    print(f"config_path: {payload['config_path']}")
    print("missing_keys: none")
PY
then
  exit 0
else
  exit $?
fi
