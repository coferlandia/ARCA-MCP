"""Deterministic Epic/task orchestration engine."""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .contracts import DependencyError, ValidationError, validate_json_schema
from .git_service import GitService
from .integration_gates import validate_integration_config
from .materialization import materialize_github_epic
from .providers import ProcessRequest, extract_agent_result, provider
from .state import RunStore, TERMINAL, atomic_json
from .work_items import direct_plan_manifest, load_manifest, next_ready_task, record_commit, task_by_id

DEFAULT_CONFIG: dict[str, Any] = {
    "version": 2,
    "git": {
        "base_branch": "main",
        "branch_prefix": "orchestrator",
        "worktree_root": "../.worktrees",
        "require_clean_base_worktree": True,
        "fetch_before_run": False,
        "push_after_merge": False,
        "remove_review_worktree_after_review": True,
        "remove_implementation_worktree_after_integration": True,
        "delete_epic_branch_after_integration": True,
    },
    "integration": {
        "github": {
            "required_gates": [],
            "wait_seconds": 30,
            "max_wait_cycles": None,
        }
    },
    "roles": {
        role: {
            "primary": {
                "client": "codex",
                "model": "gpt-5.6-luna" if role == "code_reviewer" else "gpt-5.4-mini",
                "reasoning": "medium",
            },
            "fallbacks": [{"client": "opencode", "model": "opencode/big-pickle", "variant": "high"}],
        }
        for role in ("orchestrator", "coding_agent", "completion_verifier", "code_reviewer", "fix_agent")
    },
    "providers": {
        "codex": {
            "command": "codex",
            "enabled": True,
            "sandbox": {
                "orchestrator": "read-only",
                "coding_agent": "workspace-write",
                "completion_verifier": "read-only",
                "code_reviewer": "read-only",
                "fix_agent": "workspace-write",
            },
        },
        "opencode": {"command": "opencode", "enabled": True, "server": {"enabled": False, "url": "http://localhost:4096"}},
    },
    "retry": {
        "provider_wait_seconds": 300,
        "transient_attempts_per_provider": 2,
        "max_provider_wait_cycles": None,
        "persist_before_wait": True,
        "retry_jitter_seconds": 0,
    },
    "timeouts": {
        "specification_analysis_seconds": 1800,
        "coding_seconds": 14400,
        "completion_verification_seconds": 1800,
        "review_seconds": 3600,
        "fix_seconds": 7200,
        "test_seconds": 3600,
        "process_termination_grace_seconds": 30,
    },
    "loops": {"max_no_progress_cycles": 3, "max_malformed_result_cycles": 3, "max_review_fix_cycles": None},
    "protocol": {"version": "1.0", "retain_full_event_streams": True, "retain_stdout": True, "retain_stderr": True, "redact_secrets": True},
    "validation": {"commands": []},
}


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def config_path(repo: Path) -> Path:
    return repo / ".project-orchestrator" / "config.json"


def _deep_merge(default: dict[str, Any], override: dict[str, Any]) -> dict[str, Any]:
    result = json.loads(json.dumps(default))
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key] = _deep_merge(result[key], value)
        else:
            result[key] = value
    return result


def load_config(repo: Path, requested: str | None = None) -> tuple[Path, dict[str, Any]]:
    path = Path(requested).resolve() if requested else config_path(repo)
    if not path.exists():
        return path, json.loads(json.dumps(DEFAULT_CONFIG))
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValidationError(f"invalid JSON configuration: {exc}") from exc
    if raw.get("version") == 1:
        raw = {**raw, "version": 2}
        git = dict(raw.get("git") or {})
        for retired in (
            "merge_strategy",
            "base_update_strategy",
            "candidate_commit_strategy",
            "one_commit_per_phase",
            "delete_phase_branch_after_merge",
            "remove_implementation_worktree_after_merge",
        ):
            git.pop(retired, None)
        raw["git"] = git
    value = _deep_merge(DEFAULT_CONFIG, raw)
    validate_config(value)
    return path, value


def validate_config(value: dict[str, Any]) -> None:
    if value.get("version") != 2:
        raise ValidationError("configuration version must be 2")
    for key in ("git", "integration", "roles", "providers", "retry", "timeouts", "loops", "protocol"):
        if not isinstance(value.get(key), dict):
            raise ValidationError(f"configuration requires object: {key}")
    for role in ("orchestrator", "coding_agent", "completion_verifier", "code_reviewer", "fix_agent"):
        if role not in value["roles"]:
            raise ValidationError(f"configuration requires role: {role}")
    validate_integration_config(value)


def _resolve_manifest(
    repo: Path,
    *,
    spec: Path | None,
    epic: str | None,
    manifest_path: Path | None,
    dry_run: bool,
) -> dict[str, Any]:
    if sum(value is not None for value in (spec, epic, manifest_path)) != 1:
        raise ValidationError("exactly one execution source is required: --spec, --epic, or --manifest")
    if spec is not None:
        return direct_plan_manifest(spec)
    if manifest_path is not None:
        return load_manifest(manifest_path)
    if dry_run:
        raise ValidationError("GitHub --epic dry-run requires local materialization; run materialize first and use --manifest for a mutation-free preview")
    assert epic is not None
    return materialize_github_epic(repo, epic)


def _epic_slug(manifest: dict[str, Any]) -> str:
    raw = str(manifest.get("epic", {}).get("id") or "epic")
    return re.sub(r"[^a-z0-9-]+", "-", raw.lower()).strip("-") or "epic"


def _worktree_assignment(repo: Path, config: dict[str, Any], run_id: str, manifest: dict[str, Any], base: str) -> dict[str, str]:
    root = (repo / config["git"]["worktree_root"]).resolve()
    branch = f"{config['git']['branch_prefix']}/{run_id}/{_epic_slug(manifest)}"
    path = root / repo.name / run_id / "implementation"
    return {
        "branch": branch,
        "implementation_worktree": str(path),
        "review_pattern": str(path.parent / "review-<kind>-<cycle>-<short-sha>"),
        "base_commit": base,
    }


def _copy_contracts_to_worktree(repo: Path, worktree: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    copied = json.loads(json.dumps(manifest))
    for node in [copied.get("epic", {}), *copied.get("tasks", [])]:
        raw = node.get("path")
        if not raw:
            continue
        source = Path(raw)
        source = source if source.is_absolute() else repo / source
        if not source.exists():
            continue
        try:
            relative = source.resolve().relative_to(repo.resolve())
        except ValueError:
            continue
        target = worktree / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        node["path"] = str(relative).replace("\\", "/")
    manifest_path = worktree / ".agent" / "work-items" / _epic_slug(copied) / "manifest.json"
    if copied.get("source", {}).get("kind") == "github":
        manifest_path = worktree / ".agent" / "work-items" / f"epic-{copied['source']['epic_issue']}" / "manifest.json"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    atomic_json(manifest_path, copied)
    copied["local_manifest_path"] = str(manifest_path)
    return copied


def prepare_run(
    repo: Path,
    spec: Path | None,
    config: dict[str, Any],
    run_id: str,
    dry_run: bool,
    base_override: str | None = None,
    *,
    epic: str | None = None,
    manifest_path: Path | None = None,
) -> dict[str, Any]:
    git = GitService(repo)
    git.ensure_repo()
    base_branch = base_override or config["git"]["base_branch"]
    base = git.head(base_branch)
    if config["git"].get("require_clean_base_worktree") and not git.clean():
        raise ValidationError("base worktree must be clean")
    manifest = _resolve_manifest(repo, spec=spec, epic=epic, manifest_path=manifest_path, dry_run=dry_run)
    assignment = _worktree_assignment(repo, config, run_id, manifest, base)
    result = {
        "run_id": run_id,
        "base_branch": base_branch,
        "base_commit": base,
        "manifest": manifest,
        "intended": assignment,
        "state_path": str(git.common_dir() / "project-orchestrator" / "runs" / run_id),
    }
    if dry_run:
        return result

    store = RunStore(git.common_dir(), run_id)
    store.create({
        "run_id": run_id,
        "state": "INITIALIZED",
        "repository": str(repo),
        "base_branch": base_branch,
        "base_commit": base,
        "manifest": manifest,
        "current_task_id": None,
        "resources": {},
        "retry": {},
        "cleanup_ownership": [],
        "task_reviews": {},
        "holistic_reviews": [],
        "provider_attempts": [],
        "sessions": {},
        "final_reviewed_sha": None,
    })
    atomic_json(store.root / "execution-manifest.json", manifest)
    store.transition("CONFIG_VALIDATED")
    store.transition("CONTRACT_RESOLVED")
    store.transition("EPIC_WORKTREE_CREATING", assignment)
    git.ensure_branch(assignment["branch"], base)
    implementation = Path(assignment["implementation_worktree"])
    git.add_worktree(implementation, assignment["branch"])
    value = store.load()
    value["manifest"] = _copy_contracts_to_worktree(repo, implementation, manifest)
    value["resources"] = {
        "implementation_worktree": str(implementation),
        "epic_branch": assignment["branch"],
        "epic_base_commit": base,
    }
    value["cleanup_ownership"] = [{"kind": "worktree", "path": str(implementation), "scope": "epic"}]
    atomic_json(store.state_file, value)
    store.transition("EPIC_WORKTREE_CREATED", assignment)
    _write_reports(store, store.load())
    return result


def _write_reports(store: RunStore, state: dict[str, Any]) -> None:
    manifest = state.get("manifest", {})
    tasks = manifest.get("tasks", [])
    files = {
        "RUN.md": f"# Orchestration run {state['run_id']}\n\nBase: `{state.get('base_branch')}` / `{state.get('base_commit')}`\nMode: `{manifest.get('execution_mode')}`\n",
        "WORK-ITEMS.md": "# Work items\n\n" + "\n".join(f"- {task['id']}: {task.get('status', 'pending')}" for task in tasks) + "\n",
        "CURRENT-STATUS.md": (
            f"# Current status\n\n- Run: {state['run_id']}\n- State: {state['state']}\n"
            f"- Task: {state.get('current_task_id') or 'none'}\n"
            f"- Implementation worktree: {state.get('resources', {}).get('implementation_worktree', 'none')}\n"
            f"- Final reviewed SHA: {state.get('final_reviewed_sha') or 'none'}\n"
        ),
    }
    if state.get("state") == "PROJECT_COMPLETED":
        files["FINAL-REPORT.md"] = f"# Final report\n\nRun `{state['run_id']}` completed at {state.get('updated_at')}.\n"
    for name, content in files.items():
        (store.root / name).write_text(content, encoding="utf-8")


def progress_signature(value: dict[str, Any]) -> str:
    payload = {key: value.get(key) for key in ("status", "remaining_work", "changed_files", "findings", "tests", "blockers", "scope_deviations")}
    return hashlib.sha256(json.dumps(payload, sort_keys=True, default=str).encode()).hexdigest()


def normalize_provider_result(role: str, value: dict[str, Any]) -> dict[str, Any]:
    result = dict(value)
    status = result.get("status")
    if role == "completion-verifier":
        result["status"] = {"pass": "completed", "passed": "completed", "success": "completed", "fail": "incomplete", "failed": "incomplete"}.get(status, status)
        acceptance = result.get("acceptance", {})
        result["requirement_completion"] = [{"criterion": key, "status": val} for key, val in acceptance.items()] if isinstance(acceptance, dict) else acceptance
        keys = ("protocol_version", "run_id", "phase_id", "attempt_id", "role", "worktree_path", "status", "requirement_completion", "remaining_work", "blockers", "scope_deviations", "tests")
        result = {key: result.get(key, [] if key in {"requirement_completion", "remaining_work", "blockers", "scope_deviations", "tests"} else "") for key in keys}
    elif role == "code-reviewer":
        result["status"] = {"pass": "approved", "passed": "approved", "success": "approved", "fail": "changes-required", "failed": "changes-required"}.get(status, status)
        keys = ("protocol_version", "run_id", "phase_id", "attempt_id", "role", "worktree_path", "status", "candidate_commit", "base_commit", "findings", "tests", "scope_deviations")
        result = {key: result.get(key, [] if key in {"findings", "tests", "scope_deviations"} else "") for key in keys}
    elif role == "fix-agent":
        result["status"] = {"pass": "completed", "passed": "completed", "success": "completed"}.get(status, status)
        keys = ("protocol_version", "run_id", "phase_id", "attempt_id", "role", "worktree_path", "status", "findings", "changed_files", "tests", "blockers")
        result = {key: result.get(key, [] if key in {"findings", "changed_files", "tests", "blockers"} else "") for key in keys}
    elif role == "coding-agent":
        result["status"] = {"pass": "completed", "passed": "completed", "success": "completed"}.get(status, status)
        if "changed_files" not in result and isinstance(result.get("changes"), list):
            result["changed_files"] = result["changes"]
    return result


def _attempt_dir(store: RunStore, work_item_id: str, role: str, number: int) -> Path:
    path = store.root / "work-items" / work_item_id / "attempts" / f"{role}-{number:03d}"
    path.mkdir(parents=True, exist_ok=True)
    return path


def _role_candidates(config: dict[str, Any], role: str, provider_override: str | None = None, model_override: str | None = None) -> list[dict[str, Any]]:
    entry = config["roles"].get(role.replace("-", "_"), config["roles"].get(role, {}))
    candidates: list[dict[str, Any]] = []
    if entry.get("primary"):
        candidates.append(dict(entry["primary"]))
    candidates.extend(dict(item) for item in entry.get("fallbacks", []))
    if provider_override:
        candidates = [dict(candidate, client=provider_override) for candidate in candidates if candidate.get("client") == provider_override] or [{"client": provider_override, "model": model_override}]
    if model_override:
        for candidate in candidates:
            candidate["model"] = model_override
    return candidates


def _write_attempt(attempt: Path, request: dict[str, Any], result: dict[str, Any], report: str, execution: dict[str, Any], events: list[dict[str, Any]]) -> None:
    atomic_json(attempt / "request.json", request)
    atomic_json(attempt / "agent-result.json", result)
    atomic_json(attempt / "execution.json", execution)
    (attempt / "agent-report.md").write_text(report, encoding="utf-8")
    (attempt / "events.jsonl").write_text("".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")


def _invoke(store: RunStore, state: dict[str, Any], config: dict[str, Any], work_item: dict[str, Any], role: str, worktree: Path, prompt: str, candidate: str | None = None) -> dict[str, Any]:
    attempts = state.setdefault("provider_attempts", [])
    work_item_id = work_item["id"]
    number = len(attempts) + 1
    attempt = _attempt_dir(store, work_item_id, role, number)
    prompt_file = attempt / "instructions.md"
    prompt_file.write_text(prompt, encoding="utf-8")
    schema_name = {"coding-agent": "coding-result.schema.json", "completion-verifier": "completion-result.schema.json", "code-reviewer": "review-result.schema.json", "fix-agent": "fix-result.schema.json"}[role]
    schema = Path(__file__).resolve().parents[2] / "schemas" / schema_name
    last_error: Exception | None = None
    for choice in _role_candidates(config, role, state.get("requested_provider"), state.get("requested_model")):
        client = choice.get("client")
        try:
            adapter = provider(client, config)
            health = adapter.probe()
        except Exception as exc:
            last_error = exc
            continue
        if not health.available:
            last_error = DependencyError(f"provider executable unavailable: {client}")
            continue
        model = adapter.resolve_model(choice.get("model"))
        session = state.get("sessions", {}).get(role)
        command = adapter.build_command(
            worktree=worktree,
            model=model,
            prompt_file=prompt_file,
            role=role,
            reasoning=choice.get("reasoning") or choice.get("variant"),
            session_id=session,
            output_schema=schema,
        )
        timeout_key = {"coding-agent": "coding_seconds", "completion-verifier": "completion_verification_seconds", "code-reviewer": "review_seconds", "fix-agent": "fix_seconds"}[role]
        execution = adapter.execute(ProcessRequest(
            command=command,
            cwd=worktree,
            stdin=prompt,
            timeout=config["timeouts"][timeout_key],
            termination_grace=config["timeouts"].get("process_termination_grace_seconds", 30),
            pid_path=attempt / "process.pid",
        ))
        classification = adapter.classify_failure(execution)
        attempts.append({"attempt": number, "role": role, "provider": client, "model": model, "classification": classification, "session_id": execution.session_id, "at": now()})
        if execution.session_id:
            state.setdefault("sessions", {})[role] = execution.session_id
        atomic_json(store.state_file, state)
        last_message_path = prompt_file.with_suffix(".last.md")
        last_message = last_message_path.read_text(encoding="utf-8") if last_message_path.exists() else None
        result = normalize_provider_result(role, extract_agent_result(execution.events, last_message))
        result["protocol_version"] = "1.0"
        result["run_id"] = state["run_id"]
        result["phase_id"] = work_item_id  # compatibility field; semantically work_item_id in v2
        result["attempt_id"] = attempt.name
        result["role"] = role
        result["worktree_path"] = str(worktree)
        required_defaults = {
            "coding-agent": {"remaining_work", "changed_files", "tests", "blockers", "scope_deviations"},
            "completion-verifier": {"requirement_completion", "remaining_work", "blockers", "scope_deviations", "tests"},
            "code-reviewer": {"findings", "tests", "scope_deviations"},
            "fix-agent": {"findings", "changed_files", "tests", "blockers"},
        }[role]
        for key in required_defaults:
            result.setdefault(key, [])
        if role == "code-reviewer":
            result.setdefault("candidate_commit", candidate or git_head_safe(worktree))
            result.setdefault("base_commit", state["resources"].get("epic_base_commit", ""))
        try:
            validate_json_schema(result, schema)
        except ValidationError as exc:
            last_error = exc
            continue
        result.setdefault("candidate_commit", candidate)
        result.setdefault("provider", client)
        result.setdefault("model", model)
        _write_attempt(
            attempt,
            {"role": role, "work_item_id": work_item_id, "worktree_path": str(worktree), "candidate_commit": candidate},
            result,
            f"# {role}\n\nStatus: `{result.get('status')}`\n\nProvider: `{client}`\n",
            execution.__dict__,
            execution.events,
        )
        return result
    if last_error:
        raise last_error
    raise DependencyError(f"no provider available for {role}")


def git_head_safe(worktree: Path) -> str:
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=worktree, text=True).strip()
    except (OSError, subprocess.CalledProcessError):
        return ""


def _contract_prompt(state: dict[str, Any], work_item: dict[str, Any], role: str, worktree: Path, candidate: str | None = None, findings: list[Any] | None = None, *, holistic: bool = False) -> str:
    manifest = state["manifest"]
    return (
        f"You are the {role} in project-orchestrator v2.\n"
        f"Use exactly this worktree: {worktree}\n"
        "Do not perform Git lifecycle operations or commit.\n"
        f"Execution mode: {manifest.get('execution_mode')}\n"
        f"Epic contract: {manifest.get('epic', {}).get('path') or 'none'}\n"
        f"Assigned work item: {work_item['id']} — {work_item.get('title', '')}\n"
        f"Work contract: {work_item.get('path')}\n"
        f"Holistic Epic review: {'yes' if holistic else 'no'}\n"
        f"Candidate: {candidate or 'none'}\n"
        f"Findings: {json.dumps(findings or [])}\n"
        "Read only the bounded contract files plus repository context necessary to execute/verify them. "
        "Do not browse GitHub or redesign the contract. Return the required agent-result.json protocol as the final JSON object."
    )


def deterministic_checks(repo: Path, worktree: Path, config: dict[str, Any], base: str) -> dict[str, Any]:
    checks: list[dict[str, Any]] = []
    for command in list(config.get("validation", {}).get("commands", [])):
        argv = command if isinstance(command, list) else str(command).split()
        if not argv:
            continue
        try:
            result = subprocess.run(argv, cwd=worktree, text=True, capture_output=True, timeout=config["timeouts"].get("test_seconds", 3600), check=False)
            checks.append({"command": argv, "returncode": result.returncode, "stdout": result.stdout[-4000:], "stderr": result.stderr[-4000:]})
        except subprocess.TimeoutExpired:
            checks.append({"command": argv, "returncode": -1, "failure": "TIMEOUT"})
    return {"passed": all(item.get("returncode", 0) == 0 for item in checks), "checks": checks, "diff_hash": GitService(worktree).diff_hash(base, worktree)}


def _commit_message(manifest: dict[str, Any], task: dict[str, Any], *, kind: str, review_round: int | None = None) -> str:
    title = re.sub(r"\s+", " ", str(task.get("title") or task["id"])).strip()
    subject = ("fix" if kind == "review-fix" else "feat") + f": {title[:68]}"
    trailers: list[str] = []
    if task.get("issue") is not None:
        trailers.append(f"Issue: #{task['issue']}")
    epic_issue = manifest.get("source", {}).get("epic_issue")
    if epic_issue is not None:
        trailers.append(f"Epic: #{epic_issue}")
    if review_round is not None:
        trailers.append(f"Review: round {review_round}")
    return subject + ("\n\n" + "\n".join(trailers) if trailers else "")


def _review_worktree_path(implementation: Path, kind: str, cycle: int, sha: str) -> Path:
    return implementation.parent / f"review-{kind}-{cycle}-{sha[:8]}"


def _wait_for_provider(store: RunStore, state: dict[str, Any], config: dict[str, Any], resume_state: str, exc: Exception) -> dict[str, Any]:
    state["resume_state"] = resume_state
    state["retry"] = {"next_retry_at": now(), "wait_seconds": config["retry"].get("provider_wait_seconds", 300), "reason": str(exc)}
    atomic_json(store.state_file, state)
    return store.transition("WAITING_FOR_PROVIDER", {"reason": str(exc)})


def _remove_review(git: GitService, state: dict[str, Any], config: dict[str, Any]) -> None:
    raw = state.get("resources", {}).pop("review_worktree", None)
    if raw:
        path = Path(raw)
        if path.exists() and config["git"].get("remove_review_worktree_after_review", True):
            git.remove_worktree(path)


def _retry_coding(store: RunStore, state: dict[str, Any], config: dict[str, Any], detail: dict[str, Any]) -> dict[str, Any]:
    state = store.transition("CODING_INCOMPLETE", detail)
    signatures = state.setdefault("coding_progress_signatures", [])
    signature = progress_signature(detail)
    repeated = signatures and signatures[-1] == signature
    state["coding_no_progress_cycles"] = state.get("coding_no_progress_cycles", 0) + 1 if repeated else 0
    signatures.append(signature)
    atomic_json(store.state_file, state)
    if state["coding_no_progress_cycles"] >= config["loops"].get("max_no_progress_cycles", 3):
        return store.transition("BLOCKED_BY_NO_PROGRESS", {"signature": signature, "detail": detail})
    return store.transition("CODING_RUNNING")


def execute_run(repo: Path, run_id: str, config: dict[str, Any]) -> dict[str, Any]:
    git = GitService(repo)
    store = RunStore(git.common_dir(), run_id)
    with store.lock():
        state = store.load()
        while state["state"] not in TERMINAL and state["state"] not in {"EPIC_READY_FOR_INTEGRATION", "PR_OPEN_AWAITING_MERGE_APPROVAL"}:
            if state["state"] == "WAITING_FOR_PROVIDER":
                state = store.transition(state.get("resume_state", "CODING_RUNNING"))

            if state["state"] in {"EPIC_WORKTREE_CREATED", "TASK_READY_FOR_MERGE"}:
                if state["state"] == "TASK_READY_FOR_MERGE":
                    task = task_by_id(state["manifest"], state["current_task_id"])
                    task["status"] = "ready_for_merge"
                    state["current_task_id"] = None
                    atomic_json(store.state_file, state)
                next_task = next_ready_task(state["manifest"])
                if next_task is None:
                    state = store.transition("HOLISTIC_REVIEW_WORKTREE_CREATING")
                else:
                    state["current_task_id"] = next_task["id"]
                    next_task["status"] = "in_progress"
                    state["coding_no_progress_cycles"] = 0
                    state["coding_progress_signatures"] = []
                    atomic_json(store.state_file, state)
                    state = store.transition("TASK_SELECTED", {"task": next_task["id"]})

            if state["state"] == "TASK_SELECTED":
                state = store.transition("CODING_RUNNING")

            if state["state"] == "CODING_RUNNING":
                task = task_by_id(state["manifest"], state["current_task_id"])
                worktree = Path(state["resources"]["implementation_worktree"])
                try:
                    result = _invoke(store, state, config, task, "coding-agent", worktree, _contract_prompt(state, task, "coding-agent", worktree))
                except DependencyError as exc:
                    state = _wait_for_provider(store, state, config, "CODING_RUNNING", exc)
                    break
                state = store.transition("CODING_REPORTED", result)
                if result.get("status") != "completed":
                    state = _retry_coding(store, state, config, result)
                    if state["state"] in TERMINAL:
                        break
                    continue

            if state["state"] == "CODING_REPORTED":
                state = store.transition("COMPLETION_VERIFYING")

            if state["state"] == "COMPLETION_VERIFYING":
                task = task_by_id(state["manifest"], state["current_task_id"])
                worktree = Path(state["resources"]["implementation_worktree"])
                try:
                    result = _invoke(store, state, config, task, "completion-verifier", worktree, _contract_prompt(state, task, "completion-verifier", worktree))
                except DependencyError:
                    result = {"status": "completed", "requirement_completion": []}
                if result.get("status") != "completed":
                    state = _retry_coding(store, state, config, result)
                    if state["state"] in TERMINAL:
                        break
                    continue
                evidence = deterministic_checks(repo, worktree, config, state["resources"]["epic_base_commit"])
                state["test_evidence"] = evidence
                atomic_json(store.state_file, state)
                if not evidence["passed"]:
                    state = _retry_coding(store, state, config, {"blockers": ["deterministic validation failed"], "test_evidence": evidence})
                    if state["state"] in TERMINAL:
                        break
                    continue
                state = store.transition("CANDIDATE_PREPARING")

            if state["state"] in {"CANDIDATE_PREPARING", "FIX_COMMIT_PREPARING"}:
                task = task_by_id(state["manifest"], state["current_task_id"])
                worktree = Path(state["resources"]["implementation_worktree"])
                selected = git.stage_product_changes(worktree, include_work_items=state["manifest"].get("source", {}).get("kind") == "local")
                if not selected:
                    state = store.transition("BLOCKED_BY_NO_PROGRESS", {"reason": "no implementation changes to commit", "task": task["id"]})
                    break
                reviews = state.setdefault("task_reviews", {}).setdefault(task["id"], [])
                kind = "review-fix" if state["state"] == "FIX_COMMIT_PREPARING" else "candidate"
                review_round = len(reviews) if kind == "review-fix" else None
                sha = git.commit(_commit_message(state["manifest"], task, kind=kind, review_round=review_round), worktree)
                record_commit(task, sha, kind, review_round)
                state["candidate_commit"] = sha
                atomic_json(store.state_file, state)
                state = store.transition("CANDIDATE_COMMITTED", {"task": task["id"], "candidate": sha, "kind": kind})

            if state["state"] == "CANDIDATE_COMMITTED":
                if state.get("holistic_fix_pending"):
                    state["holistic_fix_pending"] = False
                    atomic_json(store.state_file, state)
                    state = store.transition("HOLISTIC_REVIEW_WORKTREE_CREATING")
                else:
                    state = store.transition("REVIEW_WORKTREE_CREATING")

            if state["state"] == "REVIEW_WORKTREE_CREATING":
                task = task_by_id(state["manifest"], state["current_task_id"])
                worktree = Path(state["resources"]["implementation_worktree"])
                sha = git.head(state["resources"]["epic_branch"])
                cycle = len(state.setdefault("task_reviews", {}).setdefault(task["id"], [])) + 1
                review = _review_worktree_path(worktree, "task", cycle, sha)
                git.add_review_worktree(review, sha)
                state["resources"]["review_worktree"] = str(review)
                state["candidate_commit"] = sha
                atomic_json(store.state_file, state)
                state = store.transition("REVIEW_WORKTREE_CREATED", {"candidate": sha, "task": task["id"]})

            if state["state"] == "REVIEW_WORKTREE_CREATED":
                state = store.transition("REVIEW_RUNNING")

            if state["state"] == "REVIEW_RUNNING":
                task = task_by_id(state["manifest"], state["current_task_id"])
                sha = state["candidate_commit"]
                review = Path(state["resources"]["review_worktree"])
                try:
                    result = _invoke(store, state, config, task, "code-reviewer", review, _contract_prompt(state, task, "code-reviewer", review, sha), sha)
                except DependencyError as exc:
                    state = _wait_for_provider(store, state, config, "REVIEW_RUNNING", exc)
                    break
                if result.get("candidate_commit") != sha:
                    result["status"] = "invalid-result"
                    result.setdefault("findings", []).append({"reason": "reviewed candidate does not equal Epic branch candidate"})
                state.setdefault("task_reviews", {}).setdefault(task["id"], []).append(result)
                _remove_review(git, state, config)
                atomic_json(store.state_file, state)
                state = store.transition("REVIEW_PASSED" if result.get("status") == "approved" else "FIXES_REQUIRED", result)

            if state["state"] == "FIXES_REQUIRED":
                task = task_by_id(state["manifest"], state["current_task_id"])
                worktree = Path(state["resources"]["implementation_worktree"])
                findings = state["task_reviews"][task["id"]][-1].get("findings", [])
                state = store.transition("FIXING")
                try:
                    result = _invoke(store, state, config, task, "fix-agent", worktree, _contract_prompt(state, task, "fix-agent", worktree, findings=findings))
                except DependencyError as exc:
                    state = _wait_for_provider(store, state, config, "FIXING", exc)
                    break
                if result.get("status") != "completed":
                    state = store.transition("BLOCKED_BY_NO_PROGRESS", result)
                    break
                signatures = state.setdefault("progress_signatures", [])
                signature = progress_signature(result)
                state["no_progress_cycles"] = state.get("no_progress_cycles", 0) + 1 if signatures and signatures[-1] == signature else 0
                signatures.append(signature)
                atomic_json(store.state_file, state)
                if state["no_progress_cycles"] >= config["loops"].get("max_no_progress_cycles", 3):
                    state = store.transition("BLOCKED_BY_NO_PROGRESS", {"signature": signature})
                    break
                state = store.transition("FIX_COMMIT_PREPARING")

            if state["state"] == "REVIEW_PASSED":
                task = task_by_id(state["manifest"], state["current_task_id"])
                task["status"] = "ready_for_merge"
                atomic_json(store.state_file, state)
                state = store.transition("TASK_READY_FOR_MERGE", {"task": task["id"], "candidate": state.get("candidate_commit")})

            if state["state"] == "HOLISTIC_REVIEW_WORKTREE_CREATING":
                worktree = Path(state["resources"]["implementation_worktree"])
                sha = git.head(state["resources"]["epic_branch"])
                cycle = len(state.get("holistic_reviews", [])) + 1
                review = _review_worktree_path(worktree, "epic", cycle, sha)
                git.add_review_worktree(review, sha)
                state["resources"]["review_worktree"] = str(review)
                state["candidate_commit"] = sha
                atomic_json(store.state_file, state)
                state = store.transition("HOLISTIC_REVIEW_WORKTREE_CREATED", {"candidate": sha})

            if state["state"] == "HOLISTIC_REVIEW_WORKTREE_CREATED":
                state = store.transition("HOLISTIC_REVIEW_RUNNING")

            if state["state"] == "HOLISTIC_REVIEW_RUNNING":
                synthetic = {
                    "id": "EPIC-REVIEW",
                    "title": state["manifest"].get("epic", {}).get("title", "Epic holistic review"),
                    "path": state["manifest"].get("epic", {}).get("path"),
                }
                sha = state["candidate_commit"]
                review = Path(state["resources"]["review_worktree"])
                try:
                    result = _invoke(store, state, config, synthetic, "code-reviewer", review, _contract_prompt(state, synthetic, "code-reviewer", review, sha, holistic=True), sha)
                except DependencyError as exc:
                    state = _wait_for_provider(store, state, config, "HOLISTIC_REVIEW_RUNNING", exc)
                    break
                if result.get("candidate_commit") != sha:
                    result["status"] = "invalid-result"
                    result.setdefault("findings", []).append({"reason": "holistic review candidate does not equal Epic branch HEAD"})
                state.setdefault("holistic_reviews", []).append(result)
                _remove_review(git, state, config)
                atomic_json(store.state_file, state)
                if result.get("status") == "approved":
                    state["final_reviewed_sha"] = sha
                    atomic_json(store.state_file, state)
                    state = store.transition("EPIC_READY_FOR_INTEGRATION", {"candidate": sha})
                else:
                    state = store.transition("HOLISTIC_FIXES_REQUIRED", result)

            if state["state"] == "HOLISTIC_FIXES_REQUIRED":
                synthetic = {
                    "id": "EPIC-REVIEW",
                    "title": state["manifest"].get("epic", {}).get("title", "Epic holistic review"),
                    "path": state["manifest"].get("epic", {}).get("path"),
                    "issue": None,
                }
                worktree = Path(state["resources"]["implementation_worktree"])
                findings = state["holistic_reviews"][-1].get("findings", [])
                state = store.transition("HOLISTIC_FIXING")
                try:
                    result = _invoke(store, state, config, synthetic, "fix-agent", worktree, _contract_prompt(state, synthetic, "fix-agent", worktree, findings=findings, holistic=True))
                except DependencyError as exc:
                    state = _wait_for_provider(store, state, config, "HOLISTIC_FIXING", exc)
                    break
                if result.get("status") != "completed":
                    state = store.transition("BLOCKED_BY_NO_PROGRESS", result)
                    break
                state["holistic_fix_pending"] = True
                state["current_task_id"] = None
                atomic_json(store.state_file, state)
                state = store.transition("HOLISTIC_FIX_COMMIT_PREPARING")

            if state["state"] == "HOLISTIC_FIX_COMMIT_PREPARING":
                worktree = Path(state["resources"]["implementation_worktree"])
                selected = git.stage_product_changes(worktree, include_work_items=state["manifest"].get("source", {}).get("kind") == "local")
                if not selected:
                    state = store.transition("BLOCKED_BY_NO_PROGRESS", {"reason": "holistic fix produced no changes"})
                    break
                epic_issue = state["manifest"].get("source", {}).get("epic_issue")
                message = "fix: address holistic Epic review"
                if epic_issue is not None:
                    message += f"\n\nEpic: #{epic_issue}\nReview: holistic round {len(state['holistic_reviews'])}"
                sha = git.commit(message, worktree)
                state.setdefault("holistic_fix_commits", []).append({"sha": sha, "review_round": len(state["holistic_reviews"])})
                state["candidate_commit"] = sha
                atomic_json(store.state_file, state)
                state = store.transition("CANDIDATE_COMMITTED", {"candidate": sha, "kind": "holistic-review-fix"})

            atomic_json(store.state_file, state)
            _write_reports(store, state)

        _write_reports(store, state)
        state["state_path"] = str(store.root)
        return state
