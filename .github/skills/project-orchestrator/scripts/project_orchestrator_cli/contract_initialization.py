"""One-time initialization between local work contracts and GitHub Issues."""
from __future__ import annotations

import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any

from .contracts import ValidationError
from .github_service import GitHubService, IssueRef
from .materialization import ANALYSIS_MARKER, now
from .work_items import direct_plan_manifest, parse_execution_strategy, sha256_text, validate_manifest

CONTRACT_MARKER_PREFIX = "coferlandia-contract-id"
EPIC_LABEL = "type:epic"


def _atomic_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent, text=True)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(value, handle, indent=2, sort_keys=True, ensure_ascii=False)
            handle.write("\n")
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def _atomic_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent, text=True)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            handle.write(text)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def _path(repo: Path, raw: str | Path) -> Path:
    value = Path(raw)
    return value if value.is_absolute() else repo / value


def _read_contract(repo: Path, raw: str | Path, label: str) -> str:
    path = _path(repo, raw)
    if not path.is_file():
        raise ValidationError(f"{label} contract not found: {path}")
    text = path.read_text(encoding="utf-8")
    if not text.strip():
        raise ValidationError(f"{label} contract is empty: {path}")
    return text


def _strip_projection_frontmatter(text: str) -> str:
    if not text.startswith("---\n"):
        return text.strip()
    end = text.find("\n---\n", 4)
    if end < 0:
        return text.strip()
    frontmatter = text[4:end]
    generated_keys = ("snapshot:", "repository:", "issue:", "epic:", "work_item:", "materialized_at:")
    if any(re.search(rf"(?m)^\s*{re.escape(key)}", frontmatter) for key in generated_keys):
        return text[end + 5 :].strip()
    return text.strip()


def _title(text: str, fallback: str) -> str:
    match = re.search(r"(?m)^#\s+(.+?)\s*$", text)
    return match.group(1).strip() if match else fallback


def _marker(kind: str, identity: str) -> str:
    clean = str(identity).strip()
    if not clean or "\n" in clean or "-->" in clean:
        raise ValidationError(f"invalid {kind} contract identity: {identity!r}")
    return f"<!-- {CONTRACT_MARKER_PREFIX}: {kind}:{clean} -->"


def _with_marker(text: str, marker: str, *, parent_epic: int | None = None) -> str:
    body = _strip_projection_frontmatter(text)
    lines = [marker]
    if parent_epic is not None:
        lines.extend([f"Parent Epic: #{parent_epic}"])
    lines.extend(["", body])
    return "\n".join(lines).rstrip() + "\n"


def _strategy(repo: Path, manifest: dict[str, Any]) -> dict[str, str]:
    raw = manifest.get("execution_strategy")
    if isinstance(raw, dict):
        return {str(key): str(value) for key, value in raw.items()}
    epic_path = manifest.get("epic", {}).get("path")
    if not epic_path:
        raise ValidationError("manifest requires an Epic path to resolve Execution Strategy")
    return parse_execution_strategy(_read_contract(repo, epic_path, "Epic"))


def _tracking(strategy: dict[str, str]) -> str:
    value = strategy.get("tracking", "").strip().lower()
    if value == "github":
        return "github"
    if value in {"local", "local fallback", "local-fallback"}:
        return "local"
    raise ValidationError(f"unsupported Execution Strategy tracking: {strategy.get('tracking')}")


def _find_marked(issues: list[dict[str, Any]], marker: str) -> dict[str, Any] | None:
    matches = [item for item in issues if marker in str(item.get("body") or "")]
    if len(matches) > 1:
        numbers = ", ".join(f"#{item.get('number')}" for item in matches)
        raise ValidationError(f"contract marker collision for {marker}: {numbers}")
    return matches[0] if matches else None


def _ensure_issue(
    service: GitHubService,
    repository: str,
    issues: list[dict[str, Any]],
    *,
    marker: str,
    title: str,
    body: str,
    labels: list[str] | None = None,
) -> tuple[dict[str, Any], bool]:
    existing = _find_marked(issues, marker)
    if existing:
        return existing, False
    created = service.create_issue(repository, title=title, body=body, labels=labels)
    issues.append(created)
    return created, True


def _parent_matches(issue: dict[str, Any], epic_number: int) -> bool:
    parent = issue.get("parent")
    if isinstance(parent, dict) and int(parent.get("number") or 0) == epic_number:
        return True
    return bool(re.search(rf"(?mi)^\s*(?:Parent\s+Epic|Epic)\s*:\s*#?{epic_number}\s*$", str(issue.get("body") or "")))


def _analysis_path(repo: Path, manifest_path: Path, manifest: dict[str, Any]) -> Path | None:
    analysis = manifest.get("analysis")
    if isinstance(analysis, dict) and analysis.get("path"):
        return _path(repo, str(analysis["path"]))
    candidate = manifest_path.parent / "ANALYSIS.md"
    return candidate if candidate.is_file() else None


def _validate_local_contracts(repo: Path, manifest_path: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    strategy = _strategy(repo, manifest)
    epic_text = _read_contract(repo, manifest["epic"]["path"], "Epic")
    task_texts: dict[str, str] = {}
    for task in manifest["tasks"]:
        task_texts[str(task["id"])] = _read_contract(repo, task["path"], f"task {task['id']}")
    analysis_path = _analysis_path(repo, manifest_path, manifest)
    analysis_text = None
    if manifest.get("execution_mode") == "task-execution":
        if analysis_path is None and manifest.get("source", {}).get("kind") != "github":
            raise ValidationError("Analyst task-execution manifest requires ANALYSIS.md")
        if analysis_path is not None:
            analysis_text = _read_contract(repo, analysis_path, "Analysis")
    return {
        "strategy": strategy,
        "epic_text": epic_text,
        "task_texts": task_texts,
        "analysis_path": analysis_path,
        "analysis_text": analysis_text,
    }


def _validate_existing_mapping(repo: Path, manifest: dict[str, Any], service: GitHubService) -> None:
    source = manifest.get("source") or {}
    repository = str(source.get("repository") or "")
    epic_number = source.get("epic_issue") or manifest.get("epic", {}).get("issue")
    if not repository or epic_number is None:
        raise ValidationError("GitHub-backed local manifest requires repository and Epic Issue identity")
    epic_number = int(epic_number)
    epic_issue = service.issue(IssueRef(repository, epic_number))
    _read_contract(repo, manifest["epic"]["path"], "Epic")
    origin = str(source.get("origin") or source.get("kind") or "github")
    if origin == "local":
        epic_marker = _marker("epic", str(manifest.get("epic", {}).get("id") or "EPIC"))
        if epic_marker not in str(epic_issue.get("body") or ""):
            raise ValidationError(f"Epic #{epic_number} does not carry the expected local contract marker")
    for task in manifest["tasks"]:
        _read_contract(repo, task["path"], f"task {task['id']}")
        if task.get("id") == "DIRECT-PLAN":
            continue
        if task.get("issue") is None:
            raise ValidationError(f"task {task['id']} is missing GitHub Issue identity")
        issue_number = int(task["issue"])
        issue = service.issue(IssueRef(repository, issue_number))
        if not _parent_matches(issue, epic_number):
            raise ValidationError(f"task {task['id']} Issue #{issue_number} is not linked to Epic #{epic_number}")
        if origin == "local":
            task_marker = _marker("task", str(task["id"]))
            if task_marker not in str(issue.get("body") or ""):
                raise ValidationError(f"task {task['id']} Issue #{issue_number} does not carry the expected contract marker")
        elif str(task["id"]) != f"TASK-{issue_number}":
            raise ValidationError(f"GitHub-origin task {task['id']} does not match Issue #{issue_number}")


def initialize_local_manifest(
    repo: Path,
    manifest_path: Path,
    *,
    dry_run: bool = False,
    service: GitHubService | None = None,
    github_project: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Create the missing GitHub representation once, then freeze the local snapshot."""
    repo = repo.resolve()
    manifest_path = manifest_path.resolve()
    try:
        manifest = validate_manifest(json.loads(manifest_path.read_text(encoding="utf-8")))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValidationError(f"invalid manifest JSON: {exc}") from exc

    source = manifest.setdefault("source", {})
    if not isinstance(manifest.get("execution_strategy"), dict):
        try:
            manifest["execution_strategy"] = _strategy(repo, manifest)
        except ValidationError as exc:
            if source.get("kind") == "local" and "missing required '## Execution Strategy' contract" in str(exc):
                source.setdefault("origin", "local")
                source.setdefault("tracking", "local")
                source.setdefault("initial_materialization_complete", True)
                return {"changed": False, "required": False, "tracking": "local", "legacy": True, "manifest": manifest}
            raise

    local = _validate_local_contracts(repo, manifest_path, manifest)
    strategy = local["strategy"]
    manifest["execution_strategy"] = strategy
    tracking = _tracking(strategy)
    source.setdefault("origin", source.get("kind", "local"))
    source["tracking"] = tracking

    if tracking == "local":
        source["initial_materialization_complete"] = True
        return {"changed": False, "required": False, "tracking": "local", "manifest": manifest}

    if dry_run:
        return {
            "changed": False,
            "required": source.get("kind") != "github",
            "tracking": "github",
            "performed": False,
            "manifest": manifest,
        }

    service = service or GitHubService(repo)
    if source.get("kind") == "github":
        _validate_existing_mapping(repo, manifest, service)
        changed = not bool(source.get("initial_materialization_complete"))
        source["initial_materialization_complete"] = True
        source.setdefault("initialized_at", now())
        if changed:
            _atomic_json(manifest_path, manifest)
        return {"changed": changed, "required": False, "tracking": "github", "performed": False, "manifest": manifest}

    repository = str(source.get("repository") or service.repository_name())
    issues = service.list_issues(repository)
    epic_id = str(manifest.get("epic", {}).get("id") or "EPIC")
    epic_marker = _marker("epic", epic_id)
    epic_body = _with_marker(local["epic_text"], epic_marker)
    epic, epic_created = _ensure_issue(
        service,
        repository,
        issues,
        marker=epic_marker,
        title=_title(local["epic_text"], epic_id),
        body=epic_body,
        labels=[EPIC_LABEL],
    )
    epic_number = int(epic["number"])
    epic_ref = IssueRef(repository, epic_number)

    analysis_created = False
    if local["analysis_text"] is not None:
        before = service.existing_comment_with_marker(epic_ref, ANALYSIS_MARKER)
        service.ensure_issue_comment(epic_ref, ANALYSIS_MARKER, str(local["analysis_text"]).strip())
        analysis_created = before is None

    created_tasks: list[int] = []
    recovered_tasks: list[int] = []
    for task in manifest["tasks"]:
        if task.get("id") == "DIRECT-PLAN":
            task["issue"] = None
            continue
        task_id = str(task["id"])
        task_marker = _marker("task", task_id)
        task_body = _with_marker(local["task_texts"][task_id], task_marker, parent_epic=epic_number)
        issue, created = _ensure_issue(
            service,
            repository,
            issues,
            marker=task_marker,
            title=_title(local["task_texts"][task_id], task_id),
            body=task_body,
        )
        issue_number = int(issue["number"])
        if not created and not _parent_matches(issue, epic_number):
            raise ValidationError(f"recovered task marker {task_marker} is not linked to Epic #{epic_number}")
        if created:
            created_tasks.append(issue_number)
            service.try_add_sub_issue(repository, epic_number, issue_number)
        else:
            recovered_tasks.append(issue_number)
        task["issue"] = issue_number

    project_items: list[int] = []
    if github_project and github_project.get("owner") and github_project.get("number"):
        for issue in [epic, *(item for item in issues if int(item.get("number") or 0) in created_tasks + recovered_tasks)]:
            url = issue.get("url") or issue.get("html_url")
            if url:
                service.ensure_project_item(
                    str(github_project["owner"]),
                    int(github_project["number"]),
                    str(url),
                    int(issue["number"]),
                )
                project_items.append(int(issue["number"]))

    source.update({
        "kind": "github",
        "origin": "local",
        "tracking": "github",
        "repository": repository,
        "epic_issue": epic_number,
        "initialized_at": now(),
        "initial_materialization_complete": True,
    })
    manifest["epic"]["issue"] = epic_number
    if local["analysis_path"] is not None:
        manifest["analysis"] = {
            "path": str(local["analysis_path"].relative_to(repo)).replace("\\", "/")
            if local["analysis_path"].is_relative_to(repo)
            else str(local["analysis_path"]),
            "marker": ANALYSIS_MARKER,
        }
    manifest = validate_manifest(manifest)
    _atomic_json(manifest_path, manifest)
    return {
        "changed": True,
        "required": True,
        "tracking": "github",
        "performed": True,
        "repository": repository,
        "epic_issue": epic_number,
        "created_epic": epic_created,
        "created_analysis": analysis_created,
        "created_tasks": created_tasks,
        "recovered_tasks": recovered_tasks,
        "project_items": project_items,
        "manifest": manifest,
    }


def initialize_local_spec(
    repo: Path,
    spec_path: Path,
    *,
    dry_run: bool = False,
    service: GitHubService | None = None,
    github_project: dict[str, Any] | None = None,
) -> tuple[Path | None, dict[str, Any] | None]:
    """Convert a GitHub-tracked direct plan into a standard local manifest, then initialize it."""
    text = _read_contract(repo, spec_path, "Specification")
    try:
        strategy = parse_execution_strategy(text)
    except ValidationError:
        return None, None
    if _tracking(strategy) != "github":
        return None, None
    if strategy.get("decomposition", "").strip().lower() != "none":
        raise ValidationError("--spec direct-plan input requires Execution Strategy decomposition: none")
    manifest = direct_plan_manifest(spec_path)
    manifest["execution_strategy"] = strategy
    if dry_run:
        return None, {"changed": False, "required": True, "tracking": "github", "performed": False, "manifest": manifest}

    digest = sha256_text(text).split(":", 1)[1][:12]
    root = repo / ".agent" / "work-items" / f"direct-plan-{digest}"
    epic_path = root / "EPIC.md"
    manifest_path = root / "manifest.json"
    _atomic_text(epic_path, text.rstrip() + "\n")
    relative = str(epic_path.relative_to(repo)).replace("\\", "/")
    manifest["epic"]["path"] = relative
    manifest["tasks"][0]["path"] = relative
    _atomic_json(manifest_path, manifest)
    result = initialize_local_manifest(
        repo,
        manifest_path,
        dry_run=False,
        service=service,
        github_project=github_project,
    )
    return manifest_path, result


def _option_value(argv: list[str], name: str) -> str | None:
    for index, token in enumerate(argv):
        if token == name and index + 1 < len(argv):
            return argv[index + 1]
        if token.startswith(name + "="):
            return token.split("=", 1)[1]
    return None


def _replace_option(argv: list[str], old: str, new: str, value: str) -> list[str]:
    result = list(argv)
    for index, token in enumerate(result):
        if token == old and index + 1 < len(result):
            result[index : index + 2] = [new, value]
            return result
        if token.startswith(old + "="):
            result[index] = f"{new}={value}"
            return result
    return result


def _github_project_config(repo: Path, argv: list[str]) -> dict[str, Any] | None:
    requested = _option_value(argv, "--config")
    path = Path(requested).resolve() if requested else repo / ".project-orchestrator" / "config.json"
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    project = value.get("github_project")
    return project if isinstance(project, dict) else None


def prepare_cli_args(argv: list[str], *, cwd: Path | None = None) -> list[str]:
    """Perform contract-store initialization before the public CLI creates a run/worktree."""
    raw = list(argv)
    if "run" not in raw:
        return raw
    cwd = (cwd or Path.cwd()).resolve()
    repo_value = _option_value(raw, "--repo")
    repo = Path(repo_value).resolve() if repo_value else cwd
    dry_run = "--dry-run" in raw
    project = _github_project_config(repo, raw)

    manifest_value = _option_value(raw, "--manifest")
    if manifest_value:
        result = initialize_local_manifest(
            repo,
            Path(manifest_value).resolve(),
            dry_run=dry_run,
            github_project=project,
        )
        if dry_run and result.get("required"):
            print("contract store initialization required; dry-run made no GitHub changes", file=os.sys.stderr)
        return raw

    spec_value = _option_value(raw, "--spec")
    if spec_value:
        manifest_path, result = initialize_local_spec(
            repo,
            Path(spec_value).resolve(),
            dry_run=dry_run,
            github_project=project,
        )
        if dry_run and result and result.get("required"):
            print("contract store initialization required; dry-run made no GitHub changes", file=os.sys.stderr)
        if manifest_path is not None:
            return _replace_option(raw, "--spec", "--manifest", str(manifest_path))
    return raw
