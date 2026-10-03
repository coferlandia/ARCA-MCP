#!/usr/bin/env bash

obsidian_pm_lib_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./logging.sh
source "${obsidian_pm_lib_dir}/logging.sh"

pm_obsidian_project_path() {
  local vault_root="$1" projects_folder="$2" slug="$3"
  printf '%s/%s/%s.md\n' "$vault_root" "$projects_folder" "$slug"
}

pm_obsidian_task_path() {
  local vault_root="$1" tasks_folder="$2" task_id="$3"
  printf '%s/%s/%s.md\n' "$vault_root" "$tasks_folder" "$task_id"
}
