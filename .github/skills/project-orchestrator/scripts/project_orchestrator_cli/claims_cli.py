"""CLI extension that adds durable claims without changing the v2 engine contract."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from . import cli as base_cli
from .claims import (
    ClaimStore,
    enrich_state_with_claims,
    execute_claimed_run,
    integrate_claimed_run,
    prepare_claimed_final_pr,
    release_run_claims,
)
from .claims_runtime import prepare_claimed_run
from .contracts import Envelope, OrchestratorError, ValidationError, failure
from .git_service import GitService

json_or_human = base_cli.json_or_human


def parser() -> argparse.ArgumentParser:
    value = base_cli.parser()
    subparsers = next(action for action in value._actions if isinstance(action, argparse._SubParsersAction))
    claims = subparsers.add_parser("claims")
    claims_sub = claims.add_subparsers(dest="claims_command", required=True)
    claims_sub.add_parser("list")
    inspect = claims_sub.add_parser("inspect")
    inspect.add_argument("reference")
    release = claims_sub.add_parser("release")
    release.add_argument("reference")
    release.add_argument("--reason", required=True)
    release.add_argument("--force", action="store_true")
    return value


def _install_wrappers() -> None:
    base_cli.prepare_run = prepare_claimed_run
    base_cli.execute_run = execute_claimed_run
    base_cli.prepare_final_pr = prepare_claimed_final_pr
    base_cli.integrate_run = integrate_claimed_run


def _claim_store(path: Path) -> ClaimStore:
    return ClaimStore(GitService(path).common_dir())


def _owner_state(path: Path, run_id: str) -> dict | None:
    try:
        return base_cli.get_store(path, run_id).load()
    except Exception:
        return None


def _claim_details(path: Path, record: dict) -> dict:
    owner = _owner_state(path, str(record["run_id"]))
    worktree = record.get("worktree")
    return {
        **record,
        "owner_state": owner.get("state") if owner else None,
        "owner_state_path": str(base_cli.get_store(path, str(record["run_id"])).root),
        "worktree_exists": bool(worktree and Path(worktree).exists()),
        "owner_terminal": bool(owner and owner.get("state") in base_cli.TERMINAL),
    }


def _dispatch_claims(path: Path, args: argparse.Namespace) -> Envelope:
    store = _claim_store(path)
    if args.claims_command == "list":
        claims = [_claim_details(path, item) for item in store.list_active()]
        return Envelope("claims.list", result={"claims": claims})
    record = store.resolve(args.reference)
    if args.claims_command == "inspect":
        return Envelope("claims.inspect", result=_claim_details(path, record))
    if args.claims_command == "release":
        if not args.force:
            raise ValidationError("administrative claim release requires --force")
        released = store.release(record["claim_key"], "administrative", args.reason, force=True)
        return Envelope(
            "claims.release",
            changed=bool(released),
            result={
                "claim_key": record["claim_key"],
                "released": bool(released),
                "reason": args.reason,
            },
        )
    raise ValidationError(f"unknown claims command: {args.claims_command}")


def dispatch(args: argparse.Namespace) -> Envelope:
    path = base_cli.repo(args)
    if args.command == "claims":
        return _dispatch_claims(path, args)
    _install_wrappers()
    envelope = base_cli.dispatch(args)
    if args.command == "capabilities":
        commands = envelope.result.setdefault("commands", [])
        if not any(item.get("name") == "claims" for item in commands):
            commands.append(
                {
                    "name": "claims",
                    "mutating": True,
                    "supports_dry_run": False,
                    "supports_json": True,
                }
            )
    if args.command == "cancel":
        _, config = base_cli.load_config(path)
        warnings = release_run_claims(
            path,
            args.run_id,
            config,
            "explicit user cancellation",
            restore_project=True,
        )
        envelope.warnings.extend(warnings)
        envelope.result = enrich_state_with_claims(
            GitService(path).common_dir(),
            base_cli.get_store(path, args.run_id).load(),
        )
    elif args.command == "status":
        common_dir = GitService(path).common_dir()
        if args.run_id:
            envelope.result = enrich_state_with_claims(common_dir, envelope.result)
        else:
            envelope.result["runs"] = [
                enrich_state_with_claims(common_dir, item)
                for item in envelope.result.get("runs", [])
            ]
    return envelope


def main(argv: list[str] | None = None) -> int:
    raw = list(sys.argv[1:] if argv is None else argv)
    if "--json" in raw:
        raw.remove("--json")
        raw.insert(0, "--json")
    args = parser().parse_args(raw)
    command_name = (
        args.command
        if args.command not in {"providers", "claims"}
        else f"{args.command}.{getattr(args, args.command + '_command')}"
    )
    try:
        envelope = dispatch(args)
        json_or_human(envelope, args.json)
        return 0
    except OrchestratorError as exc:
        json_or_human(failure(command_name, exc), True)
        return exc.code
    except Exception as exc:
        json_or_human(failure(command_name, exc), True)
        return 1
