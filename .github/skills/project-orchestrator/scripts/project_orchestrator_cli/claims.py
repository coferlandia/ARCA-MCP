"""Durable Epic/task claims and GitHub Project lifecycle projection."""
from __future__ import annotations

import hashlib
import json
import os
import tempfile
import threading
from pathlib import Path
from typing import Any, Callable

from .contracts import UnsafeOperation, ValidationError
from .git_service import GitService
from .github_service import GitHubService, IssueRef
from .state import RunStore, atomic_json, utcnow
from .work_items import direct_plan_manifest, load_manifest, task_by_id


class ClaimConflict(UnsafeOperation):
    """Raised when an execution unit is already owned by another run."""

    def __init__(self, claim_key: str, owner: dict[str, Any]):
        self.claim_key = claim_key
        self.owner = owner
        owner_run = owner.get("run_id", "unknown")
        claimed_at = owner.get("claimed_at", "unknown")
        super().__init__(
            "Execution unit is already claimed.\n\n"
            f"Claim: {claim_key}\n"
            f"Owner run: {owner_run}\n"
            f"Claimed at: {claimed_at}\n\n"
            "Inspect with:\n"
            f"project-orchestrator-cli.py claims inspect {claim_key}"
        )


class ClaimStore:
    """Repository-wide, atomic claim store under the Git common directory."""

    def __init__(self, common_dir: Path):
        self.common_dir = common_dir.resolve()
        self.root = self.common_dir / "project-orchestrator" / "claims"

    @staticmethod
    def digest(claim_key: str) -> str:
        if not claim_key or not isinstance(claim_key, str):
            raise ValidationError("claim key must be a non-empty string")
        return hashlib.sha256(claim_key.encode("utf-8")).hexdigest()

    def path_for(self, claim_key: str) -> Path:
        return self.root / f"{self.digest(claim_key)}.json"

    def _read_path(self, path: Path) -> dict[str, Any]:
        try:
            value = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise ValidationError(f"malformed claim file requires administrative intervention: {path}") from exc
        if not isinstance(value, dict) or value.get("schema_version") != 1:
            raise ValidationError(f"unsupported or malformed claim record: {path}")
        claim_key = value.get("claim_key")
        if not isinstance(claim_key, str) or self.path_for(claim_key) != path:
            raise ValidationError(f"claim key does not match claim filename: {path}")
        if value.get("status") != "active" or not value.get("run_id"):
            raise ValidationError(f"invalid active claim record: {path}")
        return value

    def get(self, claim_key: str) -> dict[str, Any] | None:
        path = self.path_for(claim_key)
        return self._read_path(path) if path.exists() else None

    def acquire(self, record: dict[str, Any]) -> dict[str, Any]:
        claim_key = str(record.get("claim_key") or "")
        run_id = str(record.get("run_id") or "")
        if not run_id:
            raise ValidationError("claim record requires run_id")
        path = self.path_for(claim_key)
        self.root.mkdir(parents=True, exist_ok=True)
        value = {
            **record,
            "schema_version": 1,
            "claim_key": claim_key,
            "run_id": run_id,
            "status": "active",
            "claimed_at": record.get("claimed_at") or utcnow(),
            "updated_at": utcnow(),
        }
        fd, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=self.root, text=True)
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as handle:
                json.dump(value, handle, indent=2, sort_keys=True)
                handle.write("\n")
                handle.flush()
                os.fsync(handle.fileno())
            try:
                os.link(temporary, path)
                return value
            except FileExistsError:
                existing = self._read_path(path)
                if existing.get("run_id") == run_id:
                    return existing
                raise ClaimConflict(claim_key, existing)
        finally:
            try:
                os.unlink(temporary)
            except FileNotFoundError:
                pass

    def update(self, claim_key: str, run_id: str, changes: dict[str, Any]) -> dict[str, Any]:
        value = self.get(claim_key)
        if value is None:
            raise ValidationError(f"claim does not exist: {claim_key}")
        if value.get("run_id") != run_id:
            raise ClaimConflict(claim_key, value)
        value.update(changes)
        value["updated_at"] = utcnow()
        atomic_json(self.path_for(claim_key), value)
        return value

    def list_active(self) -> list[dict[str, Any]]:
        if not self.root.exists():
            return []
        return sorted((self._read_path(path) for path in self.root.glob("*.json")), key=lambda item: item["claim_key"])

    def list_for_run(self, run_id: str) -> list[dict[str, Any]]:
        return [item for item in self.list_active() if item.get("run_id") == run_id]

    def resolve(self, reference: str) -> dict[str, Any]:
        exact = self.get(reference)
        if exact is not None:
            return exact
        matches = [item for item in self.list_active() if self.digest(item["claim_key"]) == reference]
        if not matches:
            raise ValidationError(f"claim not found: {reference}")
        if len(matches) != 1:
            raise ValidationError(f"ambiguous claim reference: {reference}")
        return matches[0]

    def release(self, claim_key: str, run_id: str, reason: str, *, force: bool = False) -> dict[str, Any] | None:
        if not reason.strip():
            raise ValidationError("claim release requires a non-empty reason")
        path = self.path_for(claim_key)
        if not path.exists():
            return None
        value = self._read_path(path)
        if value.get("run_id") != run_id and not force:
            raise ClaimConflict(claim_key, value)
        released_at = utcnow()
        history_root = self.common_dir / "project-orchestrator" / "runs" / str(value["run_id"]) / "claim-history"
        history = {
            "schema_version": 1,
            "released_at": released_at,
            "released_by": "administrative-force" if force else "owning-run",
            "release_reason": reason,
            "previous_record": value,
        }
        history_name = f"{self.digest(claim_key)}-{released_at.replace(':', '').replace('+', '_')}.json"
        atomic_json(history_root / history_name, history)
        try:
            path.unlink()
        except FileNotFoundError:
            pass
        state_file = self.common_dir / "project-orchestrator" / "runs" / str(value["run_id"]) / "run-state.json"
        if state_file.exists():
            try:
                state = json.loads(state_file.read_text(encoding="utf-8"))
                claims = state.setdefault("claims", {"epic": None, "tasks": {}})
                if claims.get("epic", {}).get("claim_key") == claim_key:
                    claims["epic"]["status"] = "released"
                for item in claims.setdefault("tasks", {}).values():
                    if item.get("claim_key") == claim_key:
                        item["status"] = "released"
                state.setdefault("events", []).append({"at": released_at, "type": "CLAIM_FORCE_RELEASED" if force else "CLAIM_RELEASED", "detail": {"claim_key": claim_key, "reason": reason}})
                atomic_json(state_file, state)
            except (OSError, json.JSONDecodeError):
                pass
        return history


def _project_cfg(config: dict[str, Any]) -> dict[str, Any] | None:
    value = config.get("github_project")
    return value if isinstance(value, dict) and value.get("owner") and value.get("number") is not None else None


def _status_name(config: dict[str, Any], logical: str) -> str:
    mapping = {"pending": "Todo", "in_progress": "In Progress", "review": "Review", "blocked": "Blocked", "done": "Done"}
    mapping.update(config.get("github_project", {}).get("status_mapping", {}))
    return str(mapping[logical])


def _github_identity(manifest: dict[str, Any]) -> tuple[str, int] | None:
    source = manifest.get("source", {})
    repository = source.get("repository")
    epic_issue = source.get("epic_issue")
    if source.get("kind") == "github" and repository and epic_issue is not None:
        return str(repository), int(epic_issue)
    return None


def _source_digest(path: Path) -> str:
    return hashlib.sha256(path.resolve().as_posix().encode("utf-8") + b"\0" + path.read_bytes()).hexdigest()


def _epic_key_from_manifest(manifest: dict[str, Any], source_path: Path | None = None) -> str:
    github = _github_identity(manifest)
    if github:
        return f"epic:github:{github[0]}#{github[1]}"
    source = manifest.get("source", {})
    epic_id = str(manifest.get("epic", {}).get("id") or "EPIC")
    identity = source.get("source_hash") or source.get("contract_revision")
    if source_path is not None and source_path.exists():
        identity = _source_digest(source_path)
    identity = str(identity or hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest())
    kind = "direct-plan" if manifest.get("execution_mode") == "direct-plan" else "local"
    return f"epic:{kind}:{identity}:{epic_id}"


def _task_key(state: dict[str, Any], task: dict[str, Any]) -> str:
    github = _github_identity(state["manifest"])
    if github and task.get("issue") is not None:
        return f"task:github:{github[0]}#{int(task['issue'])}"
    epic_key = state.get("claims", {}).get("epic", {}).get("claim_key") or _epic_key_from_manifest(state["manifest"])
    return f"task:local:{ClaimStore.digest(str(epic_key))}:{task['id']}"


def _record_event(store: RunStore, event_type: str, detail: dict[str, Any]) -> dict[str, Any]:
    state = store.load()
    state.setdefault("events", []).append({"at": utcnow(), "type": event_type, "detail": detail})
    atomic_json(store.state_file, state)
    return state


def _record_claim(store: RunStore, scope: str, record: dict[str, Any], task_id: str | None = None) -> dict[str, Any]:
    state = store.load()
    claims = state.setdefault("claims", {"epic": None, "tasks": {}})
    claims.setdefault("tasks", {})
    summary = {"claim_key": record["claim_key"], "status": "active", "claimed_at": record["claimed_at"]}
    if scope == "epic":
        claims["epic"] = summary
    else:
        assert task_id is not None
        claims["tasks"][task_id] = summary
    atomic_json(store.state_file, state)
    return state


def _previous_project_status(service: GitHubService, owner: str, number: int, issue_number: int) -> str | None:
    for item in service.project_items(owner, number):
        content = item.get("content") or {}
        if content.get("number") != issue_number:
            continue
        for key in ("status", "Status"):
            value = item.get(key)
            if isinstance(value, str) and value:
                return value
            if isinstance(value, dict) and value.get("name"):
                return str(value["name"])
    return None


def _project_claim(repo: Path, config: dict[str, Any], claim_store: ClaimStore, record: dict[str, Any], repository: str, issue_number: int, logical: str) -> dict[str, Any]:
    project = _project_cfg(config)
    if not project:
        return claim_store.update(record["claim_key"], record["run_id"], {"project_projection": {"configured": False, "applied": False, "status": "skipped", "updated_at": utcnow()}})
    current = record.get("project_projection") or {}
    target = _status_name(config, logical)
    if current.get("applied") and current.get("current_status") == target:
        return record
    service = GitHubService(repo)
    owner = str(project["owner"])
    number = int(project["number"])
    previous = current.get("previous_status") or _previous_project_status(service, owner, number, issue_number)
    try:
        issue = service.issue(IssueRef(repository, issue_number))
        service.set_project_status(owner, number, issue, target)
    except Exception as exc:
        claim_store.update(record["claim_key"], record["run_id"], {"project_projection": {"configured": True, "applied": False, "status": "failed", "previous_status": previous, "current_status": None, "error": str(exc), "updated_at": utcnow()}})
        raise
    return claim_store.update(record["claim_key"], record["run_id"], {"project_projection": {"configured": True, "applied": True, "status": "applied", "previous_status": previous, "current_status": target, "error": None, "updated_at": utcnow()}})


def _claim_record(*, claim_key: str, scope: str, run_id: str, manifest: dict[str, Any], task: dict[str, Any] | None = None, worktree: str | None = None) -> dict[str, Any]:
    github = _github_identity(manifest)
    return {
        "claim_key": claim_key,
        "scope": scope,
        "source_type": "github_issue" if github else "local_contract",
        "repository": github[0] if github else None,
        "epic_id": str(manifest.get("epic", {}).get("id") or ""),
        "task_id": str(task.get("id")) if task else None,
        "issue_number": int(task["issue"]) if task and task.get("issue") is not None else (github[1] if github and scope == "epic" else None),
        "run_id": run_id,
        "worktree": worktree,
        "project_projection": {"configured": False, "applied": False, "status": "pending"},
    }


def _manifest_before_prepare(repo: Path, spec: Path | None, epic: str | None, manifest_path: Path | None) -> tuple[dict[str, Any], Path | None]:
    if spec is not None:
        return direct_plan_manifest(spec), spec
    if manifest_path is not None:
        return load_manifest(manifest_path), manifest_path
    if epic is None:
        raise ValidationError("exactly one execution source is required")
    ref = GitHubService(repo).resolve_issue_ref(epic)
    return {
        "execution_mode": "task-execution",
        "source": {"kind": "github", "repository": ref.repository, "epic_issue": ref.number},
        "epic": {"id": f"EPIC-{ref.number}"},
        "tasks": [],
    }, None


def prepare_claimed_run(repo: Path, spec: Path | None, config: dict[str, Any], run_id: str, dry_run: bool, base_override: str | None = None, *, epic: str | None = None, manifest_path: Path | None = None) -> dict[str, Any]:
    from . import engine

    if dry_run:
        return engine.prepare_run(repo, spec, config, run_id, dry_run, base_override, epic=epic, manifest_path=manifest_path)
    git = GitService(repo)
    git.ensure_repo()
    common_dir = git.common_dir()
    claim_store = ClaimStore(common_dir)
    manifest, identity_path = _manifest_before_prepare(repo, spec, epic, manifest_path)
    claim_key = _epic_key_from_manifest(manifest, identity_path)
    record = claim_store.acquire(_claim_record(claim_key=claim_key, scope="epic", run_id=run_id, manifest=manifest))
    github = _github_identity(manifest)
    if github:
        record = _project_claim(repo, config, claim_store, record, github[0], github[1], "in_progress")
    run_store = RunStore(common_dir, run_id)
    try:
        result = engine.prepare_run(repo, spec, config, run_id, False, base_override, epic=epic, manifest_path=manifest_path)
    except Exception:
        if not run_store.state_file.exists():
            try:
                _restore_project(repo, config, record)
            finally:
                claim_store.release(claim_key, run_id, "run preparation failed before durable run creation")
        raise
    state = run_store.load()
    worktree = state.get("resources", {}).get("implementation_worktree")
    record = claim_store.update(claim_key, run_id, {"worktree": worktree})
    _record_claim(run_store, "epic", record)
    _record_event(run_store, "EPIC_CLAIM_ACQUIRED", {"claim_key": claim_key})
    return result


def ensure_epic_claim(repo: Path, state: dict[str, Any], config: dict[str, Any]) -> dict[str, Any]:
    git = GitService(repo)
    store = RunStore(git.common_dir(), state["run_id"])
    claim_store = ClaimStore(git.common_dir())
    claim_key = state.get("claims", {}).get("epic", {}).get("claim_key") or _epic_key_from_manifest(state["manifest"])
    record = claim_store.acquire(_claim_record(claim_key=claim_key, scope="epic", run_id=state["run_id"], manifest=state["manifest"], worktree=state.get("resources", {}).get("implementation_worktree")))
    github = _github_identity(state["manifest"])
    if github:
        record = _project_claim(repo, config, claim_store, record, github[0], github[1], "in_progress")
    _record_claim(store, "epic", record)
    return record


def ensure_task_claim(repo: Path, state: dict[str, Any], config: dict[str, Any]) -> dict[str, Any]:
    task = task_by_id(state["manifest"], state["current_task_id"])
    git = GitService(repo)
    store = RunStore(git.common_dir(), state["run_id"])
    claim_store = ClaimStore(git.common_dir())
    claim_key = _task_key(state, task)
    record = claim_store.acquire(_claim_record(claim_key=claim_key, scope="task", run_id=state["run_id"], manifest=state["manifest"], task=task, worktree=state.get("resources", {}).get("implementation_worktree")))
    github = _github_identity(state["manifest"])
    if github and task.get("issue") is not None:
        record = _project_claim(repo, config, claim_store, record, github[0], int(task["issue"]), "in_progress")
    _record_claim(store, "task", record, task["id"])
    _record_event(store, "TASK_CLAIM_ACQUIRED", {"claim_key": claim_key, "task": task["id"]})
    return record


_INVOKE_PATCH_LOCK = threading.RLock()


def execute_claimed_run(repo: Path, run_id: str, config: dict[str, Any]) -> dict[str, Any]:
    from . import engine

    git = GitService(repo)
    state = RunStore(git.common_dir(), run_id).load()
    ensure_epic_claim(repo, state, config)
    with _INVOKE_PATCH_LOCK:
        original: Callable[..., dict[str, Any]] = engine._invoke

        def guarded_invoke(store: RunStore, current: dict[str, Any], cfg: dict[str, Any], work_item: dict[str, Any], role: str, worktree: Path, prompt: str, candidate: str | None = None) -> dict[str, Any]:
            if role == "coding-agent":
                latest = store.load()
                ensure_epic_claim(repo, latest, cfg)
                ensure_task_claim(repo, latest, cfg)
            return original(store, current, cfg, work_item, role, worktree, prompt, candidate)

        engine._invoke = guarded_invoke
        try:
            return engine.execute_run(repo, run_id, config)
        finally:
            engine._invoke = original


def _project_all(repo: Path, state: dict[str, Any], config: dict[str, Any], logical: str) -> None:
    github = _github_identity(state["manifest"])
    if not github or not _project_cfg(config):
        return
    service = GitHubService(repo)
    owner = str(config["github_project"]["owner"])
    number = int(config["github_project"]["number"])
    target = _status_name(config, logical)
    issue_numbers = [github[1], *[int(task["issue"]) for task in state["manifest"].get("tasks", []) if task.get("issue") is not None]]
    for issue_number in issue_numbers:
        issue = service.issue(IssueRef(github[0], issue_number))
        service.set_project_status(owner, number, issue, target)


def prepare_claimed_final_pr(repo: Path, run_id: str, config: dict[str, Any]) -> dict[str, Any]:
    from .integration import prepare_final_pr

    state = prepare_final_pr(repo, run_id, config)
    _project_all(repo, state, config, "in_progress")
    return state


def _restore_project(repo: Path, config: dict[str, Any], record: dict[str, Any]) -> str | None:
    projection = record.get("project_projection") or {}
    if not projection.get("configured") or not projection.get("applied"):
        return None
    repository = record.get("repository")
    issue_number = record.get("issue_number")
    project = _project_cfg(config)
    if not repository or issue_number is None or not project:
        return None
    service = GitHubService(repo)
    owner = str(project["owner"])
    number = int(project["number"])
    current = _previous_project_status(service, owner, number, int(issue_number))
    applied = projection.get("current_status")
    if current and applied and current.lower() != str(applied).lower():
        return f"skipped: externally changed to {current}"
    target = projection.get("previous_status") or _status_name(config, "pending")
    issue = service.issue(IssueRef(str(repository), int(issue_number)))
    service.set_project_status(owner, number, issue, str(target))
    return str(target)


def release_run_claims(repo: Path, run_id: str, config: dict[str, Any], reason: str, *, restore_project: bool = False) -> list[str]:
    git = GitService(repo)
    claim_store = ClaimStore(git.common_dir())
    warnings: list[str] = []
    claims = sorted(claim_store.list_for_run(run_id), key=lambda item: 1 if item.get("scope") == "epic" else 0)
    for record in claims:
        if restore_project:
            try:
                _restore_project(repo, config, record)
            except Exception as exc:
                warnings.append(f"could not restore Project status for {record['claim_key']}: {exc}")
        claim_store.release(record["claim_key"], run_id, reason)
    run_store = RunStore(git.common_dir(), run_id)
    if run_store.state_file.exists():
        state = run_store.load()
        claims_state = state.setdefault("claims", {"epic": None, "tasks": {}})
        if claims_state.get("epic"):
            claims_state["epic"]["status"] = "released"
        for item in claims_state.setdefault("tasks", {}).values():
            item["status"] = "released"
        atomic_json(run_store.state_file, state)
        _record_event(run_store, "CLAIMS_RELEASED", {"reason": reason, "count": len(claims), "warnings": warnings})
    return warnings


def integrate_claimed_run(repo: Path, run_id: str, config: dict[str, Any]) -> dict[str, Any]:
    from .integration import integrate_run

    git = GitService(repo)
    store = RunStore(git.common_dir(), run_id)
    before = store.load()
    if before.get("state") == "PROJECT_COMPLETED":
        state = before
    else:
        state = integrate_run(repo, run_id, config)
    if state.get("state") != "PROJECT_COMPLETED":
        return state
    _project_all(repo, state, config, "done")
    release_run_claims(repo, run_id, config, "verified delivery to main")
    return store.load()


def enrich_state_with_claims(common_dir: Path, state: dict[str, Any]) -> dict[str, Any]:
    value = json.loads(json.dumps(state))
    store = ClaimStore(common_dir)
    active = store.list_for_run(str(value.get("run_id") or ""))
    value["claim_status"] = {
        "epic": next((item for item in active if item.get("scope") == "epic"), None),
        "tasks": [item for item in active if item.get("scope") == "task"],
    }
    return value
