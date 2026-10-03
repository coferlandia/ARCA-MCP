#!/usr/bin/env python3
"""GitHub-native reporting surface for coferlandia-project-manager.

GitHub Issues/Projects are operational truth. Local TODO/HISTORY parsers remain only as
legacy helpers for pre-cutover diagnostics and compatibility with older callers; reporting
commands never use them as authoritative task state.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

EXPECTED_ARTIFACTS = (
    "README.md",
    "DECISIONS.md",
    "RUNBOOK.md",
    "AGENTS.md",
)
LEGACY_ARTIFACTS = ("TODO.md", "HISTORY.md", ".agent/catalog/OPEN_QUESTIONS.md")

# Kept for backward-compatible imports from existing PM helper modules. These are not the
# GitHub-native workflow contract.
VALID_TASK_STATUSES = (
    "intake", "needs-brainstorming", "spec-writing", "spec-review", "planning",
    "plan-review", "ready-for-agent", "worktree-prep", "implementing", "debugging",
    "code-review", "changes-requested", "verification", "branch-finishing",
    "syncing-docs", "done", "blocked", "cancelled",
)
ACTIVE_STATUSES = set(VALID_TASK_STATUSES) - {"done", "cancelled"}
REVIEW_STATUSES = {"code-review", "spec-review", "plan-review", "changes-requested"}
BRAINSTORM_STATUSES = {"needs-brainstorming"}
PLAN_WAIT_STATUSES = {"planning", "plan-review"}
CODE_REVIEW_STATUSES = {"code-review", "changes-requested"}
_TODO_EXPLICIT_STATUS_RE = re.compile(r"\[\s*(?:status\s*:\s*)?(?P<status>[a-z-]+)\s*\]", re.I)
_TODO_ID_RE = re.compile(r"(?P<id>TASK-[a-zA-Z0-9_-]+|PM-[0-9]+|task[-_]?[a-zA-Z0-9_-]+)", re.I)


def _run(cmd: list[str], *, cwd: Path, check: bool = False) -> subprocess.CompletedProcess[str]:
    try:
        result = subprocess.run(cmd, cwd=cwd, text=True, capture_output=True, check=False)
    except FileNotFoundError as exc:
        result = subprocess.CompletedProcess(cmd, 127, stdout="", stderr=str(exc))
    if check and result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "command failed")
    return result


def _is_git_repo(path: Path) -> bool:
    return _run(["git", "rev-parse", "--is-inside-work-tree"], cwd=path).returncode == 0


def _git(path: Path, *args: str) -> str:
    return _run(["git", *args], cwd=path).stdout.strip()


def _now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _load_projects_file(projects_file: Path) -> list[dict[str, Any]]:
    if not projects_file.is_file():
        return []
    try:
        return json.loads(projects_file.read_text(encoding="utf-8")).get("projects", [])
    except (json.JSONDecodeError, OSError):
        return []


def _resolve_project_path(raw_path: str) -> Path:
    cleaned = raw_path.replace("\\", "/")
    match = re.match(r"^([A-Za-z]):/(.*)$", cleaned)
    if match:
        cleaned = f"/mnt/{match.group(1).lower()}/{match.group(2)}"
    return Path(cleaned).resolve()


def iter_managed_project_entries(projects_file: Path):
    seen: set[str] = set()
    for entry in _load_projects_file(projects_file):
        if entry.get("status", "active") != "active":
            continue
        raw = entry.get("path") or ""
        if not raw:
            continue
        path = _resolve_project_path(raw)
        key = str(path)
        if key in seen:
            continue
        seen.add(key)
        yield entry.get("slug") or path.name, path


def iter_managed_projects(projects_file: Path):
    for _, path in iter_managed_project_entries(projects_file):
        yield path


def project_slug_map(projects_file: Path) -> dict[str, Path]:
    return {slug: path for slug, path in iter_managed_project_entries(projects_file)}


def _entry_for_slug(projects_file: Path, slug: str) -> dict[str, Any] | None:
    for entry in _load_projects_file(projects_file):
        if entry.get("status", "active") == "active" and entry.get("slug") == slug:
            return entry
    return None


def _missing_artifacts(project_path: Path) -> list[str]:
    """Return missing durable root artifacts used by PM health checks.

    Archivist's own validator is authoritative for the internal `.agent/catalog/`
    structure. Keeping PM focused on the durable public surface also lets the Phase 1
    toolset be deployed before every managed repository has completed its cutover.
    """
    return [name for name in EXPECTED_ARTIFACTS if not (project_path / name).is_file()]


def _legacy_artifacts(project_path: Path) -> list[str]:
    return [name for name in LEGACY_ARTIFACTS if (project_path / name).exists()]


def parse_todo_tasks(todo_path: Path) -> list[dict[str, Any]]:
    """Legacy migration-only TODO parser retained for backward compatibility."""
    if not todo_path.is_file():
        return []
    tasks = []
    pattern = re.compile(r"^\s*[-*]\s+\[([ xX])\]\s+(.*)$", re.M)
    for match in pattern.finditer(todo_path.read_text(encoding="utf-8", errors="replace")):
        line = match.group(2).strip()
        explicit = _TODO_EXPLICIT_STATUS_RE.search(line)
        status = explicit.group("status").lower() if explicit and explicit.group("status").lower() in VALID_TASK_STATUSES else ("done" if match.group(1).lower() == "x" else "intake")
        id_match = _TODO_ID_RE.search(line)
        tasks.append({"task_id": id_match.group("id") if id_match else None, "title": line, "status": status, "checkbox_done": match.group(1).lower() == "x"})
    return tasks


def parse_history_entries(history_path: Path, since_days: int = 7) -> list[dict[str, Any]]:
    """Legacy migration-only HISTORY parser retained for backward compatibility."""
    if not history_path.is_file():
        return []
    text = history_path.read_text(encoding="utf-8", errors="replace")
    cutoff = (datetime.now(timezone.utc) - timedelta(days=since_days)).date()
    date_re = re.compile(r"^\s*##\s+(20\d{2}-\d{2}-\d{2})\b", re.M)
    bullet_re = re.compile(r"^\s*[-*]\s+(.+)$", re.M)
    headings = list(date_re.finditer(text))
    if not headings:
        return [{"line": m.group(1).strip()} for m in bullet_re.finditer(text)]
    out = []
    for idx, heading in enumerate(headings):
        date = datetime.fromisoformat(heading.group(1)).date()
        if date < cutoff:
            continue
        end = headings[idx + 1].start() if idx + 1 < len(headings) else len(text)
        for match in bullet_re.finditer(text[heading.end():end]):
            out.append({"line": match.group(1).strip(), "date": heading.group(1)})
    return out


def _derive_project_pm_status(tasks: list[dict[str, Any]]) -> str:
    """Legacy helper retained for older imports; GitHub-native reports use Project state."""
    if any(t.get("status") == "blocked" for t in tasks):
        return "blocked"
    if any(t.get("status") in REVIEW_STATUSES for t in tasks):
        return "review"
    if any(t.get("status") in ACTIVE_STATUSES for t in tasks):
        return "in-progress"
    return "done" if tasks else "unknown"


def _build_git_state(project_path: Path, default_branch: str = "main") -> dict[str, Any]:
    branch = _git(project_path, "branch", "--show-current")
    porcelain = _git(project_path, "status", "--porcelain").splitlines()
    remote_url = _git(project_path, "remote", "get-url", "origin")
    ahead = behind = 0
    if branch:
        tracking = _git(project_path, "rev-parse", "--abbrev-ref", f"{branch}@{{upstream}}")
        if tracking:
            parts = _git(project_path, "rev-list", "--left-right", "--count", f"HEAD...{tracking}").split()
            if len(parts) == 2 and all(p.isdigit() for p in parts):
                ahead, behind = map(int, parts)
    last_date = _git(project_path, "log", "-1", "--format=%cI")
    days = None
    if last_date:
        try:
            days = (datetime.now(timezone.utc) - datetime.fromisoformat(last_date)).days
        except ValueError:
            pass
    return {
        "branch": branch,
        "detached_head": not bool(branch),
        "default_branch": default_branch,
        "last_commit_sha": _git(project_path, "rev-parse", "--short=7", "HEAD"),
        "dirty": any(line and not line.startswith("??") for line in porcelain),
        "untracked": any(line.startswith("??") for line in porcelain),
        "remote_url": remote_url,
        "ahead": ahead,
        "behind": behind,
        "last_commit_date": last_date,
        "days_since_commit": days,
    }


def _repo_name(project_path: Path, entry: dict[str, Any] | None = None) -> str | None:
    if entry and entry.get("repository"):
        return str(entry["repository"])
    result = _run(["gh", "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner"], cwd=project_path)
    return result.stdout.strip() if result.returncode == 0 and result.stdout.strip() else None


def _gh_json(project_path: Path, args: list[str]) -> tuple[Any | None, str | None]:
    if not shutil_which("gh"):
        return None, "gh-not-found"
    result = _run(["gh", *args], cwd=project_path)
    if result.returncode != 0:
        return None, result.stderr.strip() or result.stdout.strip() or "gh-command-failed"
    try:
        return json.loads(result.stdout or "null"), None
    except json.JSONDecodeError:
        return None, "invalid-gh-json"


def shutil_which(name: str) -> str | None:
    import shutil
    return shutil.which(name)


def _issues(project_path: Path, repository: str) -> tuple[list[dict[str, Any]], str | None]:
    fields = "number,title,state,stateReason,url,createdAt,updatedAt,closedAt,labels,assignees,parent,subIssuesSummary,blockedBy,blocking,projectItems"
    payload, error = _gh_json(project_path, ["issue", "list", "--repo", repository, "--state", "all", "--limit", "1000", "--json", fields])
    return (payload or []) if isinstance(payload, list) else [], error


def _project_items(project_path: Path, project_cfg: dict[str, Any] | None) -> tuple[list[dict[str, Any]], str | None]:
    if not project_cfg:
        return [], None
    owner = project_cfg.get("owner")
    number = project_cfg.get("number")
    if not owner or number is None:
        return [], "invalid-github-project-config"
    payload, error = _gh_json(project_path, ["project", "item-list", str(number), "--owner", str(owner), "--limit", "1000", "--format", "json"])
    if isinstance(payload, dict):
        items = payload.get("items")
        return items if isinstance(items, list) else [], error
    return payload if isinstance(payload, list) else [], error


def _label_names(issue: dict[str, Any]) -> list[str]:
    out = []
    for value in issue.get("labels") or []:
        if isinstance(value, dict) and value.get("name"):
            out.append(str(value["name"]))
        elif isinstance(value, str):
            out.append(value)
    return out


def _issue_blocked(issue: dict[str, Any]) -> bool:
    blocked_by = issue.get("blockedBy") or []
    return bool(blocked_by) or any(label.lower() == "blocked" for label in _label_names(issue))


def _project_item_issue_number(item: dict[str, Any]) -> int | None:
    content = item.get("content") if isinstance(item.get("content"), dict) else item
    number = content.get("number") if isinstance(content, dict) else None
    return int(number) if isinstance(number, int) or (isinstance(number, str) and number.isdigit()) else None


def _project_item_status(item: dict[str, Any]) -> str | None:
    for key in ("status", "Status"):
        value = item.get(key)
        if isinstance(value, str):
            return value
        if isinstance(value, dict):
            return value.get("name") or value.get("value")
    field_values = item.get("fieldValues") or item.get("field_values")
    if isinstance(field_values, list):
        for value in field_values:
            name = str(value.get("field", {}).get("name", "")).lower() if isinstance(value, dict) else ""
            if name == "status":
                return value.get("name") or value.get("value")
    return None


def _project_item_fields(item: dict[str, Any]) -> dict[str, Any]:
    """Preserve GitHub Project fields without inventing a schema.

    `gh project item-list --format json` may flatten custom fields at the top level.
    Preserve those values for portfolio/Obsidian consumers while excluding duplicated
    content identity fields.
    """
    ignored = {"content", "id", "title", "repository", "type"}
    return {key: value for key, value in item.items() if key not in ignored}


def _normalize_status(raw: str | None, issue: dict[str, Any]) -> str:
    if issue.get("state") == "CLOSED":
        return "done"
    if _issue_blocked(issue):
        return "blocked"
    value = (raw or "").lower()
    if any(term in value for term in ("review", "qa", "verify", "verification")):
        return "review"
    if any(term in value for term in ("progress", "doing", "implement", "active", "develop")):
        return "in-progress"
    if any(term in value for term in ("done", "complete", "closed")):
        return "done"
    if value:
        return "backlog"
    return "backlog"


def _project_snapshot(projects_file: Path, slug: str, path: Path, default_branch: str) -> dict[str, Any]:
    entry = _entry_for_slug(projects_file, slug) or {}
    git = _build_git_state(path, default_branch)
    repository = _repo_name(path, entry)
    issues: list[dict[str, Any]] = []
    issue_error = None
    if repository:
        issues, issue_error = _issues(path, repository)
    project_cfg = entry.get("github_project") if isinstance(entry.get("github_project"), dict) else None
    project_items, project_error = _project_items(path, project_cfg)
    project_by_issue = {_project_item_issue_number(item): item for item in project_items if _project_item_issue_number(item) is not None}
    project_status = {number: _project_item_status(item) for number, item in project_by_issue.items()}

    operational_mode = "github"
    normalized = []
    for issue in issues:
        number = issue.get("number")
        raw_status = project_status.get(number)
        normalized.append({
            **issue,
            "project_status": raw_status,
            "project_fields": _project_item_fields(project_by_issue[number]) if number in project_by_issue else {},
            "portfolio_status": _normalize_status(raw_status, issue),
            "blocked": _issue_blocked(issue),
            "source": "github",
        })

    # Transitional compatibility is explicit, never silent: if a pre-cutover repository
    # cannot be resolved on GitHub yet, expose its legacy TODO as migration-only pseudo
    # items. This keeps the PM deployable before repositories are migrated one by one,
    # while `operational_mode` and `github_native` make clear that this is not the target
    # source of truth. Repositories with a resolvable GitHub remote never fall back.
    legacy = _legacy_artifacts(path)
    if repository is None and legacy:
        operational_mode = "legacy-migration"
        issue_error = None
        normalized = []
        for index, task in enumerate(parse_todo_tasks(path / "TODO.md"), start=1):
            legacy_status = task.get("status") or "intake"
            normalized.append({
                "number": None,
                "legacy_task_id": task.get("task_id"),
                "title": task.get("title") or f"Legacy task {index}",
                "state": "CLOSED" if task.get("checkbox_done") else "OPEN",
                "url": None,
                "project_status": legacy_status,
                "portfolio_status": (
                    "blocked" if legacy_status == "blocked" else
                    "review" if legacy_status in REVIEW_STATUSES else
                    "in-progress" if legacy_status in ACTIVE_STATUSES and legacy_status != "intake" else
                    "done" if legacy_status in {"done", "cancelled"} else
                    "backlog"
                ),
                "blocked": legacy_status == "blocked",
                "source": "legacy-migration",
            })

    open_issues = [i for i in normalized if i.get("state") == "OPEN"]
    recently_closed = []
    cutoff = datetime.now(timezone.utc) - timedelta(days=7)
    for issue in normalized:
        closed_at = issue.get("closedAt")
        if issue.get("state") != "CLOSED" or not closed_at:
            continue
        try:
            if datetime.fromisoformat(str(closed_at).replace("Z", "+00:00")) >= cutoff:
                recently_closed.append(issue)
        except ValueError:
            pass

    missing = _missing_artifacts(path)
    return {
        "project_slug": slug,
        "repo_path": path.as_posix(),
        "repository": repository,
        "github_project": project_cfg,
        "github_project_items": project_items,
        "github_errors": [e for e in (issue_error, project_error) if e],
        "operational_mode": operational_mode,
        "git": git,
        "archivist_initialized": not missing,
        "missing_artifacts": missing,
        "legacy_operational_artifacts": legacy,
        "github_native": bool(repository) and not legacy,
        "issues": normalized,
        "open_issues": open_issues,
        "recently_closed": recently_closed,
    }


def cmd_portfolio_report(args: argparse.Namespace) -> dict[str, Any]:
    projects = []
    conflicts = []
    for slug, path in iter_managed_project_entries(args.projects_file):
        if not _is_git_repo(path):
            conflicts.append({"project_slug": slug, "type": "repo_path_missing"})
            continue
        snapshot = _project_snapshot(args.projects_file, slug, path, args.default_branch)
        projects.append(snapshot)
        if snapshot["missing_artifacts"]:
            conflicts.append({"project_slug": slug, "type": "missing_archivist_artifact", "missing_artifacts": snapshot["missing_artifacts"]})
        if snapshot["github_errors"]:
            conflicts.append({"project_slug": slug, "type": "github_read_error", "details": snapshot["github_errors"]})
        if snapshot.get("repository") and not snapshot.get("github_project"):
            conflicts.append({"project_slug": slug, "type": "github_project_unconfigured"})

    open_issues = [i for p in projects for i in p["open_issues"]]
    blocked = [i for i in open_issues if i["portfolio_status"] == "blocked"]
    review = [i for i in open_issues if i["portfolio_status"] == "review"]
    in_progress = [i for i in open_issues if i["portfolio_status"] == "in-progress"]
    recent = [i for p in projects for i in p["recently_closed"]]
    return {
        "status": "ok",
        "generated_at": _now_iso(),
        "projects_file": args.projects_file.as_posix(),
        "projects_count": len(projects),
        "summary": {
            "active_projects": len(projects),
            "open_issues": len(open_issues),
            "issues_in_progress": len(in_progress),
            "issues_in_review": len(review),
            "blocked_issues": len(blocked),
            "issues_closed_this_week": len(recent),
            "repos_with_uncommitted_changes": sum(1 for p in projects if p["git"]["dirty"] or p["git"]["untracked"]),
            "repos_ahead_or_behind_remote": sum(1 for p in projects if p["git"]["ahead"] or p["git"]["behind"]),
            "projects_lacking_archivist_artifacts": sum(1 for p in projects if p["missing_artifacts"]),
            "projects_pending_github_native_migration": sum(1 for p in projects if not p["github_native"]),
            "projects_with_github_read_errors": sum(1 for p in projects if p["github_errors"]),
            "projects_without_github_project": sum(1 for p in projects if p.get("repository") and not p.get("github_project")),
            # Compatibility aliases for older PM consumers. They are derived from the
            # GitHub-native categories and must not be interpreted as the old state machine.
            "blocked_projects": len({p["project_slug"] for p in projects if any(i["portfolio_status"] == "blocked" for i in p["open_issues"])}),
            "ready_for_agent_tasks": 0,
            "projects_in_review": len({p["project_slug"] for p in projects if any(i["portfolio_status"] == "review" for i in p["open_issues"])}),
            "tasks_completed_this_week": len(recent),
            "projects_with_sync_conflicts": len({c.get("project_slug") for c in conflicts}),
            "projects_without_recent_activity": sum(1 for p in projects if p["git"].get("days_since_commit") is not None and p["git"]["days_since_commit"] > args.stale_days),
            "tasks_needing_brainstorming": 0,
            "tasks_waiting_for_plan_approval": 0,
            "tasks_waiting_for_code_review": len(review),
            "projects_needing_maintenance": len({p["project_slug"] for p in projects if p["missing_artifacts"] or p["legacy_operational_artifacts"] or p["github_errors"] or p["git"]["dirty"] or p["git"]["untracked"] or p["git"]["ahead"] or p["git"]["behind"]}),
        },
        "projects": projects,
        "tasks": [i for p in projects for i in p["open_issues"]],
        "conflicts": conflicts,
    }


def cmd_project_report(args: argparse.Namespace) -> dict[str, Any]:
    path = project_slug_map(args.projects_file).get(args.project)
    if path is None or not _is_git_repo(path):
        return {"status": "error", "error": f"Project '{args.project}' not found or not a Git repository in projects.json."}
    snapshot = _project_snapshot(args.projects_file, args.project, path, args.default_branch)
    counts = {key: sum(1 for i in snapshot["open_issues"] if i["portfolio_status"] == key) for key in ("backlog", "in-progress", "review", "blocked")}
    return {"status": "ok", "generated_at": _now_iso(), **snapshot, "issue_counts": {**counts, "open": len(snapshot["open_issues"]), "recently_closed": len(snapshot["recently_closed"])}}


def _parse_issue_reference(value: str) -> tuple[str | None, str | None, int | None]:
    raw = value.strip()
    match = re.match(r"^(?P<slug>[A-Za-z0-9._-]+)#(?P<number>\d+)$", raw)
    if match:
        return match.group("slug"), None, int(match.group("number"))
    url_match = re.search(r"github\.com/(?P<owner>[^/]+)/(?P<repo>[^/]+)/issues/(?P<number>\d+)(?:$|[?#])", raw)
    if url_match:
        return None, f"{url_match.group('owner')}/{url_match.group('repo')}", int(url_match.group("number"))
    cleaned = raw.lstrip("#")
    return (None, None, int(cleaned)) if cleaned.isdigit() else (None, None, None)


def cmd_task_report(args: argparse.Namespace) -> dict[str, Any]:
    requested_slug, requested_repository, number = _parse_issue_reference(args.task)
    if number is None:
        return {"status": "error", "error": "GitHub-native task report requires an Issue reference such as project-slug#142, #142, or an Issue URL."}
    matches = []
    for slug, path in iter_managed_project_entries(args.projects_file):
        if requested_slug and slug != requested_slug:
            continue
        if not _is_git_repo(path):
            continue
        entry = _entry_for_slug(args.projects_file, slug) or {}
        repository = _repo_name(path, entry)
        if not repository:
            continue
        if requested_repository and repository.lower() != requested_repository.lower():
            continue
        payload, error = _gh_json(path, ["issue", "view", str(number), "--repo", repository, "--json", "number,title,state,stateReason,url,createdAt,updatedAt,closedAt,labels,assignees,parent,subIssues,blockedBy,blocking,projectItems,body"])
        if not error and isinstance(payload, dict):
            matches.append({"project": slug, "repository": repository, "issue": payload})
    if len(matches) == 1:
        return {"status": "ok", "generated_at": _now_iso(), **matches[0]}
    if len(matches) > 1 and not requested_slug:
        choices = [f"{m['project']}#{number}" for m in matches]
        return {"status": "error", "error": f"Issue #{number} is ambiguous across managed repositories. Use project-slug#{number}.", "matches": choices}
    target = args.task if requested_repository else (f"{requested_slug}#{number}" if requested_slug else f"#{number}")
    return {"status": "error", "error": f"Issue {target} was not found in the selected accessible managed repository set."}

def cmd_health_check(args: argparse.Namespace) -> dict[str, Any]:
    report = cmd_portfolio_report(args)
    issues = list(report.get("conflicts", []))
    for project in report.get("projects", []):
        git = project["git"]
        if git["dirty"]:
            issues.append({"project_slug": project["project_slug"], "type": "dirty_repo"})
        if git["untracked"]:
            issues.append({"project_slug": project["project_slug"], "type": "untracked_files"})
        if git["ahead"]:
            issues.append({"project_slug": project["project_slug"], "type": "ahead_of_remote", "ahead": git["ahead"]})
        if git["behind"]:
            issues.append({"project_slug": project["project_slug"], "type": "behind_remote", "behind": git["behind"]})
        if git.get("days_since_commit") is not None and git["days_since_commit"] > args.stale_days:
            issues.append({"project_slug": project["project_slug"], "type": "stale", "days_since_commit": git["days_since_commit"]})
        if project["legacy_operational_artifacts"]:
            issues.append({"project_slug": project["project_slug"], "type": "github_native_migration_required", "details": project["legacy_operational_artifacts"]})
    return {"status": "ok", "generated_at": _now_iso(), "projects_count": report["projects_count"], "summary": {"total_projects": report["projects_count"], "healthy_projects": max(0, report["projects_count"] - len({i.get('project_slug') for i in issues})), "projects_with_issues": len({i.get('project_slug') for i in issues}), "total_issues": len(issues), "maintenance_due": bool(issues)}, "issues": issues}


def _list_worktrees(project_path: Path) -> list[dict[str, Any]]:
    output = _git(project_path, "worktree", "list", "--porcelain")
    worktrees, current = [], {}
    for line in output.splitlines() + [""]:
        if line.startswith("worktree "):
            if current:
                worktrees.append(current)
            current = {"path": line[9:].strip()}
        elif line.startswith("branch "):
            current["branch"] = line[7:].strip().replace("refs/heads/", "")
        elif line.startswith("HEAD "):
            current["head"] = line[5:].strip()
        elif line.startswith("detached"):
            current["detached"] = True
    if current and current not in worktrees:
        worktrees.append(current)
    return worktrees


def cmd_worktree_cleanup(args: argparse.Namespace) -> dict[str, Any]:
    if args.apply:
        raise SystemExit("worktree-cleanup is advisory only")
    rows = []
    for slug, project_path in iter_managed_project_entries(args.projects_file):
        if not _is_git_repo(project_path):
            continue
        for wt in _list_worktrees(project_path):
            path = Path(wt.get("path", "")).resolve()
            is_main = path == project_path.resolve()
            dirty = False
            if path.is_dir() and not is_main:
                dirty = bool(_run(["git", "status", "--porcelain"], cwd=path).stdout.strip())
            rows.append({"project_slug": slug, **wt, "is_main": is_main, "dirty": dirty, "classification": "main" if is_main else ("dirty-linked" if dirty else "clean-linked")})
    actionable = [r for r in rows if not r["is_main"]]
    return {"status": "ok", "generated_at": _now_iso(), "summary": {"total_worktrees": len(rows), "main_worktrees": sum(1 for r in rows if r["is_main"]), "linked_worktrees": len(actionable), "clean_suggesting_removal": sum(1 for r in actionable if not r["dirty"]), "dirty_caution": sum(1 for r in actionable if r["dirty"])}, "worktrees": rows, "clean_removable": [r for r in actionable if not r["dirty"]], "dirty_caution": [r for r in actionable if r["dirty"]], "removed_worktrees": []}


def _render_portfolio_markdown(payload: dict[str, Any]) -> str:
    s = payload["summary"]
    lines = ["# Portfolio Report", "", f"- Active projects: {s['active_projects']}", f"- Open Issues: {s['open_issues']}", f"- In progress: {s['issues_in_progress']}", f"- In review: {s['issues_in_review']}", f"- Blocked: {s['blocked_issues']}", f"- Closed this week: {s['issues_closed_this_week']}", f"- Pending GitHub-native migration: {s['projects_pending_github_native_migration']}", ""]
    for project in payload.get("projects", []):
        lines += [f"## {project['project_slug']}", f"- Repository: {project.get('repository') or 'unresolved'}", f"- GitHub-native: {project['github_native']}", f"- Open Issues: {len(project['open_issues'])}", f"- Recently closed: {len(project['recently_closed'])}", f"- Archivist: {'healthy' if project['archivist_initialized'] else 'missing ' + ', '.join(project['missing_artifacts'])}", ""]
    return "\n".join(lines)


def _render_project_markdown(payload: dict[str, Any]) -> str:
    if payload.get("status") == "error":
        return f"# Project Report\n\nError: {payload.get('error')}\n"
    c = payload["issue_counts"]
    lines = ["# Project Report", "", f"- Project: {payload['project_slug']}", f"- Repository: {payload.get('repository') or 'unresolved'}", f"- GitHub-native: {payload['github_native']}", f"- Open Issues: {c['open']}", f"- In progress: {c['in-progress']}", f"- Review: {c['review']}", f"- Blocked: {c['blocked']}", f"- Recently closed: {c['recently_closed']}", "", "## Open Issues"]
    for issue in payload["open_issues"]:
        lines.append(f"- [{issue['portfolio_status']}] #{issue['number']} {issue['title']}")
    return "\n".join(lines) + "\n"


def _render_task_markdown(payload: dict[str, Any]) -> str:
    if payload.get("status") == "error":
        return f"# Issue Report\n\nError: {payload.get('error')}\n"
    i = payload["issue"]
    return "\n".join(["# Issue Report", "", f"- Project: {payload['project']}", f"- Repository: {payload['repository']}", f"- Issue: #{i['number']} {i['title']}", f"- State: {i['state']}", f"- URL: {i['url']}", ""])


def _render_health_check_markdown(payload: dict[str, Any]) -> str:
    s = payload["summary"]
    lines = ["# Health Check Report", "", f"- Total projects: {s['total_projects']}", f"- Healthy projects: {s['healthy_projects']}", f"- Projects with issues: {s['projects_with_issues']}", f"- Health findings: {s['total_issues']}", ""]
    for issue in payload.get("issues", []):
        lines.append(f"- {issue.get('project_slug')}: {issue.get('type')}")
    return "\n".join(lines) + "\n"


def _render_worktree_cleanup_markdown(payload: dict[str, Any]) -> str:
    s = payload["summary"]
    return "\n".join(["# Worktree Cleanup Report", "", f"- Total worktrees: {s['total_worktrees']}", f"- Linked worktrees: {s['linked_worktrees']}", f"- Clean: {s['clean_suggesting_removal']}", f"- Dirty: {s['dirty_caution']}", ""])


_COMMANDS = {"portfolio-report": cmd_portfolio_report, "project-report": cmd_project_report, "task-report": cmd_task_report, "health-check": cmd_health_check, "worktree-cleanup": cmd_worktree_cleanup}
_RENDERERS = {"portfolio-report": _render_portfolio_markdown, "project-report": _render_project_markdown, "task-report": _render_task_markdown, "health-check": _render_health_check_markdown, "worktree-cleanup": _render_worktree_cleanup_markdown}


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="GitHub-native reporting surface for coferlandia-project-manager.")
    sub = parser.add_subparsers(dest="command", required=True)
    def common(p: argparse.ArgumentParser) -> None:
        p.add_argument("--projects-file", required=True, type=Path)
        p.add_argument("--format", choices=("json", "markdown"), default="markdown")
        p.add_argument("--default-branch", default="main")
        p.add_argument("--stale-days", type=int, default=30)
        p.add_argument("--apply", action="store_true")
    for name in ("portfolio-report", "health-check", "worktree-cleanup"):
        p = sub.add_parser(name)
        common(p)
        if name == "worktree-cleanup":
            p.add_argument("--mode", choices=("dry-run", "suggest"), default="dry-run")
    p = sub.add_parser("project-report")
    common(p)
    p.add_argument("--project", required=True)
    p = sub.add_parser("task-report")
    common(p)
    p.add_argument("--task", required=True, help="Issue reference: project-slug#142 (preferred), #142 when unique, or an Issue URL.")
    return parser


def main(argv=None) -> int:
    args = _build_parser().parse_args(argv)
    try:
        payload = _COMMANDS[args.command](args)
    except RuntimeError as exc:
        payload = {"status": "error", "error": str(exc)}
    if args.format == "json":
        print(json.dumps(payload, indent=2, ensure_ascii=False))
    else:
        print(_RENDERERS[args.command](payload))
    return 0 if payload.get("status") != "error" else 1


if __name__ == "__main__":
    sys.exit(main())
