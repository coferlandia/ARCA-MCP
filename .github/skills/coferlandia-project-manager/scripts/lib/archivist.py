#!/usr/bin/env python3
"""Archivist/GitHub projection helpers for coferlandia-project-manager.

The PM no longer mirrors TODO/HISTORY into its own task database. This module keeps the
existing PM command surface while generating runtime/Obsidian projections from GitHub-native
project snapshots.
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from collections import OrderedDict
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from reporting import (
    EXPECTED_ARTIFACTS,
    _build_git_state,
    _git,
    _is_git_repo,
    _project_snapshot,
    _resolve_project_path,
    iter_managed_project_entries,
)

CATALOG_ARTIFACTS = (".agent/catalog/SOURCE_INDEX.md", ".agent/catalog/PROCESSING_RUNS.md")
ALL_EXPECTED_ARTIFACTS = EXPECTED_ARTIFACTS + CATALOG_ARTIFACTS


def _now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H%M%SZ")


def _pm_repo_root() -> Path:
    import subprocess
    result = subprocess.run(["git", "rev-parse", "--show-toplevel"], text=True, capture_output=True, check=False)
    return Path(result.stdout.strip()).resolve() if result.returncode == 0 else Path.cwd().resolve()


def _runtime_root() -> Path:
    return _pm_repo_root() / ".coferlandia/project-manager"


def _load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def _load_config(path: Path | None) -> dict[str, Any]:
    if path is None:
        return {}
    if not path.is_file():
        raise SystemExit(f"Config not found: {path}")
    return _load_json(path)


def _config_value(config: dict[str, Any], *keys: str, default=None):
    value: Any = config
    for key in keys:
        if not isinstance(value, dict) or key not in value:
            return default
        value = value[key]
    return default if value is None else value


def _normalize_path_text(value: str) -> str:
    match = re.match(r"^([A-Za-z]):[\\/](.*)$", value)
    if not match:
        return value
    tail = match.group(2).replace("\\", "/")
    return f"/mnt/{match.group(1).lower()}/{tail}"


def _obsidian_settings(config: dict[str, Any]) -> dict[str, str]:
    root = str(_config_value(config, "obsidian", "vault_root", default="") or "")
    root = _normalize_path_text(root) if root else (_pm_repo_root() / "obsidian").as_posix()
    return {
        "vault_root": root,
        "pm_projects_folder": str(_config_value(config, "obsidian", "pm_projects_folder", default="Projects")),
        "pm_tasks_folder": str(_config_value(config, "obsidian", "pm_tasks_folder", default="Projects/issues")),
    }


def _frontmatter_scalar(value: Any) -> str:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    return json.dumps(value, ensure_ascii=False)


def _parse_markdown_frontmatter(text: str) -> tuple[dict[str, Any], str]:
    if not text.startswith("---\n"):
        return {}, text
    lines = text.splitlines()
    try:
        end = lines.index("---", 1)
    except ValueError:
        return {}, text
    payload: OrderedDict[str, Any] = OrderedDict()
    for line in lines[1:end]:
        if ":" not in line:
            continue
        key, raw = line.split(":", 1)
        raw = raw.strip()
        try:
            payload[key.strip()] = json.loads(raw)
        except json.JSONDecodeError:
            payload[key.strip()] = raw
    body = "\n".join(lines[end + 1 :]).lstrip("\n")
    return dict(payload), body


def _write_project_note(path: Path, snapshot: dict[str, Any], config: dict[str, Any]) -> None:
    existing, body = ({}, "")
    if path.is_file():
        existing, body = _parse_markdown_frontmatter(path.read_text(encoding="utf-8", errors="replace"))
    git = snapshot["git"]
    counts = {key: sum(1 for i in snapshot["open_issues"] if i["portfolio_status"] == key) for key in ("backlog", "in-progress", "review", "blocked")}
    updates = OrderedDict(
        [
            ("pm-project", True),
            ("title", snapshot["project_slug"].replace("-", " ").replace("_", " ").title()),
            ("coferlandia-project-id", snapshot["project_slug"]),
            ("repo_path", snapshot["repo_path"]),
            ("repo_remote", git.get("remote_url", "")),
            ("repository", snapshot.get("repository")),
            ("github_project", snapshot.get("github_project")),
            ("pm_owner", "coferlandia-project-manager"),
            ("archivist_initialized", snapshot["archivist_initialized"]),
            ("github_native", snapshot["github_native"]),
            ("last_seen_commit", git.get("last_commit_sha", "")),
            ("open_issues", len(snapshot["open_issues"])),
            ("issues_in_progress", counts["in-progress"]),
            ("issues_in_review", counts["review"]),
            ("blocked_issues", counts["blocked"]),
            ("last_projection_at", _now_iso()),
            ("tags", ["coferlandia", "repo", "agentic-dev"]),
        ]
    )
    merged = OrderedDict(existing)
    merged.update(updates)
    fm = ["---"] + [f"{k}: {_frontmatter_scalar(v)}" for k, v in merged.items()] + ["---", ""]

    managed_lines = [
        "<!-- COFERLANDIA:PROJECT-STATE:START -->",
        "## GitHub Project State",
        "",
        f"- Repository: `{snapshot.get('repository') or 'unresolved'}`",
        f"- Open Issues: {len(snapshot['open_issues'])}",
        f"- In progress: {counts['in-progress']}",
        f"- In review: {counts['review']}",
        f"- Blocked: {counts['blocked']}",
        f"- Recently closed: {len(snapshot['recently_closed'])}",
        f"- GitHub-native migration complete: {snapshot['github_native']}",
        "",
        "### Active Issues",
        "",
    ]
    for issue in snapshot["open_issues"][:30]:
        fields = issue.get("project_fields") or {}
        extra = ", ".join(f"{key}={value}" for key, value in fields.items() if key.lower() != "status" and value not in (None, "", []))
        suffix = f" — {extra}" if extra else ""
        if issue.get("number") is not None and issue.get("url"):
            managed_lines.append(f"- [{issue['portfolio_status']}] [#{issue['number']}]({issue['url']}) {issue['title']}{suffix}")
        else:
            managed_lines.append(f"- [legacy-migration/{issue['portfolio_status']}] {issue.get('legacy_task_id') or 'unmapped'}: {issue['title']}{suffix}")
    managed_lines += ["", "<!-- COFERLANDIA:PROJECT-STATE:END -->", ""]
    managed = "\n".join(managed_lines)

    if "<!-- COFERLANDIA:PROJECT-STATE:START -->" in body and "<!-- COFERLANDIA:PROJECT-STATE:END -->" in body:
        body = re.sub(
            r"<!-- COFERLANDIA:PROJECT-STATE:START -->.*?<!-- COFERLANDIA:PROJECT-STATE:END -->\n?",
            managed.strip() + "\n",
            body,
            flags=re.S,
        )
    else:
        body = managed + ("\n" + body if body else "")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(fm) + body.rstrip() + "\n", encoding="utf-8")


def _issue_note_identity(snapshot: dict[str, Any], issue: dict[str, Any]) -> tuple[str, str]:
    if issue.get("number") is not None:
        return f"{snapshot['project_slug']}-issue-{issue['number']}", "github"
    legacy = str(issue.get("legacy_task_id") or "legacy-unmapped")
    safe = re.sub(r"[^A-Za-z0-9._-]+", "-", legacy).strip("-") or "legacy-unmapped"
    return f"{snapshot['project_slug']}-{safe}", "legacy-migration"


def _write_issue_note(path: Path, snapshot: dict[str, Any], issue: dict[str, Any]) -> None:
    existing, body = ({}, "")
    if path.is_file():
        existing, body = _parse_markdown_frontmatter(path.read_text(encoding="utf-8", errors="replace"))
    note_id, source = _issue_note_identity(snapshot, issue)
    updates = OrderedDict(
        [
            ("pm-task", True),
            ("title", issue.get("title") or note_id),
            ("coferlandia-task-id", note_id),
            ("project", snapshot["project_slug"]),
            ("repository", snapshot.get("repository")),
            ("source", source),
            ("github_issue", issue.get("number")),
            ("github_url", issue.get("url")),
            ("status", issue.get("portfolio_status", "backlog")),
            ("github_project_status", issue.get("project_status")),
            ("github_project_fields", issue.get("project_fields") or {}),
            ("blocked", bool(issue.get("blocked"))),
            ("last_projection_at", _now_iso()),
        ]
    )
    merged = OrderedDict(existing)
    merged.update(updates)
    fm = ["---"] + [f"{k}: {_frontmatter_scalar(v)}" for k, v in merged.items()] + ["---", ""]
    marker_start = "<!-- COFERLANDIA:ISSUE-STATE:START -->"
    marker_end = "<!-- COFERLANDIA:ISSUE-STATE:END -->"
    source_line = f"GitHub Issue [#{issue['number']}]({issue['url']})" if issue.get("number") is not None and issue.get("url") else "Legacy migration item (temporary; migrate to GitHub Issue)"
    managed = "\n".join([
        marker_start,
        "## Operational State",
        "",
        f"- Source: {source_line}",
        f"- Status: {issue.get('portfolio_status', 'backlog')}",
        f"- GitHub Project status: {issue.get('project_status') or 'unconfigured/unset'}",
        f"- Project fields: {json.dumps(issue.get('project_fields') or {}, ensure_ascii=False)}",
        f"- Blocked: {bool(issue.get('blocked'))}",
        "",
        marker_end,
        "",
    ])
    if marker_start in body and marker_end in body:
        body = re.sub(re.escape(marker_start) + r".*?" + re.escape(marker_end) + r"\n?", managed.strip() + "\n", body, flags=re.S)
    else:
        body = managed + ("\n" + body if body else "")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(fm) + body.rstrip() + "\n", encoding="utf-8")


def missing_artifacts(project_path: Path) -> list[str]:
    """Return required Archivist artifacts for the repository's migration state.

    Repositories still carrying TODO/HISTORY are treated as explicit pre-cutover v2
    repositories so the Phase 1 toolset can be installed before each project migration.
    Once those legacy files are removed, the v3 GitHub-native contract applies and the
    internal source index/processing log become required.
    """
    if (project_path / "TODO.md").exists() or (project_path / "HISTORY.md").exists():
        legacy_expected = ("README.md", "TODO.md", "HISTORY.md", "DECISIONS.md", "RUNBOOK.md", "AGENTS.md")
        return [name for name in legacy_expected if not (project_path / name).is_file()]
    return [name for name in ALL_EXPECTED_ARTIFACTS if not (project_path / name).is_file()]


def iter_projects(projects_file: Path):
    for slug, path in iter_managed_project_entries(projects_file):
        yield slug, path, _is_git_repo(path)


def _snapshots(projects_file: Path, config: dict[str, Any]) -> list[dict[str, Any]]:
    default_branch = str(_config_value(config, "git", "default_branch", default="main"))
    return [_project_snapshot(projects_file, slug, path, default_branch) for slug, path in iter_managed_project_entries(projects_file) if _is_git_repo(path)]


def _conflicts(projects_file: Path, snapshots: list[dict[str, Any]]) -> list[dict[str, Any]]:
    conflicts = []
    live_slugs = {p["project_slug"] for p in snapshots}
    for slug, path in iter_managed_project_entries(projects_file):
        if slug not in live_slugs:
            conflicts.append({"project_slug": slug, "repo_path": path.as_posix(), "type": "repo_path_missing"})
    for project in snapshots:
        if project["missing_artifacts"]:
            conflicts.append({"project_slug": project["project_slug"], "type": "missing_archivist_artifact", "missing_artifacts": project["missing_artifacts"]})
        if project["github_errors"]:
            conflicts.append({"project_slug": project["project_slug"], "type": "github_read_error", "details": project["github_errors"]})
        if project.get("repository") and not project.get("github_project"):
            conflicts.append({"project_slug": project["project_slug"], "type": "github_project_unconfigured"})
        if project["legacy_operational_artifacts"]:
            conflicts.append({"project_slug": project["project_slug"], "type": "github_native_migration_required", "details": project["legacy_operational_artifacts"]})
    return conflicts


def _runtime_source(snapshots: list[dict[str, Any]]) -> str:
    return "github" if all(p.get("operational_mode") == "github" for p in snapshots) else "github-with-legacy-migration"


def _runtime_payload(projects_file: Path, config_path: Path | None, snapshots: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "version": 2,
        "last_scan_at": _now_iso(),
        "projects_file": projects_file.as_posix(),
        "projects_detected": len(snapshots),
        "source_of_truth": _runtime_source(snapshots),
        "runtime": {"config_path": config_path.as_posix() if config_path else None},
    }


def _write_runtime(config_path: Path, projects_file: Path, snapshots: list[dict[str, Any]], apply: bool) -> dict[str, Any]:
    root = _runtime_root()
    conflicts = _conflicts(projects_file, snapshots)
    state = _runtime_payload(projects_file, config_path, snapshots)
    project_map = {
        "version": 2,
        "source_of_truth": _runtime_source(snapshots),
        "projects": [
            {
                "project_slug": p["project_slug"],
                "repo_path": p["repo_path"],
                "repository": p.get("repository"),
                "github_project": p.get("github_project"),
                "archivist_status": "initialized" if p["archivist_initialized"] else "missing_artifacts",
                "github_native": p["github_native"],
            }
            for p in snapshots
        ],
    }
    if apply:
        existing_state_path = root / "state.json"
        if existing_state_path.is_file():
            try:
                existing_state = _load_json(existing_state_path)
                if isinstance(existing_state.get("maintenance"), dict):
                    state["maintenance"] = existing_state["maintenance"]
            except (OSError, json.JSONDecodeError):
                pass
        _write_json(root / "state.json", state)
        _write_json(root / "project-map.json", project_map)
        lines = ["# Sync Log", "", f"- Generated at: {state['last_scan_at']}", f"- Source of truth: GitHub", f"- Projects: {len(snapshots)}", f"- Conflicts: {len(conflicts)}", ""]
        (root / "sync-log.md").write_text("\n".join(lines), encoding="utf-8")
        conflict_path = root / "sync-conflicts.md"
        if conflicts:
            conflict_path.write_text("# Sync Conflicts\n\n" + "\n".join(f"- {c['project_slug']}: {c['type']}" for c in conflicts) + "\n", encoding="utf-8")
        elif conflict_path.exists():
            conflict_path.unlink()
    return {"state_path": (root / "state.json").as_posix(), "project_map_path": (root / "project-map.json").as_posix(), "conflicts": conflicts}


def _backup(apply: bool) -> dict[str, Any]:
    runtime = _runtime_root()
    dest = runtime / "backups" / _now_iso()
    copied = []
    if apply:
        dest.mkdir(parents=True, exist_ok=True)
        for name in ("state.json", "project-map.json", "sync-log.md", "sync-conflicts.md"):
            source = runtime / name
            if source.is_file():
                target = dest / name
                shutil.copy2(source, target)
                copied.append(target.as_posix())
    return {"status": "ok", "mode": "apply" if apply else "dry-run", "backup_path": dest.as_posix(), "copied_files": copied}


def cmd_status(args: argparse.Namespace) -> dict[str, Any]:
    projects = []
    for slug, path, is_git in iter_projects(args.projects_file):
        if not is_git:
            continue
        missing = missing_artifacts(path)
        legacy = [rel for rel in ("TODO.md", "HISTORY.md", ".agent/catalog/OPEN_QUESTIONS.md") if (path / rel).exists()]
        projects.append({"project_slug": slug, "repo_path": path.as_posix(), "archivist_initialized": not missing, "missing_artifacts": missing, "github_native": not legacy, "legacy_operational_artifacts": legacy})
    return {"status": "ok", "projects_detected": len(projects), "projects": projects}


def cmd_sync_plan(args: argparse.Namespace) -> dict[str, Any]:
    config = _load_config(args.config)
    snapshots = _snapshots(args.projects_file, config)
    return {"status": "ok", "mode": "dry-run", "source_of_truth": "github", "projects_detected": len(snapshots), "syncable_projects": sum(1 for p in snapshots if not p["github_errors"]), "conflicts": _conflicts(args.projects_file, snapshots)}


def cmd_sync_from_repos(args: argparse.Namespace) -> dict[str, Any]:
    if args.config is None:
        raise SystemExit("Missing required --config <path>")
    config = _load_config(args.config)
    snapshots = _snapshots(args.projects_file, config)
    conflicts = _conflicts(args.projects_file, snapshots)
    source = _runtime_source(snapshots)
    payload = {"status": "ok", "mode": "apply" if args.apply else "dry-run", "source_of_truth": source, "projects_detected": len(snapshots), "syncable_projects": sum(1 for p in snapshots if not p["github_errors"]), "write_performed": args.apply}
    if args.apply:
        hard_conflicts = [c for c in conflicts if c["type"] in {"repo_path_missing", "github_read_error"}]
        if hard_conflicts:
            raise SystemExit("Refusing PM runtime sync because GitHub/repository reads failed")
        if bool(_config_value(config, "scripts", "backup_before_apply", default=True)):
            payload["backup"] = _backup(True)
        payload["runtime"] = _write_runtime(args.config, args.projects_file, snapshots, True)
    else:
        payload["conflicts"] = conflicts
    return payload


def cmd_sync_to_obsidian(args: argparse.Namespace) -> dict[str, Any]:
    if args.config is None:
        raise SystemExit("Missing required --config <path>")
    config = _load_config(args.config)
    settings = _obsidian_settings(config)
    snapshots = _snapshots(args.projects_file, config)
    conflicts = _conflicts(args.projects_file, snapshots)
    hard_conflicts = [c for c in conflicts if c["type"] in {"repo_path_missing", "github_read_error"}]
    if args.apply and hard_conflicts:
        raise SystemExit("Refusing Obsidian projection because GitHub/repository reads failed")
    backup = _backup(True) if args.apply and bool(_config_value(config, "scripts", "backup_before_apply", default=True)) else None
    paths = []
    task_paths = []
    for snapshot in snapshots:
        path = Path(settings["vault_root"]) / settings["pm_projects_folder"] / f"{snapshot['project_slug']}.md"
        paths.append(path.as_posix())
        if args.apply:
            _write_project_note(path, snapshot, config)
        for issue in snapshot["open_issues"]:
            note_id, _ = _issue_note_identity(snapshot, issue)
            task_path = Path(settings["vault_root"]) / settings["pm_tasks_folder"] / f"{note_id}.md"
            task_paths.append(task_path.as_posix())
            if args.apply:
                _write_issue_note(task_path, snapshot, issue)
    if args.apply:
        _write_runtime(args.config, args.projects_file, snapshots, True)
    source = _runtime_source(snapshots)
    payload = {"status": "ok", "mode": "apply" if args.apply else "dry-run", "vault_root": settings["vault_root"], "projects_detected": len(snapshots), "projects_written": len(paths) if args.apply else 0, "project_notes": paths, "tasks_written": len(task_paths) if args.apply else 0, "task_notes": task_paths, "source_of_truth": source}
    if backup is not None:
        payload["backup"] = backup
    return payload


def cmd_backup(args: argparse.Namespace) -> dict[str, Any]:
    if args.config is None:
        raise SystemExit("Missing required --config <path>")
    _load_config(args.config)
    return _backup(args.apply)


def cmd_conflicts(args: argparse.Namespace) -> dict[str, Any]:
    config = _load_config(args.config)
    snapshots = _snapshots(args.projects_file, config)
    conflicts = _conflicts(args.projects_file, snapshots)
    return {"status": "ok", "conflict_count": len(conflicts), "conflicts": conflicts}


def cmd_maintenance(args: argparse.Namespace) -> dict[str, Any]:
    if args.config is None:
        raise SystemExit("Missing required --config <path>")
    config = _load_config(args.config)
    snapshots = _snapshots(args.projects_file, config)
    conflicts = _conflicts(args.projects_file, snapshots)
    payload = {"status": "ok", "mode": "apply" if args.apply else "dry-run", "maintenance_due": bool(conflicts), "archivist": {"projects_detected": len(snapshots), "initialized_projects": sum(1 for p in snapshots if p["archivist_initialized"]), "initialized_all": all(p["archivist_initialized"] for p in snapshots) if snapshots else True}, "conflicts": {"conflict_count": len(conflicts), "items": conflicts}, "write_performed": args.apply, "source_of_truth": "github"}
    if args.apply:
        runtime = _runtime_root()
        state = _runtime_payload(args.projects_file, args.config, snapshots)
        interval = int(_config_value(config, "archivist", "maintenance_interval_days", default=7))
        state["maintenance"] = {"last_run_at": _now_iso(), "next_due_at": (datetime.now(timezone.utc) + timedelta(days=interval)).strftime("%Y-%m-%dT%H%M%SZ")}
        _write_json(runtime / "state.json", state)
    return payload


_COMMANDS = {"status": cmd_status, "sync-plan": cmd_sync_plan, "sync-from-repos": cmd_sync_from_repos, "sync-to-obsidian": cmd_sync_to_obsidian, "backup": cmd_backup, "conflicts": cmd_conflicts, "maintenance": cmd_maintenance}


def _render_text(command: str, payload: dict[str, Any]) -> str:
    if command == "status":
        lines = ["Archivist status report:", f"- projects_detected: {payload['projects_detected']}"]
        for p in payload["projects"]:
            lines.append(f"- {p['project_slug']}: initialized={p['archivist_initialized']} github_native={p['github_native']} missing={','.join(p['missing_artifacts']) or 'none'}")
        return "\n".join(lines)
    if command == "sync-to-obsidian":
        return "\n".join(["Obsidian projection report:", f"- mode: {payload['mode']}", f"- source_of_truth: {payload['source_of_truth']}", f"- projects_written: {payload['projects_written']}", f"- tasks_written: {payload['tasks_written']}"])
    if command == "conflicts":
        return "\n".join(["Sync conflict report:", f"- conflict_count: {payload['conflict_count']}"] + [f"- {c['project_slug']}: {c['type']}" for c in payload['conflicts']])
    return json.dumps(payload, indent=2, ensure_ascii=False)


def _build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="GitHub-native Archivist/PM projection helpers.")
    sub = p.add_subparsers(dest="command", required=True)
    for name in _COMMANDS:
        sp = sub.add_parser(name)
        sp.add_argument("--projects-file", required=True, type=Path)
        sp.add_argument("--config", type=Path)
        sp.add_argument("--apply", action="store_true")
        sp.add_argument("--format", choices=("json", "text"), default="text")
    return p


def main(argv=None) -> int:
    args = _build_parser().parse_args(argv)
    payload = _COMMANDS[args.command](args)
    print(json.dumps(payload, indent=2, ensure_ascii=False) if args.format == "json" else _render_text(args.command, payload))
    return 0 if payload.get("status") != "error" else 1


if __name__ == "__main__":
    sys.exit(main())
