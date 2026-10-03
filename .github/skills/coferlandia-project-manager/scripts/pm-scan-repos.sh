#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/discovery.sh
source "${script_dir}/lib/discovery.sh"

print_help() {
  cat <<'EOF'
Usage: pm-scan-repos.sh --config <path> [--projects-file <path>] [--json] [--include-dirty] [--include-remotes]
Description: Scan the projects listed in projects.json and emit portfolio repository state.
EOF
}

config_path=""
projects_file_arg=""
json_output=false
include_dirty=false
include_remotes=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --config"
      config_path="$1"
      ;;
    --projects-file)
      shift
      [[ $# -gt 0 ]] || die "Missing value for --projects-file"
      projects_file_arg="$1"
      ;;
    --json)
      json_output=true
      ;;
    --include-dirty)
      include_dirty=true
      ;;
    --include-remotes)
      include_remotes=true
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
projects_file="$(pm_resolve_projects_path "${projects_file_arg}" "${config_path}")"
default_branch="$(pm_config_default_branch "${config_path}")"
[[ -f "${projects_file}" ]] || die "projects_file not found: ${projects_file}"
[[ -n "${default_branch}" ]] || default_branch="main"

mapfile -t project_paths < <(pm_projects_paths "${projects_file}")

python_cmd="$(pm_python_cmd)"
scan_payload="$("${python_cmd}" - "${projects_file}" "${default_branch}" "${include_dirty}" "${include_remotes}" "${project_paths[@]}" <<'PY'
import json
import subprocess
import sys
from pathlib import Path

projects_file = sys.argv[1]
default_branch = sys.argv[2]
include_dirty = sys.argv[3].lower() == "true"
include_remotes = sys.argv[4].lower() == "true"
project_paths = sys.argv[5:]


def git(path: str, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", path, *args],
        text=True,
        capture_output=True,
        check=False,
    )
    return result.stdout.strip()


def build_git_status(path: str) -> dict:
    branch = git(path, "branch", "--show-current")
    detached_head = branch == ""
    last_commit_sha = git(path, "rev-parse", "--short=7", "HEAD")
    dirty = False
    untracked = False
    if include_dirty:
        status_lines = git(path, "status", "--porcelain").splitlines()
        dirty = any(not line.startswith("??") for line in status_lines if line)
        untracked = any(line.startswith("??") for line in status_lines)

    worktree_lines = git(path, "worktree", "list", "--porcelain").splitlines()
    worktree_count = sum(1 for line in worktree_lines if line.startswith("worktree "))
    remote_origin_url = git(path, "remote", "get-url", "origin") if include_remotes else ""
    return {
        "has_git": True,
        "current_branch": branch,
        "default_branch_candidate": default_branch,
        "remote_origin_url": remote_origin_url,
        "last_commit_sha": last_commit_sha,
        "dirty": dirty,
        "untracked": untracked,
        "detached_head": detached_head,
        "has_worktrees": worktree_count > 1,
    }


projects = []
for path in project_paths:
    projects.append(
        {
            "project_slug": Path(path).name,
            "repo_path": path,
            "git": build_git_status(path),
        }
    )

print(json.dumps(
    {
        "status": "ok",
        "projects_file": projects_file,
        "projects_detected": len(projects),
        "projects": projects,
    },
    indent=2,
))
PY
)"

if [[ "${json_output}" == true ]]; then
  printf '%s\n' "${scan_payload}"
else
  "${python_cmd}" - "${scan_payload}" <<'PY'
import json
import sys

payload = json.loads(sys.argv[1])
print("status: ok")
print(f"projects_file: {payload['projects_file']}")
print(f"projects_detected: {payload['projects_detected']}")
for project in payload.get("projects", []):
    git = project.get("git", {})
    print(
        f"{project.get('project_slug', '')}: "
        f"branch={git.get('current_branch', '')} "
        f"dirty={git.get('dirty', False)} "
        f"untracked={git.get('untracked', False)} "
        f"worktrees={git.get('has_worktrees', False)} "
        f"remote={git.get('remote_origin_url', '')}"
    )
PY
fi
