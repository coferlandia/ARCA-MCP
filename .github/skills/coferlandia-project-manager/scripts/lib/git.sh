#!/usr/bin/env bash

git_lib_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./logging.sh
source "${git_lib_dir}/logging.sh"

pm_git_is_repo() {
  local path="$1"
  git -C "$path" rev-parse --is-inside-work-tree >/dev/null 2>&1
}

pm_git_current_branch() {
  local path="$1"
  git -C "$path" branch --show-current 2>/dev/null || true
}

pm_git_default_branch_candidate() {
  local path="$1"
  local fallback_branch="${2:-main}"

  git -C "$path" symbolic-ref --quiet refs/remotes/origin/HEAD 2>/dev/null | sed 's#refs/remotes/origin/##' || printf '%s\n' "$fallback_branch"
}

pm_git_has_multiple_worktrees() {
  local path="$1"
  local count
  count="$(git -C "$path" worktree list --porcelain 2>/dev/null | grep -c '^worktree ' || printf '0')"
  [[ "${count}" -gt 1 ]]
}
