#!/usr/bin/env bash

config_lib_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./logging.sh
source "${config_lib_dir}/logging.sh"

pm_skill_root() {
  cd -- "${config_lib_dir}/../.." >/dev/null 2>&1 && pwd
}

pm_repo_root() {
  git rev-parse --show-toplevel 2>/dev/null || pwd
}

pm_config_template_path() {
  printf '%s\n' "$(pm_skill_root)/templates/config.template.json"
}

pm_config_default_path() {
  pm_config_template_path
}

pm_config_default_target() {
  printf '%s\n' "$(pm_repo_root)/.coferlandia/project-manager/config.json"
}

pm_projects_template_path() {
  printf '%s\n' "$(pm_skill_root)/templates/projects.template.json"
}

pm_projects_default_target() {
  printf '%s\n' "$(pm_repo_root)/.coferlandia/project-manager/projects.json"
}

# Resolve the projects.json path. Precedence:
#   1. explicit --projects-file argument
#   2. "projects_file" key in the config
#   3. sibling of config_path (same directory) when config_path is explicit
#   4. repo-local default under $(pm_repo_root)
pm_resolve_projects_path() {
  local projects_file="${1:-}"
  local config_path="${2:-}"

  if [[ -n "${projects_file}" ]]; then
    printf '%s\n' "${projects_file}"
    return 0
  fi

  if [[ -n "${config_path}" && -f "${config_path}" ]]; then
    local configured
    configured="$(pm_config_json_value "${config_path}" "projects_file" 2>/dev/null || true)"
    if [[ -n "${configured}" ]]; then
      printf '%s\n' "${configured}"
      return 0
    fi
    # Default to a sibling of the config file so --config alone locates state.
    printf '%s\n' "$(dirname -- "${config_path}")/projects.json"
    return 0
  fi

  pm_projects_default_target
}

pm_default_obsidian_vault_root() {
  printf '%s\n' "$(pm_repo_root)/obsidian"
}

pm_effective_obsidian_vault_root() {
  local config_path="$1"
  local vault_root

  vault_root="$(pm_config_json_value "${config_path}" "obsidian.vault_root")"
  if [[ -n "${vault_root}" ]]; then
    pm_normalize_path_for_bash "${vault_root}"
  else
    pm_default_obsidian_vault_root
  fi
}

pm_resolve_config_path() {
  local config_path="${1:-}"

  if [[ -n "${config_path}" ]]; then
    printf '%s\n' "${config_path}"
  else
    pm_config_default_target
  fi
}

pm_normalize_path_for_bash() {
  local path="$1"
  local normalized="$path"

  if [[ "$normalized" =~ ^([A-Za-z]):[\\/](.*)$ ]]; then
    local drive_letter
    local remainder
    drive_letter="${BASH_REMATCH[1],,}"
    remainder="${BASH_REMATCH[2]//\\//}"
    normalized="/mnt/${drive_letter}/${remainder}"
  fi

  printf '%s\n' "$normalized"
}

pm_config_json_value() {
  local config_path="$1"
  local key_path="$2"
  local python_cmd
  python_cmd="$(pm_python_cmd)"

"${python_cmd}" - "${config_path}" "${key_path}" <<'PY'
import json
import sys
from pathlib import Path

config_path = Path(sys.argv[1])
key_path = sys.argv[2].split(".")
try:
    payload = json.loads(config_path.read_text(encoding="utf-8"))
except json.JSONDecodeError as exc:
    raise SystemExit(f"Invalid JSON in {config_path}: {exc}") from exc

value = payload
try:
    for key in key_path:
        value = value[key]
except (KeyError, TypeError) as exc:
    raise SystemExit(f"Missing config key: {'.'.join(key_path)}") from exc

if value is None:
    print("")
elif isinstance(value, (dict, list)):
    print(json.dumps(value))
else:
    print(value)
PY
}

pm_load_project_paths() {
  local projects_file="$1"
  [[ -f "${projects_file}" ]] || return 0
  local python_cmd
  python_cmd="$(pm_python_cmd)"

"${python_cmd}" - "${projects_file}" <<'PY'
import json
import sys
from pathlib import Path

projects_file = Path(sys.argv[1])
try:
    payload = json.loads(projects_file.read_text(encoding="utf-8"))
except json.JSONDecodeError as exc:
    raise SystemExit(f"Invalid JSON in {projects_file}: {exc}") from exc

for entry in payload.get("projects", []):
    if entry.get("status", "active") != "active":
        continue
    path = entry.get("path", "")
    if path:
        print(path)
PY
}

pm_config_default_branch() {
  local config_path="$1"
  pm_config_json_value "$config_path" "git.default_branch"
}

pm_require_file() {
  local path="$1"
  [[ -f "$path" ]] || die "Expected file not found: $path"
}

pm_python_cmd() {
  local candidate

  for candidate in python python3; do
    command -v "${candidate}" >/dev/null 2>&1 || continue
    # Reject the Microsoft Store "python" stub on Windows, which prints a
    # localized "Python not found" message and exits 0 without running Python.
    # A real interpreter prints a "Python X.Y.Z" version line to stdout.
    if "${candidate}" --version 2>/dev/null | grep -qi "^Python [0-9]"; then
      printf '%s\n' "${candidate}"
      return 0
    fi
  done

  die "Python interpreter not found. Expected 'python' or 'python3' (a real interpreter, not the Windows Store stub) in PATH."
}
