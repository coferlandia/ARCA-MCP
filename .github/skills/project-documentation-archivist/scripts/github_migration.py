#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# ///
"""Deterministic helper for migrating Archivist v2 operational files to GitHub.

Semantic classification deliberately remains outside this script. Archivist (or a human)
reviews the inventory and fills the decisions file. This tool validates those decisions,
performs idempotent GitHub mutations, records mappings, and validates cutover readiness.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ALLOWED_DISPOSITIONS = {
    "EXISTING_ISSUE",
    "EXISTING_PR",
    "EXISTING_GIT_EVIDENCE",
    "CREATE_OPEN_ISSUE",
    "CREATE_CLOSED_HISTORICAL_ISSUE",
    "KNOWLEDGE_ONLY",
    "OBSOLETE",
    "DUPLICATE",
    "NEEDS_REVIEW",
}
CREATE_DISPOSITIONS = {"CREATE_OPEN_ISSUE", "CREATE_CLOSED_HISTORICAL_ISSUE"}
CANONICAL_FILES = ("README.md", "AGENTS.md", "DECISIONS.md", "RUNBOOK.md")
DEFAULT_MIGRATION_DIR = Path(".agent/migrations")


@dataclass
class CommandResult:
    returncode: int
    stdout: str
    stderr: str


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def run(cmd: list[str], *, cwd: Path, check: bool = False) -> CommandResult:
    result = subprocess.run(cmd, cwd=cwd, text=True, capture_output=True, check=False)
    wrapped = CommandResult(result.returncode, result.stdout.strip(), result.stderr.strip())
    if check and wrapped.returncode != 0:
        message = wrapped.stderr or wrapped.stdout or f"command failed: {' '.join(cmd)}"
        raise RuntimeError(message)
    return wrapped


def stable_id(kind: str, raw: str) -> str:
    digest = hashlib.sha256(raw.encode("utf-8")).hexdigest()[:12]
    return f"legacy-{kind.lower()}-{digest}"


def git_root(project_root: Path) -> Path:
    result = run(["git", "rev-parse", "--show-toplevel"], cwd=project_root, check=True)
    return Path(result.stdout).resolve()


def gh_json(project_root: Path, *args: str) -> Any:
    result = run(["gh", *args], cwd=project_root, check=True)
    if not result.stdout:
        return None
    return json.loads(result.stdout)


def resolve_repo(project_root: Path) -> dict[str, Any]:
    return gh_json(
        project_root,
        "repo",
        "view",
        "--json",
        "nameWithOwner,url,hasIssuesEnabled,hasProjectsEnabled,defaultBranchRef",
    )


def git_state(project_root: Path) -> dict[str, Any]:
    branch = run(["git", "branch", "--show-current"], cwd=project_root, check=True).stdout
    commit = run(["git", "rev-parse", "HEAD"], cwd=project_root, check=True).stdout
    porcelain = run(["git", "status", "--porcelain"], cwd=project_root, check=True).stdout
    return {"branch": branch or "(detached)", "commit": commit, "dirty": bool(porcelain)}


def parse_todo(path: Path) -> list[dict[str, Any]]:
    if not path.is_file():
        return []
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    items: list[dict[str, Any]] = []
    heading = ""
    current: list[str] = []
    current_checked: bool | None = None
    fingerprints: dict[str, int] = {}

    def flush() -> None:
        nonlocal current, current_checked
        if not current:
            return
        raw = "\n".join(current).strip()
        if raw:
            fingerprint = f"{heading}\n{raw}"
            fingerprints[fingerprint] = fingerprints.get(fingerprint, 0) + 1
            identity = f"{fingerprint}\noccurrence:{fingerprints[fingerprint]}"
            items.append(
                {
                    "legacy_id": stable_id("todo", identity),
                    "legacy_source": "TODO.md",
                    "kind": "todo",
                    "section": heading,
                    "checkbox_done": current_checked,
                    "raw": raw,
                }
            )
        current = []
        current_checked = None

    checkbox_re = re.compile(r"^\s*[-*]\s+\[([ xX])\]\s+(.+)$")
    for line in lines:
        if line.startswith("#"):
            flush()
            heading = line.lstrip("#").strip()
            continue
        match = checkbox_re.match(line)
        if match:
            flush()
            current_checked = match.group(1).lower() == "x"
            current = [line]
            continue
        if current:
            if line.strip() and (line.startswith(" ") or line.startswith("\t") or ":" in line):
                current.append(line)
            elif not line.strip():
                current.append(line)
            else:
                flush()
        elif line.strip() and not line.startswith("---"):
            # Preserve structured non-checkbox entries as reviewable inventory items.
            current = [line]
    flush()
    return items


def parse_history(path: Path) -> list[dict[str, Any]]:
    if not path.is_file():
        return []
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    items: list[dict[str, Any]] = []
    heading = ""
    date = ""
    current: list[str] = []
    fingerprints: dict[str, int] = {}

    def flush() -> None:
        nonlocal current
        raw = "\n".join(current).strip()
        if raw:
            fingerprint = f"{date}\n{heading}\n{raw}"
            fingerprints[fingerprint] = fingerprints.get(fingerprint, 0) + 1
            identity = f"{fingerprint}\noccurrence:{fingerprints[fingerprint]}"
            items.append(
                {
                    "legacy_id": stable_id("history", identity),
                    "legacy_source": "HISTORY.md",
                    "kind": "history",
                    "section": heading,
                    "original_date": date or None,
                    "raw": raw,
                }
            )
        current = []

    date_re = re.compile(r"\b(20\d{2}-\d{2}-\d{2})\b")
    start_re = re.compile(r"^\s*[-*]\s+\S|^##+\s+(?:HIST|\d{4}-\d{2}-\d{2})", re.I)
    for line in lines:
        if line.startswith("#"):
            flush()
            heading = line.lstrip("#").strip()
            found = date_re.search(line)
            date = found.group(1) if found else date
            if re.match(r"^##+\s+HIST", line, re.I):
                current = [line]
            continue
        found = date_re.search(line)
        if found and line.lower().startswith("date:"):
            date = found.group(1)
        if start_re.match(line) and current:
            flush()
        if line.strip() and not line.startswith("---"):
            current.append(line)
        elif current:
            current.append(line)
    flush()
    return items


def parse_open_questions(path: Path) -> list[dict[str, Any]]:
    if not path.is_file():
        return []
    text = path.read_text(encoding="utf-8", errors="replace")
    blocks = re.split(r"(?m)^##\s+(?=QUESTION-)", text)
    items = []
    for block in blocks[1:]:
        raw = "## " + block.strip()
        if re.search(r"(?mi)^Status:\s*resolved\b", raw):
            continue
        items.append(
            {
                "legacy_id": stable_id("question", raw),
                "legacy_source": ".agent/catalog/OPEN_QUESTIONS.md",
                "kind": "open-question",
                "raw": raw,
            }
        )
    return items


def inventory_payload(project_root: Path) -> dict[str, Any]:
    root = git_root(project_root)
    items = parse_todo(root / "TODO.md")
    items += parse_history(root / "HISTORY.md")
    items += parse_open_questions(root / ".agent/catalog/OPEN_QUESTIONS.md")
    return {
        "schema_version": 1,
        "generated_at": now_iso(),
        "project_root": root.as_posix(),
        "base": git_state(root),
        "items": items,
    }


def default_decisions(inventory: dict[str, Any]) -> dict[str, Any]:
    decisions = []
    for item in inventory["items"]:
        decisions.append(
            {
                "legacy_id": item["legacy_id"],
                "disposition": "NEEDS_REVIEW",
                "target": None,
                "issue": {
                    "title": "",
                    "body": "",
                    "labels": [],
                },
                "feeds": [],
                "notes": "",
            }
        )
    return {
        "schema_version": 1,
        "inventory_generated_at": inventory["generated_at"],
        "decisions_updated_at": None,
        "items": decisions,
    }


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise SystemExit(f"Cannot read JSON {path}: {exc}") from exc


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def file_sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_project_args(owner: str | None, number: int | None) -> None:
    if bool(owner) != (number is not None):
        raise RuntimeError("GitHub Project owner and number must be supplied together")


def validate_decisions(inventory: dict[str, Any], decisions: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    inventory_ids = {item["legacy_id"] for item in inventory.get("items", [])}
    seen: set[str] = set()
    for decision in decisions.get("items", []):
        legacy_id = decision.get("legacy_id")
        if legacy_id in seen:
            errors.append(f"Duplicate decision for {legacy_id}")
        seen.add(legacy_id)
        if legacy_id not in inventory_ids:
            errors.append(f"Decision references unknown inventory item: {legacy_id}")
        disposition = decision.get("disposition")
        if disposition not in ALLOWED_DISPOSITIONS:
            errors.append(f"Invalid disposition for {legacy_id}: {disposition}")
        if disposition in {"EXISTING_ISSUE", "EXISTING_PR", "EXISTING_GIT_EVIDENCE", "DUPLICATE"} and not decision.get("target"):
            errors.append(f"{disposition} requires target for {legacy_id}")
        if disposition in CREATE_DISPOSITIONS:
            issue = decision.get("issue") or {}
            if not str(issue.get("title") or "").strip():
                errors.append(f"{disposition} requires issue.title for {legacy_id}")
        if disposition == "KNOWLEDGE_ONLY":
            feeds = [str(x) for x in decision.get("feeds", [])]
            if not feeds:
                errors.append(f"KNOWLEDGE_ONLY requires at least one durable feed for {legacy_id}")
            invalid = [x for x in feeds if x not in CANONICAL_FILES]
            if invalid:
                errors.append(f"KNOWLEDGE_ONLY has invalid durable feed(s) for {legacy_id}: {', '.join(invalid)}")
            if not str(decision.get("notes") or "").strip():
                errors.append(f"KNOWLEDGE_ONLY requires notes describing the distilled knowledge for {legacy_id}")
        if disposition == "OBSOLETE" and not str(decision.get("notes") or "").strip():
            errors.append(f"OBSOLETE requires notes explaining the disposition for {legacy_id}")
    missing = inventory_ids - seen
    for legacy_id in sorted(missing):
        errors.append(f"Missing decision for {legacy_id}")
    return errors


def marker(legacy_id: str) -> str:
    return f"<!-- coferlandia-migration-id: {legacy_id} -->"


def find_marker_issue(root: Path, repository: str, legacy_id: str) -> dict[str, Any] | None:
    # gh issue list supports advanced search and body matching through GitHub search.
    query = f'"coferlandia-migration-id: {legacy_id}" in:body'
    result = run(
        [
            "gh",
            "issue",
            "list",
            "--repo",
            repository,
            "--state",
            "all",
            "--search",
            query,
            "--limit",
            "10",
            "--json",
            "number,title,state,url,body",
        ],
        cwd=root,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr or result.stdout or f"Could not check migration marker for {legacy_id}")
    if not result.stdout:
        return None
    try:
        issues = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Invalid GitHub JSON while checking migration marker for {legacy_id}") from exc
    expected = marker(legacy_id)
    for issue in issues:
        if expected in (issue.get("body") or ""):
            return issue
    return None


def _issue_number_from_target(target: Any) -> int | None:
    raw = str(target or "").strip()
    match = re.search(r"(?:^#?|/issues/)(\d+)(?:$|[/?#])", raw)
    return int(match.group(1)) if match else None


def resolve_existing_issue(root: Path, repository: str, target: Any) -> dict[str, Any]:
    number = _issue_number_from_target(target)
    if number is None:
        raise RuntimeError(f"EXISTING_ISSUE target must contain an Issue number: {target}")
    payload = gh_json(root, "issue", "view", str(number), "--repo", repository, "--json", "number,title,state,url")
    if not isinstance(payload, dict) or not payload.get("url"):
        raise RuntimeError(f"Could not resolve existing Issue target: {target}")
    return payload


def resolve_existing_pr(root: Path, repository: str, target: Any) -> dict[str, Any]:
    raw = str(target or "").strip()
    match = re.search(r"(?:^#?|/pull/)(\d+)(?:$|[/?#])", raw)
    if not match:
        raise RuntimeError(f"EXISTING_PR target must contain a PR number: {target}")
    payload = gh_json(root, "pr", "view", match.group(1), "--repo", repository, "--json", "number,title,state,url,mergedAt")
    if not isinstance(payload, dict) or not payload.get("url"):
        raise RuntimeError(f"Could not resolve existing PR target: {target}")
    return payload


def validate_git_evidence(root: Path, target: Any) -> str:
    raw = str(target or "").strip()
    result = run(["git", "rev-parse", "--verify", f"{raw}^{{commit}}"], cwd=root, check=False)
    if result.returncode != 0:
        raise RuntimeError(f"EXISTING_GIT_EVIDENCE target is not a commit reachable in this repository: {raw}")
    return result.stdout


def ensure_historical_closed(root: Path, repository: str, issue: dict[str, Any], *, apply: bool) -> str:
    if str(issue.get("state") or "").upper() == "CLOSED":
        return "already-closed"
    if not apply:
        return "would-close"
    run(["gh", "issue", "close", str(issue["number"]), "--repo", repository, "--reason", "completed"], cwd=root, check=True)
    return "closed"


def create_issue(root: Path, repository: str, legacy_item: dict[str, Any], decision: dict[str, Any]) -> dict[str, Any]:
    issue = decision["issue"]
    body_parts = [str(issue.get("body") or "").rstrip()]
    body_parts.append("")
    body_parts.append(marker(decision["legacy_id"]))
    body_parts.append(f"Migrated from: `{legacy_item['legacy_source']}`")
    if legacy_item.get("original_date"):
        body_parts.append(f"Original date: `{legacy_item['original_date']}`")
    # Never publish the legacy source body automatically. Legacy documentation can
    # contain credentials, customer data, internal notes, or other material that is
    # inappropriate for a GitHub Issue (especially in a public repository). The
    # decisions file contains the human/Archivist-curated Issue body; provenance is
    # preserved through the migration marker and source path only.
    body = "\n".join(body_parts).strip() + "\n"
    cmd = ["gh", "issue", "create", "--repo", repository, "--title", issue["title"], "--body", body]
    labels = [str(x) for x in issue.get("labels", []) if str(x).strip()]
    for label in labels:
        cmd.extend(["--label", label])
    result = run(cmd, cwd=root, check=True)
    url = result.stdout.splitlines()[-1].strip()
    number_match = re.search(r"/(\d+)$", url)
    if not number_match:
        raise RuntimeError(f"Could not determine issue number from gh output: {result.stdout}")
    number = int(number_match.group(1))
    # Persist the created Issue mapping in the caller before any follow-up mutation
    # (Project insertion or historical closure). This closes the search-index delay
    # window that could otherwise create duplicates after a partial failure.
    return {"number": number, "url": url, "state": "OPEN"}


def add_issue_to_project(root: Path, issue_url: str, owner: str | None, number: int | None) -> None:
    if not owner or not number:
        return
    result = run(
        ["gh", "project", "item-add", str(number), "--owner", owner, "--url", issue_url, "--format", "json"],
        cwd=root,
        check=False,
    )
    # GitHub returns an error if an item is already present. Treat an explicit already-exists
    # response as idempotent, but surface every other failure.
    if result.returncode != 0 and "already" not in (result.stderr + result.stdout).lower():
        raise RuntimeError(result.stderr or result.stdout or "gh project item-add failed")


def cmd_preflight(args: argparse.Namespace) -> int:
    root = git_root(Path(args.project_root).resolve())
    validate_project_args(args.project_owner, args.project_number)
    gh = shutil.which("gh")
    payload: dict[str, Any] = {"status": "ok", "project_root": root.as_posix(), "gh": gh, "git": git_state(root)}
    if not gh:
        payload["status"] = "error"
        payload["error"] = "GitHub CLI (gh) not found"
    else:
        auth = run(["gh", "auth", "status"], cwd=root, check=False)
        payload["gh_authenticated"] = auth.returncode == 0
        if auth.returncode != 0:
            payload["status"] = "error"
            payload["error"] = auth.stderr or auth.stdout
        else:
            try:
                payload["repository"] = resolve_repo(root)
                if not payload["repository"].get("hasIssuesEnabled", False):
                    raise RuntimeError("GitHub Issues are disabled for this repository")
                if args.project_owner and args.project_number:
                    payload["github_project"] = gh_json(
                        root, "project", "view", str(args.project_number),
                        "--owner", args.project_owner, "--format", "json"
                    )
            except Exception as exc:  # noqa: BLE001
                payload["status"] = "error"
                payload["error"] = str(exc)
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0 if payload["status"] == "ok" else 1


def cmd_inventory(args: argparse.Namespace) -> int:
    root = git_root(Path(args.project_root).resolve())
    migration_dir = root / args.migration_dir
    inventory_path = root / args.inventory if args.inventory else migration_dir / "github-native-inventory.json"
    decisions_path = root / args.decisions if args.decisions else migration_dir / "github-native-decisions.json"
    inventory = inventory_payload(root)
    write_json(inventory_path, inventory)
    if not decisions_path.exists() or args.overwrite_decisions:
        write_json(decisions_path, default_decisions(inventory))
    print(json.dumps({"status": "ok", "inventory": inventory_path.as_posix(), "decisions": decisions_path.as_posix(), "items": len(inventory["items"])}, indent=2))
    return 0


def resolve_inventory(root: Path, decisions_path: Path, explicit_inventory: str | None) -> tuple[Path, dict[str, Any], dict[str, Any]]:
    decisions = load_json(decisions_path)
    inventory_path = root / explicit_inventory if explicit_inventory else root / DEFAULT_MIGRATION_DIR / "github-native-inventory.json"
    inventory = load_json(inventory_path)
    return inventory_path, inventory, decisions


def cmd_validate_decisions(args: argparse.Namespace) -> int:
    root = git_root(Path(args.project_root).resolve())
    decisions_path = (root / args.decisions).resolve()
    _, inventory, decisions = resolve_inventory(root, decisions_path, args.inventory)
    errors = validate_decisions(inventory, decisions)
    payload = {"status": "ok" if not errors else "error", "items": len(inventory.get("items", [])), "errors": errors}
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0 if not errors else 1


def cmd_apply(args: argparse.Namespace) -> int:
    root = git_root(Path(args.project_root).resolve())
    validate_project_args(args.project_owner, args.project_number)
    decisions_path = (root / args.decisions).resolve()
    inventory_path, inventory, decisions = resolve_inventory(root, decisions_path, args.inventory)
    errors = validate_decisions(inventory, decisions)
    if args.apply:
        unresolved = [item.get("legacy_id") for item in decisions.get("items", []) if item.get("disposition") == "NEEDS_REVIEW"]
        if unresolved:
            errors.append("Write apply is blocked while migration decisions remain NEEDS_REVIEW: " + ", ".join(str(x) for x in unresolved))
    if errors:
        print(json.dumps({"status": "error", "errors": errors}, indent=2), file=sys.stderr)
        return 1

    repo_info = resolve_repo(root)
    repository = repo_info["nameWithOwner"]
    by_id = {item["legacy_id"]: item for item in inventory["items"]}
    map_path = root / args.map if args.map else root / DEFAULT_MIGRATION_DIR / "github-native-map.json"
    inventory_hash = file_sha256(inventory_path)
    decisions_hash = file_sha256(decisions_path)

    prior_by_id: dict[str, dict[str, Any]] = {}
    if args.apply and map_path.is_file():
        prior_map = load_json(map_path)
        if prior_map.get("repository") != repository:
            raise RuntimeError("Existing migration journal belongs to a different GitHub repository")
        if prior_map.get("inventory_sha256") != inventory_hash or prior_map.get("decisions_sha256") != decisions_hash:
            raise RuntimeError("Existing migration journal is stale; do not reuse it after inventory/decision changes")
        prior_by_id = {item.get("legacy_id"): item for item in prior_map.get("results", [])}

    results: list[dict[str, Any]] = []

    def payload(*, complete: bool) -> dict[str, Any]:
        return {
            "schema_version": 1,
            "generated_at": now_iso(),
            "mode": "apply" if args.apply else "dry-run",
            "complete": complete,
            "repository": repository,
            "inventory": inventory_path.as_posix(),
            "inventory_sha256": inventory_hash,
            "decisions": decisions_path.as_posix(),
            "decisions_sha256": decisions_hash,
            "results": results,
        }

    def persist_progress() -> None:
        if args.apply:
            write_json(map_path, payload(complete=False))

    for decision in decisions["items"]:
        legacy_id = decision["legacy_id"]
        disposition = decision["disposition"]
        entry: dict[str, Any] = {"legacy_id": legacy_id, "disposition": disposition, "action": "none"}
        if disposition in CREATE_DISPOSITIONS:
            # Prefer a locally journaled Issue over GitHub search. Search indexing is
            # eventually consistent and is not sufficient by itself after a partial run.
            prior = prior_by_id.get(legacy_id)
            existing = None
            if prior and prior.get("github", {}).get("number"):
                existing = resolve_existing_issue(root, repository, prior["github"]["number"])
                entry.update({"action": "reused-journaled-issue", "github": existing})
            else:
                existing = find_marker_issue(root, repository, legacy_id)
                if existing:
                    entry.update({"action": "reused-marker-issue", "github": {"number": existing["number"], "url": existing["url"], "state": existing["state"]}})

            if existing:
                results.append(entry)
                persist_progress()
            elif args.apply:
                created = create_issue(root, repository, by_id[legacy_id], decision)
                entry.update({"action": "created", "github": created})
                results.append(entry)
                # Critical ordering: journal the GitHub identity before any later mutation.
                persist_progress()
            else:
                entry["action"] = "would-create"
                results.append(entry)

            if args.apply and entry.get("github"):
                if disposition == "CREATE_OPEN_ISSUE":
                    add_issue_to_project(root, entry["github"]["url"], args.project_owner, args.project_number)
                else:
                    close_action = ensure_historical_closed(root, repository, entry["github"], apply=True)
                    entry["close_action"] = close_action
                    entry["github"]["state"] = "CLOSED"
                persist_progress()
            elif not args.apply and existing and disposition == "CREATE_CLOSED_HISTORICAL_ISSUE":
                entry["close_action"] = ensure_historical_closed(root, repository, existing, apply=False)
        elif disposition == "EXISTING_ISSUE":
            resolved = resolve_existing_issue(root, repository, decision.get("target"))
            entry.update({"action": "validated-mapping", "target": decision.get("target"), "github": resolved})
            results.append(entry)
            persist_progress()
            if args.apply and str(resolved.get("state") or "").upper() == "OPEN":
                add_issue_to_project(root, resolved["url"], args.project_owner, args.project_number)
        elif disposition == "EXISTING_PR":
            resolved = resolve_existing_pr(root, repository, decision.get("target"))
            entry.update({"action": "validated-mapping", "target": decision.get("target"), "github": resolved})
            results.append(entry)
            persist_progress()
        elif disposition == "EXISTING_GIT_EVIDENCE":
            sha = validate_git_evidence(root, decision.get("target"))
            entry.update({"action": "validated-mapping", "target": decision.get("target"), "git_commit": sha})
            results.append(entry)
            persist_progress()
        elif disposition == "DUPLICATE":
            entry.update({"action": "mapped-duplicate", "target": decision.get("target")})
            results.append(entry)
            persist_progress()
        else:
            entry["action"] = "no-github-mutation"
            results.append(entry)
            persist_progress()

    migration_map = payload(complete=True)
    if args.apply:
        write_json(map_path, migration_map)
    print(json.dumps(migration_map, indent=2, ensure_ascii=False))
    return 0


def cmd_validate_cutover(args: argparse.Namespace) -> int:
    root = git_root(Path(args.project_root).resolve())
    decisions_path = (root / args.decisions).resolve()
    _, inventory, decisions = resolve_inventory(root, decisions_path, args.inventory)
    errors = validate_decisions(inventory, decisions)
    for decision in decisions.get("items", []):
        if decision.get("disposition") == "NEEDS_REVIEW":
            errors.append(f"Unresolved migration decision: {decision.get('legacy_id')}")

    # Refuse cutover if legacy sources changed after inventory. A later edit must be
    # inventoried and classified rather than silently disappearing when legacy files
    # are removed. Stable IDs are content-derived, so set equality catches additions,
    # deletions, and edits of inventory items.
    current_inventory = inventory_payload(root)
    inventoried_ids = {item.get("legacy_id") for item in inventory.get("items", [])}
    current_ids = {item.get("legacy_id") for item in current_inventory.get("items", [])}
    if inventoried_ids != current_ids:
        errors.append("Legacy operational sources changed after inventory; regenerate inventory and review decisions")

    map_path = root / args.map if args.map else root / DEFAULT_MIGRATION_DIR / "github-native-map.json"
    migration_map = load_json(map_path) if map_path.is_file() else {"results": []}
    if map_path.is_file():
        if migration_map.get("complete") is not True:
            errors.append("Migration journal records an incomplete apply; rerun apply to resume")
        if migration_map.get("inventory_sha256") != file_sha256((root / args.inventory).resolve() if args.inventory else root / DEFAULT_MIGRATION_DIR / "github-native-inventory.json"):
            errors.append("Migration map does not match the current inventory file; rerun apply")
        if migration_map.get("decisions_sha256") != file_sha256(decisions_path):
            errors.append("Migration map does not match the current decisions file; rerun apply")
    mapped = {item.get("legacy_id"): item for item in migration_map.get("results", [])}
    current_repository = resolve_repo(root).get("nameWithOwner")
    if map_path.is_file() and migration_map.get("repository") != current_repository:
        errors.append("Migration map belongs to a different GitHub repository; rerun apply")
    for decision in decisions.get("items", []):
        disposition = decision.get("disposition")
        result = mapped.get(decision.get("legacy_id"))
        if disposition in CREATE_DISPOSITIONS | {"EXISTING_ISSUE", "EXISTING_PR", "EXISTING_GIT_EVIDENCE", "DUPLICATE"}:
            if not result:
                errors.append(f"Migration disposition lacks applied mapping evidence: {decision.get('legacy_id')}")
            elif result.get("disposition") != disposition:
                errors.append(f"Migration mapping disposition is stale for {decision.get('legacy_id')}; rerun apply")
            elif disposition in {"EXISTING_ISSUE", "EXISTING_PR", "EXISTING_GIT_EVIDENCE", "DUPLICATE"} and result.get("target") != decision.get("target"):
                errors.append(f"Migration mapping target is stale for {decision.get('legacy_id')}; rerun apply")
            elif disposition in CREATE_DISPOSITIONS | {"EXISTING_ISSUE", "EXISTING_PR"} and not result.get("github"):
                errors.append(f"GitHub mapping lacks validated GitHub evidence: {decision.get('legacy_id')}")
            elif disposition == "EXISTING_GIT_EVIDENCE" and not result.get("git_commit"):
                errors.append(f"Git evidence mapping lacks validated commit: {decision.get('legacy_id')}")
            elif disposition in CREATE_DISPOSITIONS | {"EXISTING_ISSUE"} and result.get("github"):
                try:
                    live = resolve_existing_issue(root, current_repository, result["github"].get("number"))
                    if disposition == "CREATE_CLOSED_HISTORICAL_ISSUE" and str(live.get("state") or "").upper() != "CLOSED":
                        errors.append(f"Historical Issue is no longer closed: {decision.get('legacy_id')}")
                except RuntimeError as exc:
                    errors.append(f"GitHub Issue mapping is no longer resolvable for {decision.get('legacy_id')}: {exc}")
            elif disposition == "EXISTING_PR" and result.get("github"):
                try:
                    resolve_existing_pr(root, current_repository, result["github"].get("number"))
                except RuntimeError as exc:
                    errors.append(f"GitHub PR mapping is no longer resolvable for {decision.get('legacy_id')}: {exc}")
            elif disposition == "EXISTING_GIT_EVIDENCE" and result.get("git_commit"):
                try:
                    validate_git_evidence(root, result.get("git_commit"))
                except RuntimeError as exc:
                    errors.append(f"Git evidence mapping is no longer resolvable for {decision.get('legacy_id')}: {exc}")

    for rel in CANONICAL_FILES:
        if not (root / rel).is_file():
            errors.append(f"Missing durable canonical file: {rel}")

    payload = {
        "status": "ready" if not errors else "blocked",
        "project_root": root.as_posix(),
        "legacy_files_present": [rel for rel in ("TODO.md", "HISTORY.md", ".agent/catalog/OPEN_QUESTIONS.md") if (root / rel).exists()],
        "errors": errors,
        "next_action": "remove legacy operational files, then run validate_catalog.py --require-github-native" if not errors else "resolve blockers",
    }
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0 if not errors else 1


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="command", required=True)

    def common(sp: argparse.ArgumentParser) -> None:
        sp.add_argument("--project-root", default=".")

    sp = sub.add_parser("preflight")
    common(sp)
    sp.add_argument("--project-owner")
    sp.add_argument("--project-number", type=int)
    sp.set_defaults(func=cmd_preflight)

    sp = sub.add_parser("inventory")
    common(sp)
    sp.add_argument("--migration-dir", type=Path, default=DEFAULT_MIGRATION_DIR)
    sp.add_argument("--inventory")
    sp.add_argument("--decisions")
    sp.add_argument("--overwrite-decisions", action="store_true")
    sp.set_defaults(func=cmd_inventory)

    sp = sub.add_parser("validate-decisions")
    common(sp)
    sp.add_argument("--inventory")
    sp.add_argument("--decisions", required=True)
    sp.set_defaults(func=cmd_validate_decisions)

    sp = sub.add_parser("apply")
    common(sp)
    sp.add_argument("--inventory")
    sp.add_argument("--decisions", required=True)
    sp.add_argument("--map")
    sp.add_argument("--project-owner")
    sp.add_argument("--project-number", type=int)
    sp.add_argument("--apply", action="store_true")
    sp.set_defaults(func=cmd_apply)

    sp = sub.add_parser("validate-cutover")
    common(sp)
    sp.add_argument("--inventory")
    sp.add_argument("--decisions", required=True)
    sp.add_argument("--map")
    sp.set_defaults(func=cmd_validate_cutover)
    return p


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        return args.func(args)
    except (RuntimeError, OSError, ValueError) as exc:
        print(json.dumps({"status": "error", "error": str(exc)}, indent=2), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
