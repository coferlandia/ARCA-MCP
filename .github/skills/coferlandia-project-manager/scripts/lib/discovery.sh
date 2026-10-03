#!/usr/bin/env bash

discovery_lib_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./config.sh
source "${discovery_lib_dir}/config.sh"
# shellcheck source=./git.sh
source "${discovery_lib_dir}/git.sh"

pm_project_slug_from_path() {
  basename -- "$1"
}

pm_projects_paths() {
  local projects_file="$1"
  [[ -n "${projects_file}" ]] || return 0

  pm_load_project_paths "${projects_file}" | while IFS= read -r project_path; do
    [[ -n "${project_path}" ]] || continue
    pm_normalize_path_for_bash "${project_path}"
  done
}

