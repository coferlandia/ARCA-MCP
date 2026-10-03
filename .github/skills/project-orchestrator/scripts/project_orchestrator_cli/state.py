"""Atomic, lock-protected durable run state for Epic/task orchestration."""
from __future__ import annotations

import json
import os
import tempfile
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from .contracts import UnsafeOperation, ValidationError

TERMINAL = {
    "PROJECT_COMPLETED",
    "CANCELLED",
    "TERMINAL_FAILURE",
    "BLOCKED_BY_SPECIFICATION",
    "BLOCKED_BY_CONFIGURATION",
    "BLOCKED_BY_AUTHENTICATION",
    "BLOCKED_BY_GIT_STATE",
    "BLOCKED_BY_BASE_MOVED",
    "BLOCKED_BY_MERGE_CONFLICT",
    "BLOCKED_BY_NO_PROGRESS",
}

STATES = {
    "INITIALIZED",
    "CONFIG_VALIDATED",
    "CONTRACT_RESOLVED",
    "EPIC_WORKTREE_CREATING",
    "EPIC_WORKTREE_CREATED",
    "TASK_SELECTED",
    "CODING_RUNNING",
    "CODING_REPORTED",
    "CODING_INCOMPLETE",
    "COMPLETION_VERIFYING",
    "CANDIDATE_PREPARING",
    "CANDIDATE_COMMITTED",
    "REVIEW_WORKTREE_CREATING",
    "REVIEW_WORKTREE_CREATED",
    "REVIEW_RUNNING",
    "FIXES_REQUIRED",
    "FIXING",
    "FIX_COMMIT_PREPARING",
    "REVIEW_PASSED",
    "TASK_READY_FOR_MERGE",
    "HOLISTIC_REVIEW_WORKTREE_CREATING",
    "HOLISTIC_REVIEW_WORKTREE_CREATED",
    "HOLISTIC_REVIEW_RUNNING",
    "HOLISTIC_FIXES_REQUIRED",
    "HOLISTIC_FIXING",
    "HOLISTIC_FIX_COMMIT_PREPARING",
    "EPIC_READY_FOR_INTEGRATION",
    "PR_OPEN_AWAITING_MERGE_APPROVAL",
    "WAITING_FOR_INTEGRATION_CHECKS",
    "INTEGRATION_CHECKS_FAILED",
    "INTEGRATING",
    "INTEGRATED",
    "ARCHIVING",
    "WAITING_FOR_PROVIDER",
    *TERMINAL,
}

TRANSITIONS: dict[str, set[str]] = {
    "INITIALIZED": {"CONFIG_VALIDATED", "CANCELLED", "BLOCKED_BY_CONFIGURATION"},
    "CONFIG_VALIDATED": {"CONTRACT_RESOLVED", "BLOCKED_BY_SPECIFICATION", "BLOCKED_BY_AUTHENTICATION", "CANCELLED"},
    "CONTRACT_RESOLVED": {"EPIC_WORKTREE_CREATING", "CANCELLED"},
    "EPIC_WORKTREE_CREATING": {"EPIC_WORKTREE_CREATED", "BLOCKED_BY_GIT_STATE", "CANCELLED"},
    "EPIC_WORKTREE_CREATED": {"TASK_SELECTED", "HOLISTIC_REVIEW_WORKTREE_CREATING", "CANCELLED"},
    "TASK_SELECTED": {"CODING_RUNNING", "CANCELLED"},
    "CODING_RUNNING": {"CODING_REPORTED", "WAITING_FOR_PROVIDER", "CANCELLED", "TERMINAL_FAILURE"},
    "CODING_REPORTED": {"COMPLETION_VERIFYING", "CODING_INCOMPLETE", "BLOCKED_BY_SPECIFICATION"},
    "CODING_INCOMPLETE": {"CODING_RUNNING", "WAITING_FOR_PROVIDER", "BLOCKED_BY_NO_PROGRESS", "CANCELLED"},
    "COMPLETION_VERIFYING": {"CANDIDATE_PREPARING", "CODING_INCOMPLETE", "BLOCKED_BY_SPECIFICATION"},
    "CANDIDATE_PREPARING": {"CANDIDATE_COMMITTED", "BLOCKED_BY_NO_PROGRESS", "TERMINAL_FAILURE"},
    "CANDIDATE_COMMITTED": {"REVIEW_WORKTREE_CREATING", "HOLISTIC_REVIEW_WORKTREE_CREATING", "CANCELLED"},
    "REVIEW_WORKTREE_CREATING": {"REVIEW_WORKTREE_CREATED", "BLOCKED_BY_GIT_STATE"},
    "REVIEW_WORKTREE_CREATED": {"REVIEW_RUNNING", "CANCELLED"},
    "REVIEW_RUNNING": {"REVIEW_PASSED", "FIXES_REQUIRED", "WAITING_FOR_PROVIDER", "CANCELLED", "TERMINAL_FAILURE"},
    "FIXES_REQUIRED": {"FIXING", "CANCELLED"},
    "FIXING": {"FIX_COMMIT_PREPARING", "WAITING_FOR_PROVIDER", "BLOCKED_BY_NO_PROGRESS", "CANCELLED"},
    "FIX_COMMIT_PREPARING": {"CANDIDATE_COMMITTED", "BLOCKED_BY_NO_PROGRESS", "TERMINAL_FAILURE"},
    "REVIEW_PASSED": {"TASK_READY_FOR_MERGE", "CANCELLED"},
    "TASK_READY_FOR_MERGE": {"TASK_SELECTED", "HOLISTIC_REVIEW_WORKTREE_CREATING", "CANCELLED"},
    "HOLISTIC_REVIEW_WORKTREE_CREATING": {"HOLISTIC_REVIEW_WORKTREE_CREATED", "BLOCKED_BY_GIT_STATE"},
    "HOLISTIC_REVIEW_WORKTREE_CREATED": {"HOLISTIC_REVIEW_RUNNING", "CANCELLED"},
    "HOLISTIC_REVIEW_RUNNING": {"EPIC_READY_FOR_INTEGRATION", "HOLISTIC_FIXES_REQUIRED", "WAITING_FOR_PROVIDER", "CANCELLED", "TERMINAL_FAILURE"},
    "HOLISTIC_FIXES_REQUIRED": {"HOLISTIC_FIXING", "CANCELLED"},
    "HOLISTIC_FIXING": {"HOLISTIC_FIX_COMMIT_PREPARING", "WAITING_FOR_PROVIDER", "BLOCKED_BY_NO_PROGRESS", "CANCELLED"},
    "HOLISTIC_FIX_COMMIT_PREPARING": {"CANDIDATE_COMMITTED", "BLOCKED_BY_NO_PROGRESS", "TERMINAL_FAILURE"},
    "EPIC_READY_FOR_INTEGRATION": {"PR_OPEN_AWAITING_MERGE_APPROVAL", "INTEGRATING", "CANCELLED", "BLOCKED_BY_BASE_MOVED"},
    "PR_OPEN_AWAITING_MERGE_APPROVAL": {"WAITING_FOR_INTEGRATION_CHECKS", "INTEGRATION_CHECKS_FAILED", "INTEGRATING", "CANCELLED", "BLOCKED_BY_BASE_MOVED"},
    "WAITING_FOR_INTEGRATION_CHECKS": {"WAITING_FOR_INTEGRATION_CHECKS", "INTEGRATION_CHECKS_FAILED", "INTEGRATING", "CANCELLED", "BLOCKED_BY_BASE_MOVED", "BLOCKED_BY_AUTHENTICATION"},
    "INTEGRATION_CHECKS_FAILED": {"INTEGRATION_CHECKS_FAILED", "WAITING_FOR_INTEGRATION_CHECKS", "INTEGRATING", "CANCELLED", "BLOCKED_BY_BASE_MOVED"},
    "INTEGRATING": {"INTEGRATED", "BLOCKED_BY_MERGE_CONFLICT", "BLOCKED_BY_BASE_MOVED", "TERMINAL_FAILURE"},
    "INTEGRATED": {"ARCHIVING", "PROJECT_COMPLETED", "TERMINAL_FAILURE"},
    "ARCHIVING": {"PROJECT_COMPLETED", "TERMINAL_FAILURE"},
    "WAITING_FOR_PROVIDER": {"CODING_RUNNING", "REVIEW_RUNNING", "FIXING", "HOLISTIC_REVIEW_RUNNING", "HOLISTIC_FIXING", "CANCELLED", "BLOCKED_BY_AUTHENTICATION"},
}


def utcnow() -> str:
    return datetime.now(timezone.utc).isoformat()


class SimulatedClock:
    """Deterministic clock for retry/recovery tests; never sleeps wall-clock time."""

    def __init__(self, start: datetime | None = None):
        self.current = start or datetime.now(timezone.utc)

    def now(self) -> datetime:
        return self.current

    def sleep(self, seconds: float) -> None:
        self.current += timedelta(seconds=seconds)

    def isoformat(self) -> str:
        return self.current.isoformat()


def retry_due(next_retry_at: str, now: datetime) -> bool:
    return now >= datetime.fromisoformat(next_retry_at)


def atomic_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent, text=True)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(value, handle, indent=2, sort_keys=True)
            handle.write("\n")
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


class RunStore:
    def __init__(self, common_dir: Path, run_id: str):
        if not run_id.replace("-", "").replace("_", "").isalnum():
            raise UnsafeOperation("run id contains unsafe characters")
        self.root = common_dir / "project-orchestrator" / "runs" / run_id
        self.state_file = self.root / "run-state.json"

    def load(self) -> dict[str, Any]:
        if not self.state_file.exists():
            raise ValidationError(f"run state not found: {self.root.name}")
        return json.loads(self.state_file.read_text(encoding="utf-8"))

    def create(self, value: dict[str, Any]) -> None:
        if self.state_file.exists():
            raise ValidationError(f"run already exists: {self.root.name}")
        value["schema_version"] = 2
        value["updated_at"] = utcnow()
        value.setdefault("events", [])
        atomic_json(self.state_file, value)

    def lock(self) -> "RunLock":
        return RunLock(self.root.parents[2], self.root.name)

    def transition(self, target: str, detail: dict[str, Any] | None = None) -> dict[str, Any]:
        if target not in STATES:
            raise ValidationError(f"unknown state: {target}")
        value = self.load()
        current = value["state"]
        if current != target and target not in TRANSITIONS.get(current, set()) and not (target == "CANCELLED" and current not in TERMINAL):
            raise ValidationError(f"invalid state transition: {current} -> {target}")
        value["state"] = target
        value["updated_at"] = utcnow()
        value.setdefault("events", []).append({"at": value["updated_at"], "from": current, "to": target, "detail": detail or {}})
        atomic_json(self.state_file, value)
        return value


class RunLock:
    """Cross-platform, exclusive lock for one run's controller operations."""

    def __init__(self, common_dir: Path, run_id: str):
        self.path = common_dir / "project-orchestrator" / "runs" / run_id / ".lock"
        self.owner = f"{os.getpid()}-{uuid.uuid4().hex}"
        self._handle = None

    def acquire(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        try:
            self._handle = self.path.open("x", encoding="utf-8")
        except FileExistsError as exc:
            raise UnsafeOperation(f"run is already locked: {self.path}") from exc
        self._handle.write(json.dumps({"owner": self.owner, "pid": os.getpid(), "created_at": utcnow()}))
        self._handle.flush()

    def release(self) -> None:
        if self._handle is not None:
            self._handle.close()
            self._handle = None
            try:
                self.path.unlink()
            except FileNotFoundError:
                pass

    def __enter__(self) -> "RunLock":
        self.acquire()
        return self

    def __exit__(self, *_: object) -> None:
        self.release()
