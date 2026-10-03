"""Final traceability, PR/integration, archival, and cleanup for project-orchestrator v2."""
from __future__ import annotations

import shutil
from pathlib import Path
from typing import Any

from .contracts import DependencyError, ValidationError
from .git_service import GitService
from .github_service import GitHubService, IssueRef
from .integration_gates import FAILED, GREEN, PENDING, evaluate_required_gates, integration_github_config
from .materialization import archive_delivered_tasks
from .state import RunStore, atomic_json, utcnow
from .work_items import task_by_id


def _project_cfg(config: dict[str, Any]) -> dict[str, Any] | None:
    value = config.get("github_project")
    return value if isinstance(value, dict) and value.get("owner") and value.get("number") is not None else None


def _status_name(config: dict[str, Any], logical: str) -> str:
    mapping = {
        "pending": "Todo",
        "in_progress": "In Progress",
        "review": "Review",
        "blocked": "Blocked",
        "done": "Done",
    }
    mapping.update(config.get("github_project", {}).get("status_mapping", {}))
    return str(mapping[logical])


def _github_refs(state: dict[str, Any]) -> tuple[str, int]:
    source = state.get("manifest", {}).get("source", {})
    repository = source.get("repository")
    epic = source.get("epic_issue")
    if not repository or epic is None:
        raise ValidationError("GitHub-backed run is missing repository/Epic identity")
    return str(repository), int(epic)


def _ensure_task_traceability(repo: Path, state: dict[str, Any], config: dict[str, Any]) -> None:
    if state.get("manifest", {}).get("source", {}).get("kind") != "github":
        return
    service = GitHubService(repo)
    repository, epic_number = _github_refs(state)
    project = _project_cfg(config)
    for task in state["manifest"].get("tasks", []):
        issue_number = task.get("issue")
        if issue_number is None:
            continue
        ref = IssueRef(repository, int(issue_number))
        for commit in task.get("commits", []):
            sha = str(commit["sha"])
            kind = str(commit.get("kind") or "candidate")
            marker = f"<!-- coferlandia-orchestrator:{state['run_id']}:commit:{sha} -->"
            body = (
                "## Orchestrator commit evidence\n\n"
                f"- Epic: #{epic_number}\n"
                f"- Task: #{issue_number}\n"
                f"- Commit: `{sha}`\n"
                f"- Kind: `{kind}`\n"
            )
            if commit.get("review_round") is not None:
                body += f"- Review round: {commit['review_round']}\n"
            service.ensure_issue_comment(ref, marker, body)

        reviews = state.get("task_reviews", {}).get(task["id"], [])
        for index, review in enumerate(reviews, start=1):
            candidate = str(review.get("candidate_commit") or "")
            marker = f"<!-- coferlandia-orchestrator:{state['run_id']}:review:{task['id']}:{index}:{candidate} -->"
            findings = review.get("findings") or []
            body = (
                "## Independent review evidence\n\n"
                f"- Candidate: `{candidate}`\n"
                f"- Round: {index}\n"
                f"- Result: `{review.get('status')}`\n"
                f"- Findings: {len(findings)}\n"
            )
            service.ensure_issue_comment(ref, marker, body)

        marker = f"<!-- coferlandia-orchestrator:{state['run_id']}:ready:{task['id']} -->"
        service.ensure_issue_comment(
            ref,
            marker,
            "## Task lifecycle\n\nStatus: `ready_for_merge`\n\nThe task remains open until the final Epic PR reaches the default branch.",
        )

        if project:
            issue = service.issue(ref)
            service.set_project_status(str(project["owner"]), int(project["number"]), issue, _status_name(config, "review"))


def _pr_body(state: dict[str, Any]) -> str:
    manifest = state["manifest"]
    source = manifest.get("source", {})
    epic_number = source.get("epic_issue")
    lines = [
        "## Summary",
        "",
        "Implements the reviewed Coferlandia Epic execution branch.",
        "",
        f"Final reviewed SHA: `{state['final_reviewed_sha']}`",
        "",
        "## Delivered work items",
        "",
    ]
    for task_id in manifest.get("execution_order", []):
        task = task_by_id(manifest, task_id)
        if task.get("issue") is not None:
            lines.append(f"- Closes #{task['issue']} — {task.get('title', task_id)}")
        else:
            lines.append(f"- {task_id} — {task.get('title', task_id)}")
    if epic_number is not None:
        lines.extend(["", "## Epic", "", f"Closes #{epic_number}"])
    lines.extend([
        "",
        "## Review",
        "",
        f"Task reviews: {sum(len(value) for value in state.get('task_reviews', {}).values())}",
        f"Holistic Epic reviews: {len(state.get('holistic_reviews', []))}",
        "",
        "Integration is intentionally separate from PR creation and requires an explicit orchestrator `integrate` action.",
    ])
    return "\n".join(lines) + "\n"


def _assert_reviewed_integration_head(
    git: GitService,
    repo: Path,
    state: dict[str, Any],
    *,
    check_local_base: bool = True,
) -> None:
    branch = state["resources"]["epic_branch"]
    if git.head(branch) != state.get("final_reviewed_sha"):
        raise ValidationError("Epic branch HEAD no longer matches the final reviewed SHA")
    if check_local_base and git.head(state["base_branch"]) != state["base_commit"]:
        raise ValidationError("base branch advanced after Epic execution; rerun holistic review against the new base")
    if not git.clean(repo, ignore_untracked=True):
        raise ValidationError("base worktree has tracked changes; integration is unsafe")


def prepare_final_pr(repo: Path, run_id: str, config: dict[str, Any]) -> dict[str, Any]:
    git = GitService(repo)
    store = RunStore(git.common_dir(), run_id)
    with store.lock():
        state = store.load()
        if state["state"] == "PR_OPEN_AWAITING_MERGE_APPROVAL":
            return state
        if state["state"] != "EPIC_READY_FOR_INTEGRATION":
            raise ValidationError(f"run is not ready for PR creation: {state['state']}")
        _assert_reviewed_integration_head(git, repo, state)
        if state["manifest"].get("source", {}).get("kind") != "github":
            return state

        _ensure_task_traceability(repo, state, config)
        repository, epic_number = _github_refs(state)
        branch = state["resources"]["epic_branch"]
        git.push_branch(branch)
        service = GitHubService(repo)
        title = f"Epic #{epic_number}: {state['manifest'].get('epic', {}).get('title', 'orchestrated implementation')}"
        pr = service.ensure_pull_request(repository, branch, state["base_branch"], title, _pr_body(state))
        state["manifest"]["final_pr"] = int(pr["number"])
        state["final_pr_url"] = pr.get("url")
        atomic_json(store.state_file, state)
        return store.transition("PR_OPEN_AWAITING_MERGE_APPROVAL", {"pr": pr.get("number"), "url": pr.get("url")})


def _archive_execution_contracts(store: RunStore, state: dict[str, Any]) -> None:
    worktree = Path(state["resources"]["implementation_worktree"])
    manifest = state["manifest"]
    for task in manifest.get("tasks", []):
        if task.get("status") == "ready_for_merge":
            task["status"] = "done"
    archive_delivered_tasks(worktree, manifest)
    state["manifest"] = manifest
    atomic_json(store.state_file, state)

    local_manifest_path = manifest.get("local_manifest_path")
    if local_manifest_path:
        atomic_json(Path(local_manifest_path), manifest)

    evidence = store.root / "work-items"
    if evidence.exists():
        shutil.rmtree(evidence)
    source_root = None
    epic_path = manifest.get("epic", {}).get("path")
    if epic_path:
        source = worktree / epic_path
        if source.exists():
            source_root = source.parent
    if source_root and source_root.exists():
        shutil.copytree(source_root, evidence)


def _sync_done(repo: Path, state: dict[str, Any], config: dict[str, Any]) -> list[str]:
    warnings: list[str] = []
    if state["manifest"].get("source", {}).get("kind") != "github":
        return warnings
    service = GitHubService(repo)
    repository, _ = _github_refs(state)
    project = _project_cfg(config)
    for task in state["manifest"].get("tasks", []):
        if task.get("issue") is None:
            continue
        issue = service.issue(IssueRef(repository, int(task["issue"])))
        if str(issue.get("state", "OPEN")).upper() != "CLOSED":
            warnings.append(f"Issue #{task['issue']} did not close after final PR merge")
        if project:
            service.set_project_status(str(project["owner"]), int(project["number"]), issue, _status_name(config, "done"))
    return warnings


def _record_gate_evidence(
    store: RunStore,
    state: dict[str, Any],
    candidate: dict[str, Any],
    observations: list[dict[str, Any]],
    decision: str,
    details: tuple[dict[str, Any], ...],
    phase: str,
) -> dict[str, Any]:
    state.setdefault("integration_gate_evidence", []).append({
        "at": utcnow(),
        "phase": phase,
        "candidate": candidate,
        "decision": decision,
        "details": list(details),
        "observations": observations,
    })
    atomic_json(store.state_file, state)
    return state


def _resolve_github_candidate(
    service: GitHubService,
    repository: str,
    pr_number: int,
    state: dict[str, Any],
) -> dict[str, Any]:
    pr = service.pull_request(repository, pr_number)
    if str(pr.get("state") or "").upper() != "OPEN":
        raise ValidationError(f"GitHub PR #{pr_number} is not open")
    pr_head_sha = str(pr.get("headRefOid") or "")
    if not pr_head_sha:
        raise ValidationError(f"GitHub PR #{pr_number} did not expose head SHA")
    if pr_head_sha != state.get("final_reviewed_sha"):
        raise ValidationError("GitHub PR head no longer matches the final reviewed SHA")
    base_branch = str(pr.get("baseRefName") or "")
    if base_branch != state["base_branch"]:
        raise ValidationError(f"GitHub PR base changed from {state['base_branch']} to {base_branch}")
    base_sha = service.branch_sha(repository, base_branch)
    queue_entry = service.merge_queue_entry(repository, pr_number)
    if queue_entry:
        queue_base = ((queue_entry.get("baseCommit") or {}).get("oid"))
        queue_head = ((queue_entry.get("headCommit") or {}).get("oid"))
        pending_reason = None
        if queue_base and str(queue_base) != base_sha:
            pending_reason = "merge queue entry has not reconciled to the current remote base"
        elif queue_head and str(queue_head) != pr_head_sha:
            pending_reason = "merge queue entry does not represent the current PR head"
        merge_group = None if pending_reason else service.merge_group_candidate(
            repository, pr_number, pr_head_sha, base_sha, enqueued_at=str(queue_entry.get("enqueuedAt") or "") or None
        )
        if merge_group:
            merge_group["merge_queue_entry_id"] = queue_entry.get("id")
            return merge_group
        return {
            "kind": "merge_group",
            "gate_sha": None,
            "pr_head_sha": pr_head_sha,
            "base_sha": base_sha,
            "pr_number": pr_number,
            "merge_queue_entry_id": queue_entry.get("id"),
            "pending_reason": pending_reason or "merge queue entry exists but no current merge_group candidate is available yet",
        }
    return {
        "kind": "pr_head",
        "gate_sha": pr_head_sha,
        "pr_head_sha": pr_head_sha,
        "base_sha": base_sha,
        "pr_number": pr_number,
    }


def _evaluate_github_candidate(
    store: RunStore,
    state: dict[str, Any],
    service: GitHubService,
    repository: str,
    pr_number: int,
    config: dict[str, Any],
    *,
    phase: str,
) -> tuple[dict[str, Any], str]:
    candidate = _resolve_github_candidate(service, repository, pr_number, state)
    if candidate["base_sha"] != state["base_commit"]:
        return candidate, "BASE_MOVED"
    if candidate.get("pending_reason"):
        state = store.load()
        _record_gate_evidence(store, state, candidate, [], PENDING, ({"decision": PENDING, "reason": candidate["pending_reason"]},), phase)
        return candidate, PENDING
    gate_config = integration_github_config(config)
    observations = service.integration_observations(repository, str(candidate["gate_sha"]))
    evaluation = evaluate_required_gates(gate_config["required_gates"], observations, str(candidate["gate_sha"]))
    state = store.load()
    _record_gate_evidence(store, state, candidate, observations, evaluation.decision, evaluation.details, phase)
    return candidate, evaluation.decision


def _gate_state(store: RunStore, decision: str, candidate: dict[str, Any], *, phase: str) -> dict[str, Any]:
    detail = {"phase": phase, "candidate": candidate, "decision": decision}
    if decision == PENDING:
        state = store.transition("WAITING_FOR_INTEGRATION_CHECKS", detail)
    elif decision == FAILED:
        state = store.transition("INTEGRATION_CHECKS_FAILED", detail)
    elif decision == "BASE_MOVED":
        state = store.transition("BLOCKED_BY_BASE_MOVED", detail)
    else:
        raise ValidationError(f"unsupported integration gate decision: {decision}")
    raise ValidationError(f"integration checks are not green: {state['state']}")


def _persist_wait_and_raise(store: RunStore, *, phase: str, exc: Exception) -> None:
    store.transition("WAITING_FOR_INTEGRATION_CHECKS", {"phase": phase, "reason": str(exc), "decision": PENDING})
    raise ValidationError(f"integration evidence is temporarily unavailable: {exc}") from exc


def _block_auth_and_raise(store: RunStore, *, phase: str, exc: DependencyError) -> None:
    waiting = store.transition("WAITING_FOR_INTEGRATION_CHECKS", {"phase": phase, "reason": str(exc), "decision": PENDING})
    store.transition("BLOCKED_BY_AUTHENTICATION", {"phase": phase, "reason": str(exc), "previous_state": waiting["state"]})
    raise exc


def integrate_run(repo: Path, run_id: str, config: dict[str, Any]) -> dict[str, Any]:
    git = GitService(repo)
    store = RunStore(git.common_dir(), run_id)
    with store.lock():
        state = store.load()
        kind = state["manifest"].get("source", {}).get("kind")
        allowed = {"EPIC_READY_FOR_INTEGRATION"} if kind != "github" else {
            "PR_OPEN_AWAITING_MERGE_APPROVAL",
            "WAITING_FOR_INTEGRATION_CHECKS",
            "INTEGRATION_CHECKS_FAILED",
        }
        if state["state"] not in allowed:
            raise ValidationError(f"run is not awaiting explicit integration: {state['state']}")
        _assert_reviewed_integration_head(git, repo, state, check_local_base=kind != "github")
        if kind != "github" and git.current_branch(repo) != state["base_branch"]:
            raise ValidationError(f"local integration requires the primary checkout on base branch {state['base_branch']}")
        branch = state["resources"]["epic_branch"]

        if kind == "github":
            repository, _ = _github_refs(state)
            pr_number = int(state["manifest"]["final_pr"])
            service = GitHubService(repo)
            try:
                first_candidate, first_decision = _evaluate_github_candidate(
                    store, store.load(), service, repository, pr_number, config, phase="initial",
                )
            except DependencyError as exc:
                _block_auth_and_raise(store, phase="initial", exc=exc)
            except ValidationError:
                raise
            except Exception as exc:
                _persist_wait_and_raise(store, phase="initial", exc=exc)
            if first_decision != GREEN:
                _gate_state(store, first_decision, first_candidate, phase="initial")

            try:
                second_candidate, second_decision = _evaluate_github_candidate(
                    store, store.load(), service, repository, pr_number, config, phase="pre-merge-revalidation",
                )
            except DependencyError as exc:
                _block_auth_and_raise(store, phase="pre-merge-revalidation", exc=exc)
            except ValidationError:
                raise
            except Exception as exc:
                _persist_wait_and_raise(store, phase="pre-merge-revalidation", exc=exc)
            identity_keys = ("kind", "gate_sha", "pr_head_sha", "base_sha", "pr_number")
            if any(first_candidate.get(key) != second_candidate.get(key) for key in identity_keys):
                store.transition("WAITING_FOR_INTEGRATION_CHECKS", {
                    "phase": "pre-merge-revalidation",
                    "decision": PENDING,
                    "reason": "integration candidate changed during revalidation",
                    "before": first_candidate,
                    "after": second_candidate,
                })
                raise ValidationError("integration candidate changed during revalidation")
            if second_decision != GREEN:
                _gate_state(store, second_decision, second_candidate, phase="pre-merge-revalidation")

            state = store.transition("INTEGRATING", {
                "branch": branch,
                "reviewed_sha": state["final_reviewed_sha"],
                "candidate": second_candidate,
                "gate_decision": GREEN,
            })
            merged = service.merge_pull_request_squash(repository, pr_number, str(second_candidate["pr_head_sha"]))
            merge_commit = merged.get("mergeCommit") or {}
            final_sha = merge_commit.get("oid") if isinstance(merge_commit, dict) else None
            if not final_sha:
                raise ValidationError("merged PR did not expose a final merge/squash SHA")
        else:
            state = store.transition("INTEGRATING", {"branch": branch, "reviewed_sha": state["final_reviewed_sha"]})
            git.merge_squash(branch, cwd=repo)
            epic_title = state["manifest"].get("epic", {}).get("title", "orchestrated implementation")
            final_sha = git.commit(f"feat: integrate {epic_title}", repo)

        state["manifest"]["squash_sha"] = final_sha
        state["final_integration_sha"] = final_sha
        atomic_json(store.state_file, state)
        state = store.transition("INTEGRATED", {"sha": final_sha})
        state = store.transition("ARCHIVING")
        _archive_execution_contracts(store, state)
        state = store.load()
        state["integration_warnings"] = _sync_done(repo, state, config)
        atomic_json(store.state_file, state)

        implementation = Path(state["resources"]["implementation_worktree"])
        if implementation.exists() and config["git"].get("remove_implementation_worktree_after_integration", True):
            git.remove_worktree(implementation)
        if config["git"].get("delete_epic_branch_after_integration", True):
            git.remove_branch(branch)
        state = store.transition("PROJECT_COMPLETED", {"sha": final_sha, "warnings": state.get("integration_warnings", [])})
        state["state_path"] = str(store.root)
        atomic_json(store.state_file, state)
        return state
