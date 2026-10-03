#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-detect-git-capabilities.sh [--json]
Description: Detect worktree support, git identity, and remote tooling availability.
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

git_installed=false
worktree=false
identity_configured=false
gh_cli=false

if command -v git >/dev/null 2>&1; then
  git_installed=true
  git worktree list >/dev/null 2>&1 && worktree=true

  user_name="$(git config --get user.name || true)"
  user_email="$(git config --get user.email || true)"
  if [[ -n "${user_name}" && -n "${user_email}" ]]; then
    identity_configured=true
  fi
fi

command -v gh >/dev/null 2>&1 && gh_cli=true
status="partial"
if [[ "${git_installed}" == true && "${worktree}" == true ]]; then
  status="ok"
fi

if [[ "${json_output}" == true ]]; then
  python_cmd="$(pm_python_cmd)"
  "${python_cmd}" - "${status}" "${git_installed}" "${worktree}" "${identity_configured}" "${gh_cli}" <<'PY'
import json
import sys

status, git_installed, worktree, identity_configured, gh_cli = sys.argv[1:6]
payload = {
    "status": status,
    "git_installed": git_installed == "true",
    "worktree": worktree == "true",
    "identity": {
        "configured": identity_configured == "true",
    },
    "gh_cli": gh_cli == "true",
}
print(json.dumps(payload, indent=2))
PY
else
  printf 'status: %s\n' "${status}"
  printf 'git_installed: %s\n' "${git_installed}"
  printf 'worktree: %s\n' "${worktree}"
  printf 'identity_configured: %s\n' "${identity_configured}"
  printf 'gh_cli: %s\n' "${gh_cli}"
fi
