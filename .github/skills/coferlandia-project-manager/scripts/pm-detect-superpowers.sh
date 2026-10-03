#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-detect-superpowers.sh [--json]
Description: Detect whether the required Superpowers skill roots are present.
EOF
}

json_output=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)
      json_output=true
      ;;
    --help|-h)
      print_help
      exit 0
      ;;
    *)
      printf 'Unknown argument: %s\n' "$1" >&2
      exit 1
      ;;
  esac
  shift
done

roots=()
[[ -d "${HOME}/.codex/skills" ]] && roots+=("${HOME}/.codex/skills")
[[ -d "${HOME}/.agents/skills" ]] && roots+=("${HOME}/.agents/skills")

status="missing"
[[ ${#roots[@]} -gt 0 ]] && status="ok"

if [[ "${json_output}" == true ]]; then
  python_cmd="$(pm_python_cmd)"
  "${python_cmd}" - "${status}" "${roots[@]}" <<'PY'
import json
import sys

status = sys.argv[1]
roots = sys.argv[2:]
print(json.dumps({"status": status, "roots": roots}, indent=2))
PY
else
  printf 'status: %s\n' "${status}"
  if [[ ${#roots[@]} -eq 0 ]]; then
    printf 'roots: none\n'
  else
    printf 'roots:\n'
    for root in "${roots[@]}"; do
      printf -- '- %s\n' "${root}"
    done
  fi
fi
