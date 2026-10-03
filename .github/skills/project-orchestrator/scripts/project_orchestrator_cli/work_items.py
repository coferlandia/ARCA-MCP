"""Validated v2 execution-manifest and work-item contracts."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from typing import Any

from .contracts import ValidationError

EXECUTION_MODES = {"direct-plan", "task-execution"}
STRATEGY_RE = re.compile(r"(?ms)^##\s+Execution Strategy\s*$\n(?P<body>.*?)(?=^##\s+|\Z)")
STRATEGY_FIELD_RE = re.compile(r"(?mi)^\s*(Tracking|Decomposition|Execution|Worker profile|Review|Integration)\s*:\s*(.+?)\s*$")
ARCHITECTURE_GATE_RE = re.compile(r"(?ms)^##\s+Architecture Gate\s*$\n(?P<body>.*?)(?=^##\s+|\Z)")
ARCHITECTURE_GATE_FIELD_RE = re.compile(
    r"(?mi)^\s*(Mode|Status|Assessment reference|Addendum updated|Blocker)\s*:\s*(.*?)\s*$"
)
ARCHITECTURE_GATE_MODES = {"the-architect", "none"}
ARCHITECTURE_GATE_STATUSES = {"required", "passed", "blocked", "not-required"}


def sha256_text(text: str) -> str:
    return "sha256:" + hashlib.sha256(text.encode("utf-8")).hexdigest()


def parse_execution_strategy(text: str) -> dict[str, str]:
    match = STRATEGY_RE.search(text)
    if not match:
        raise ValidationError("missing required '## Execution Strategy' contract")
    fields = {m.group(1).lower().replace(" ", "_"): m.group(2).strip() for m in STRATEGY_FIELD_RE.finditer(match.group("body"))}
    required = {"tracking", "decomposition", "execution", "worker_profile", "review", "integration"}
    missing = sorted(required - set(fields))
    if missing:
        raise ValidationError(f"Execution Strategy is incomplete: missing {', '.join(missing)}")
    ambiguous = sorted(key for key, value in fields.items() if "|" in value or not value.strip())
    if ambiguous:
        raise ValidationError(f"Execution Strategy is not resolved: ambiguous {', '.join(ambiguous)}")
    return fields


def parse_architecture_gate(text: str) -> dict[str, str] | None:
    """Parse an optional Architecture Gate without treating absence as an error."""
    match = ARCHITECTURE_GATE_RE.search(text)
    if not match:
        return None
    fields = {
        item.group(1).lower().replace(" ", "_"): item.group(2).strip()
        for item in ARCHITECTURE_GATE_FIELD_RE.finditer(match.group("body"))
    }
    missing = sorted({"mode", "status"} - set(fields))
    if missing:
        raise ValidationError(f"Architecture Gate is incomplete: missing {', '.join(missing)}")
    mode = fields["mode"].lower()
    status = fields["status"].lower()
    if "|" in mode or "|" in status:
        raise ValidationError("Architecture Gate is not resolved")
    if mode not in ARCHITECTURE_GATE_MODES:
        raise ValidationError(f"unsupported Architecture Gate mode: {fields['mode']}")
    if status not in ARCHITECTURE_GATE_STATUSES:
        raise ValidationError(f"unsupported Architecture Gate status: {fields['status']}")
    fields["mode"] = mode
    fields["status"] = status
    fields.setdefault("assessment_reference", "none")
    fields.setdefault("addendum_updated", "none")
    fields.setdefault("blocker", "none")
    return fields


def validate_architecture_gate(text: str) -> dict[str, str]:
    """Return normalized gate metadata or block unresolved Architect-gated work."""
    gate = parse_architecture_gate(text)
    if gate is None:
        return {
            "mode": "none",
            "status": "not-required",
            "assessment_reference": "none",
            "addendum_updated": "none",
            "blocker": "none",
        }
    if gate["mode"] == "none":
        if gate["status"] != "not-required":
            raise ValidationError(
                f"Architecture Gate is inconsistent: mode=none requires status=not-required, got {gate['status']}"
            )
        return gate
    if gate["status"] == "not-required":
        raise ValidationError("Architecture Gate is inconsistent: mode=the-architect cannot be not-required")
    if gate["status"] != "passed":
        blocker = gate.get("blocker") or "Architecture Preflight has not passed"
        raise ValidationError(
            f"Architecture Gate blocks execution: status={gate['status']}; blocker={blocker}"
        )
    return gate


def architecture_gate_from_manifest(value: dict[str, Any]) -> dict[str, str]:
    """Validate the Epic/spec contract before any branch or worktree is created."""
    explicit = value.get("architecture_gate")
    if isinstance(explicit, dict):
        mode = str(explicit.get("mode") or "none").strip().lower()
        status = str(explicit.get("status") or "not-required").strip().lower()
        synthetic = (
            "## Architecture Gate\n\n"
            f"Mode: {mode}\nStatus: {status}\n"
            f"Assessment reference: {explicit.get('assessment_reference', 'none')}\n"
            f"Addendum updated: {explicit.get('addendum_updated', 'none')}\n"
            f"Blocker: {explicit.get('blocker', 'none')}\n"
        )
        return validate_architecture_gate(synthetic)
    raw = str(value.get("epic", {}).get("path") or "").strip()
    if not raw:
        return validate_architecture_gate("")
    path = Path(raw)
    if not path.is_file():
        return validate_architecture_gate("")
    return validate_architecture_gate(path.read_text(encoding="utf-8"))


def _resolve_manifest_contract_path(manifest_path: Path, raw_path: str) -> Path | None:
    candidate = Path(raw_path)
    if candidate.is_absolute():
        return candidate if candidate.is_file() else None
    seen: set[Path] = set()
    for root in (manifest_path.parent, *manifest_path.parents):
        resolved = (root / candidate).resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        if resolved.is_file():
            return resolved
    return None


def tracking_mode_from_strategy(strategy: dict[str, str]) -> str:
    tracking = strategy["tracking"].strip().lower()
    if tracking == "github":
        return "github"
    if tracking in {"local", "local fallback", "local-fallback"}:
        return "local"
    raise ValidationError(f"unsupported Execution Strategy tracking: {strategy['tracking']}")


def execution_mode_from_strategy(strategy: dict[str, str]) -> str:
    decomposition = strategy["decomposition"].strip().lower()
    if decomposition == "analyst":
        return "task-execution"
    if decomposition == "none":
        return "direct-plan"
    raise ValidationError(f"unsupported Execution Strategy decomposition: {strategy['decomposition']}")


def direct_plan_manifest(spec: Path) -> dict[str, Any]:
    if spec.suffix.lower() not in {".md", ".txt"}:
        raise ValidationError("specification must be .md or .txt")
    if not spec.is_file():
        raise ValidationError(f"specification not found: {spec}")
    text = spec.read_text(encoding="utf-8")
    if not text.strip():
        raise ValidationError("specification is empty")
    source_hash = sha256_text(text)
    value: dict[str, Any] = {
        "schema_version": 2,
        "execution_mode": "direct-plan",
        "source": {
            "kind": "local",
            "origin": "local",
            "tracking": "local",
            "path": str(spec.resolve()),
            "source_hash": source_hash,
            "contract_revision": 1,
            "initial_materialization_complete": True,
        },
        "epic": {
            "id": "DIRECT-PLAN",
            "title": spec.stem,
            "path": str(spec.resolve()),
        },
        "tasks": [{
            "id": "DIRECT-PLAN",
            "issue": None,
            "title": spec.stem,
            "depends_on": [],
            "status": "pending",
            "path": str(spec.resolve()),
            "contract_revision": 1,
            "source_hash": source_hash,
            "commits": [],
        }],
        "architecture_gate": validate_architecture_gate(text),
        "final_pr": None,
        "squash_sha": None,
    }
    try:
        strategy = parse_execution_strategy(text)
    except ValidationError:
        strategy = None
    if strategy is not None:
        value["execution_strategy"] = strategy
        value["source"]["tracking"] = tracking_mode_from_strategy(strategy)
        value["source"]["initial_materialization_complete"] = value["source"]["tracking"] == "local"
    return validate_manifest(value)


def load_manifest(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise ValidationError(f"manifest not found: {path}")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValidationError(f"invalid manifest JSON: {exc}") from exc
    raw_epic_path = str(value.get("epic", {}).get("path") or "").strip()
    if raw_epic_path:
        epic_path = _resolve_manifest_contract_path(path.resolve(), raw_epic_path)
        if epic_path is not None:
            value["architecture_gate"] = validate_architecture_gate(epic_path.read_text(encoding="utf-8"))
    return validate_manifest(value)


def validate_manifest(value: dict[str, Any]) -> dict[str, Any]:
    if value.get("schema_version") != 2:
        raise ValidationError("execution manifest schema_version must be 2")
    mode = value.get("execution_mode")
    if mode not in EXECUTION_MODES:
        raise ValidationError(f"unsupported execution_mode: {mode}")
    if not isinstance(value.get("source"), dict) or not isinstance(value.get("epic"), dict):
        raise ValidationError("manifest requires source and epic objects")

    source = value["source"]
    kind = str(source.get("kind") or "").strip().lower()
    if kind not in {"local", "github"}:
        raise ValidationError(f"unsupported manifest source kind: {source.get('kind')}")
    source["kind"] = kind
    source.setdefault("origin", kind)
    if source["origin"] not in {"local", "github"}:
        raise ValidationError(f"unsupported manifest source origin: {source['origin']}")

    strategy = value.get("execution_strategy")
    if strategy is not None:
        if not isinstance(strategy, dict):
            raise ValidationError("execution_strategy must be an object")
        normalized_strategy = {str(key): str(item) for key, item in strategy.items()}
        if "tracking" not in normalized_strategy:
            raise ValidationError("execution_strategy is missing tracking")
        tracking = tracking_mode_from_strategy(normalized_strategy)
    else:
        tracking = "github" if kind == "github" else str(source.get("tracking") or "local").lower()
    if tracking not in {"local", "github"}:
        raise ValidationError(f"unsupported manifest tracking mode: {tracking}")
    source["tracking"] = tracking
    source.setdefault("initial_materialization_complete", kind == "github" or tracking == "local")

    tasks = value.get("tasks")
    if not isinstance(tasks, list) or not tasks:
        raise ValidationError("manifest requires at least one task/execution unit")
    if mode == "direct-plan" and len(tasks) != 1:
        raise ValidationError("direct-plan mode must contain exactly one execution unit")

    ids: list[str] = []
    for task in tasks:
        if not isinstance(task, dict):
            raise ValidationError("each manifest task must be an object")
        task_id = str(task.get("id") or "").strip()
        if not task_id:
            raise ValidationError("task id is required")
        if task_id in ids:
            raise ValidationError(f"duplicate task id: {task_id}")
        ids.append(task_id)
        if not task.get("path"):
            raise ValidationError(f"task {task_id} requires a contract path")
        task.setdefault("depends_on", [])
        task.setdefault("status", "pending")
        task.setdefault("commits", [])
        task.setdefault("contract_revision", 1)

    id_set = set(ids)
    for task in tasks:
        task_id = task["id"]
        deps = task.get("depends_on") or []
        if not isinstance(deps, list):
            raise ValidationError(f"task {task_id} depends_on must be an array")
        for dep in deps:
            if dep == task_id:
                raise ValidationError(f"task {task_id} cannot depend on itself")
            if dep not in id_set:
                raise ValidationError(f"task {task_id} references missing dependency: {dep}")

    value["architecture_gate"] = architecture_gate_from_manifest(value)
    value["execution_order"] = topological_order(tasks)
    value.setdefault("final_pr", None)
    value.setdefault("squash_sha", None)
    return value


def topological_order(tasks: list[dict[str, Any]]) -> list[str]:
    by_id = {task["id"]: task for task in tasks}
    remaining = {task_id: set(task.get("depends_on") or []) for task_id, task in by_id.items()}
    order: list[str] = []
    while remaining:
        ready = sorted(task_id for task_id, deps in remaining.items() if not deps)
        if not ready:
            cycle = ", ".join(sorted(remaining))
            raise ValidationError(f"task dependency graph contains a cycle involving: {cycle}")
        for task_id in ready:
            order.append(task_id)
            del remaining[task_id]
            for deps in remaining.values():
                deps.discard(task_id)
    return order


def task_by_id(manifest: dict[str, Any], task_id: str) -> dict[str, Any]:
    for task in manifest["tasks"]:
        if task["id"] == task_id:
            return task
    raise ValidationError(f"unknown task id: {task_id}")


def next_ready_task(manifest: dict[str, Any]) -> dict[str, Any] | None:
    done = {task["id"] for task in manifest["tasks"] if task.get("status") in {"ready_for_merge", "done"}}
    for task_id in manifest.get("execution_order") or topological_order(manifest["tasks"]):
        task = task_by_id(manifest, task_id)
        if task.get("status") in {"ready_for_merge", "done"}:
            continue
        if all(dep in done for dep in task.get("depends_on") or []):
            return task
    return None


def record_commit(task: dict[str, Any], sha: str, kind: str, review_round: int | None = None) -> None:
    entry: dict[str, Any] = {"sha": sha, "kind": kind}
    if review_round is not None:
        entry["review_round"] = review_round
    commits = task.setdefault("commits", [])
    if not any(item.get("sha") == sha for item in commits):
        commits.append(entry)
