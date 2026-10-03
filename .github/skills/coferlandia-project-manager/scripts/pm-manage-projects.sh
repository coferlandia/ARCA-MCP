#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"
# shellcheck source=./lib/git.sh
source "${script_dir}/lib/git.sh"

print_help() {
  cat <<'HELP'
Usage: pm-manage-projects.sh <command> [options]

Commands:
  add <path> [--slug <slug>] [--repository <owner/repo>]
      [--github-project-owner <owner> --github-project-number <number>]
  configure-github <slug> [--repository <owner/repo>]
      [--github-project-owner <owner> --github-project-number <number>]
  remove <slug|path>
  list [--json]

projects.json owns portfolio membership/integration coordinates only. GitHub Issues/Projects
own operational work state.
HELP
}

config_path=""
projects_file_arg=""
json_output=false
slug_arg=""
repository_arg=""
project_owner_arg=""
project_number_arg=""
subcommand=""
target=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    add|remove|list|configure-github) subcommand="$1"; shift; break ;;
    --config) shift; config_path="${1:-}" ;;
    --projects-file) shift; projects_file_arg="${1:-}" ;;
    --json) json_output=true ;;
    --slug) shift; slug_arg="${1:-}" ;;
    --repository) shift; repository_arg="${1:-}" ;;
    --github-project-owner) shift; project_owner_arg="${1:-}" ;;
    --github-project-number) shift; project_number_arg="${1:-}" ;;
    -h|--help) print_help; exit 0 ;;
    *) die "Unknown argument: $1" ;;
  esac
  shift
done
[[ -n "${subcommand}" ]] || { print_help; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --config) shift; config_path="${1:-}" ;;
    --projects-file) shift; projects_file_arg="${1:-}" ;;
    --json) json_output=true ;;
    --slug) shift; slug_arg="${1:-}" ;;
    --repository) shift; repository_arg="${1:-}" ;;
    --github-project-owner) shift; project_owner_arg="${1:-}" ;;
    --github-project-number) shift; project_number_arg="${1:-}" ;;
    -h|--help) print_help; exit 0 ;;
    -*) die "Unknown argument: $1" ;;
    *) [[ -z "${target}" ]] || die "Unexpected extra argument: $1"; target="$1" ;;
  esac
  shift
done

if [[ "${subcommand}" != "list" && -z "${target}" ]]; then
  die "${subcommand} requires a target"
fi
if [[ -n "${project_number_arg}" && ! "${project_number_arg}" =~ ^[0-9]+$ ]]; then
  die "--github-project-number must be an integer"
fi
if [[ -n "${project_number_arg}" && -z "${project_owner_arg}" ]] || [[ -n "${project_owner_arg}" && -z "${project_number_arg}" ]]; then
  die "--github-project-owner and --github-project-number must be supplied together"
fi

config_path="$(pm_resolve_config_path "${config_path}")"
projects_file="$(pm_resolve_projects_path "${projects_file_arg}" "${config_path}")"
python_cmd="$(pm_python_cmd)"

ensure_projects_file() {
  if [[ ! -f "${projects_file}" ]]; then
    mkdir -p "$(dirname -- "${projects_file}")"
    cp "$(pm_projects_template_path)" "${projects_file}"
  fi
}

resolve_repository() {
  local path="$1"
  local explicit="$2"
  if [[ -n "${explicit}" ]]; then
    printf '%s\n' "${explicit}"
    return 0
  fi
  if command -v gh >/dev/null 2>&1; then
    (cd -- "${path}" && gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>/dev/null) || true
  fi
}

case "${subcommand}" in
  add)
    normalized_target="$(pm_normalize_path_for_bash "${target}")"
    target_path="$(cd -- "${normalized_target}" 2>/dev/null && pwd)" || die "Path does not exist: ${target}"
    pm_git_is_repo "${target_path}" || die "Not a git repository: ${target_path}"
    slug="${slug_arg:-$(basename -- "${target_path}")}"
    repository="$(resolve_repository "${target_path}" "${repository_arg}")"
    ensure_projects_file
    "${python_cmd}" - "${projects_file}" "${target_path}" "${slug}" "${repository}" "${project_owner_arg}" "${project_number_arg}" <<'PY'
import json, sys
from datetime import datetime, timezone
from pathlib import Path

path, target, slug, repository, owner, number = sys.argv[1:]
projects_file = Path(path)
payload = json.loads(projects_file.read_text(encoding="utf-8"))
projects = payload.setdefault("projects", [])
resolved = str(Path(target).resolve())
now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
for entry in projects:
    if entry.get("status", "active") == "active" and (entry.get("slug") == slug or (entry.get("path") and str(Path(entry["path"]).resolve()) == resolved)):
        raise SystemExit(f"Active project already registered: {slug} / {resolved}")
entry = next((e for e in projects if e.get("slug") == slug or (e.get("path") and str(Path(e["path"]).resolve()) == resolved)), None)
if entry is None:
    entry = {"slug": slug, "path": resolved, "added_at": now, "status": "active"}
    projects.append(entry)
else:
    entry.update({"slug": slug, "path": resolved, "added_at": now, "status": "active"})
if repository:
    entry["repository"] = repository
if owner and number:
    entry["github_project"] = {"owner": owner, "number": int(number)}
payload.setdefault("version", 1)
projects_file.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"status":"ok","action":"add","project":entry,"projects_file":str(projects_file)}, indent=2))
PY
    ;;
  configure-github)
    ensure_projects_file
    "${python_cmd}" - "${projects_file}" "${target}" "${repository_arg}" "${project_owner_arg}" "${project_number_arg}" <<'PY'
import json, sys
from pathlib import Path
path, slug, repository, owner, number = sys.argv[1:]
projects_file = Path(path)
payload = json.loads(projects_file.read_text(encoding="utf-8"))
entry = next((e for e in payload.get("projects", []) if e.get("slug") == slug and e.get("status", "active") == "active"), None)
if entry is None:
    raise SystemExit(f"No active project matched: {slug}")
if repository:
    entry["repository"] = repository
if owner and number:
    entry["github_project"] = {"owner": owner, "number": int(number)}
elif owner or number:
    raise SystemExit("GitHub Project owner and number must be supplied together")
projects_file.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"status":"ok","action":"configure-github","project":entry}, indent=2))
PY
    ;;
  remove)
    ensure_projects_file
    "${python_cmd}" - "${projects_file}" "${target}" <<'PY'
import json, sys
from pathlib import Path
projects_file, target = Path(sys.argv[1]), sys.argv[2]
payload = json.loads(projects_file.read_text(encoding="utf-8"))
matched = None
for entry in payload.get("projects", []):
    if entry.get("status", "active") != "active":
        continue
    if entry.get("slug") == target:
        matched = entry; break
    try:
        if entry.get("path") and Path(entry["path"]).resolve() == Path(target).resolve():
            matched = entry; break
    except OSError:
        pass
if matched is None:
    raise SystemExit(f"No active project matched: {target}")
matched["status"] = "archived"
projects_file.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"status":"ok","action":"remove","project":matched}, indent=2))
PY
    ;;
  list)
    ensure_projects_file
    "${python_cmd}" - "${projects_file}" "${json_output}" <<'PY'
import json, sys
from pathlib import Path
path, json_output = Path(sys.argv[1]), sys.argv[2].lower() == "true"
payload = json.loads(path.read_text(encoding="utf-8"))
active = [e for e in payload.get("projects", []) if e.get("status", "active") == "active"]
if json_output:
    print(json.dumps({"status":"ok","projects_file":str(path),"projects_detected":len(active),"projects":active}, indent=2))
else:
    print("status: ok")
    print(f"projects_file: {path}")
    print(f"projects_detected: {len(active)}")
    for entry in active:
        print(f"{entry.get('slug','')}\t{entry.get('path','')}\t{entry.get('repository','')}")
PY
    ;;
esac
