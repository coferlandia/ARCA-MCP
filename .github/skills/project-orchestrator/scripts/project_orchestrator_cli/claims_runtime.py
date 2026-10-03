"""Reviewed claim preparation lifecycle used by the public CLI."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

from . import claims as _claims
from .git_service import GitService
from .state import RunStore


def stable_epic_key(manifest: dict[str, Any], source_path: Path | None = None) -> str:
    """Build a path-independent local-manifest key and a content-bound direct-plan key."""
    github = _claims._github_identity(manifest)
    if github:
        return f"epic:github:{github[0]}#{github[1]}"
    source = manifest.get("source", {})
    epic_id = str(manifest.get("epic", {}).get("id") or "EPIC")
    kind = "direct-plan" if manifest.get("execution_mode") == "direct-plan" else "local"
    identity = source.get("source_hash") or source.get("contract_revision")
    if kind == "direct-plan" and source_path is not None and source_path.exists():
        identity = _claims._source_digest(source_path)
    identity = str(identity or hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest())
    return f"epic:{kind}:{identity}:{epic_id}"


def prepare_claimed_run(
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
    """Acquire the Epic claim and fail cleanly before durable run creation."""
    from . import engine

    if dry_run:
        return engine.prepare_run(
            repo,
            spec,
            config,
            run_id,
            dry_run,
            base_override,
            epic=epic,
            manifest_path=manifest_path,
        )

    git = GitService(repo)
    git.ensure_repo()
    common_dir = git.common_dir()
    claim_store = _claims.ClaimStore(common_dir)
    manifest, identity_path = _claims._manifest_before_prepare(repo, spec, epic, manifest_path)
    claim_key = stable_epic_key(manifest, identity_path)
    record = claim_store.acquire(
        _claims._claim_record(
            claim_key=claim_key,
            scope="epic",
            run_id=run_id,
            manifest=manifest,
        )
    )

    github = _claims._github_identity(manifest)
    if github:
        try:
            record = _claims._project_claim(
                repo,
                config,
                claim_store,
                record,
                github[0],
                github[1],
                "in_progress",
            )
        except Exception:
            current = claim_store.get(claim_key) or record
            try:
                _claims._restore_project(repo, config, current)
            finally:
                claim_store.release(
                    claim_key,
                    run_id,
                    "Project In Progress projection failed before durable run creation",
                )
            raise

    run_store = RunStore(common_dir, run_id)
    try:
        result = engine.prepare_run(
            repo,
            spec,
            config,
            run_id,
            False,
            base_override,
            epic=epic,
            manifest_path=manifest_path,
        )
    except Exception:
        if not run_store.state_file.exists():
            try:
                _claims._restore_project(repo, config, record)
            finally:
                claim_store.release(
                    claim_key,
                    run_id,
                    "run preparation failed before durable run creation",
                )
        raise

    state = run_store.load()
    record = claim_store.update(
        claim_key,
        run_id,
        {"worktree": state.get("resources", {}).get("implementation_worktree")},
    )
    _claims._record_claim(run_store, "epic", record)
    _claims._record_event(run_store, "EPIC_CLAIM_ACQUIRED", {"claim_key": claim_key})
    return result
