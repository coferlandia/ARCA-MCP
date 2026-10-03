"""Argument parsing and command dispatch for project-orchestrator v2."""
from __future__ import annotations

import argparse
import json
import os
import shutil
import signal
import sys
import uuid
from pathlib import Path

from .contracts import Envelope, OrchestratorError, ValidationError, failure, validate_json_schema
from .engine import DEFAULT_CONFIG, config_path, execute_run, load_config, prepare_run, validate_config
from .git_service import GitService
from .integration import integrate_run, prepare_final_pr
from .providers import provider
from .state import RunStore, TERMINAL, atomic_json, utcnow

COMMANDS = [
    {"name": "doctor", "mutating": False, "supports_dry_run": False, "supports_json": True},
    {"name": "init-config", "mutating": True, "supports_dry_run": False, "supports_json": True},
    {"name": "validate-config", "mutating": False, "supports_dry_run": False, "supports_json": True},
    {"name": "providers.list", "mutating": False, "supports_dry_run": False, "supports_json": True},
    {"name": "providers.probe", "mutating": False, "supports_dry_run": False, "supports_json": True},
    {"name": "run", "mutating": True, "supports_dry_run": True, "supports_json": True},
    {"name": "resume", "mutating": True, "supports_dry_run": False, "supports_json": True},
    {"name": "retry", "mutating": True, "supports_dry_run": False, "supports_json": True},
    {"name": "integrate", "mutating": True, "supports_dry_run": False, "supports_json": True},
    {"name": "status", "mutating": False, "supports_dry_run": False, "supports_json": True},
    {"name": "cancel", "mutating": True, "supports_dry_run": False, "supports_json": True},
    {"name": "cleanup", "mutating": True, "supports_dry_run": True, "supports_json": True},
    {"name": "validate-result", "mutating": False, "supports_dry_run": False, "supports_json": True},
]


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Deterministic Epic/task development orchestrator")
    p.add_argument("--repo", default=".", help="target Git repository (default: current directory)")
    p.add_argument("--json", action="store_true", help="emit stable JSON envelope")
    sub = p.add_subparsers(dest="command", required=True)
    for name in ("version", "self-check", "capabilities", "doctor", "init-config", "validate-config"):
        sub.add_parser(name)

    pp = sub.add_parser("providers")
    pp_sub = pp.add_subparsers(dest="providers_command", required=True)
    pp_sub.add_parser("list")
    probe = pp_sub.add_parser("probe")
    probe.add_argument("name", nargs="?")

    run = sub.add_parser("run")
    source = run.add_mutually_exclusive_group(required=True)
    source.add_argument("--spec", help="local detailed plan/specification; executes as one direct-plan unit")
    source.add_argument("--epic", help="GitHub Epic Issue URL/reference")
    source.add_argument("--manifest", help="local v2 execution manifest")
    run.add_argument("--config")
    run.add_argument("--base-branch")
    run.add_argument("--run-id")
    run.add_argument("--provider")
    run.add_argument("--model")
    run.add_argument("--reasoning")
    run.add_argument("--dry-run", action="store_true")

    for name in ("resume", "retry", "cancel", "cleanup", "integrate"):
        command = sub.add_parser(name)
        command.add_argument("run_id")
        if name == "cleanup":
            command.add_argument("--dry-run", action="store_true")

    status = sub.add_parser("status")
    status.add_argument("run_id", nargs="?")
    validate_result = sub.add_parser("validate-result")
    validate_result.add_argument("--role", required=True, choices=["coding-agent", "completion-verifier", "code-reviewer", "fix-agent"])
    validate_result.add_argument("--file", required=True)
    return p


def repo(args: argparse.Namespace) -> Path:
    path = Path(args.repo).resolve()
    if not path.is_dir():
        raise ValidationError(f"repository directory does not exist: {path}")
    return path


def json_or_human(envelope: Envelope, as_json: bool) -> None:
    payload = envelope.payload()
    if as_json or not sys.stdout.isatty():
        print(json.dumps(payload, indent=2, sort_keys=True))
    else:
        print(f"{payload['status']}: {payload['command']}")
        print(json.dumps(payload["result"], indent=2))


def get_store(path: Path, run_id: str) -> RunStore:
    return RunStore(GitService(path).common_dir(), run_id)


def cmd_doctor(path: Path) -> Envelope:
    git = GitService(path)
    git.ensure_repo()
    config_file, config = load_config(path)
    schemas = Path(__file__).resolve().parents[2] / "schemas"
    required = list(schemas.glob("*.schema.json"))
    health = {
        name: provider(name, config).probe().__dict__
        for name in config["providers"]
        if config["providers"][name].get("enabled", True)
    }
    configured_models = {
        role: {
            "primary": value.get("primary", {}).get("model"),
            "fallbacks": [item.get("model") for item in value.get("fallbacks", [])],
        }
        for role, value in config.get("roles", {}).items()
    }
    runs = git.common_dir() / "project-orchestrator" / "runs"
    interrupted: list[str] = []
    stale_locks: list[str] = []
    collisions: list[str] = []
    if runs.exists():
        for state_file in runs.glob("*/run-state.json"):
            value = json.loads(state_file.read_text(encoding="utf-8"))
            if value.get("state") not in TERMINAL and value.get("state") != "PROJECT_COMPLETED":
                interrupted.append(str(value.get("run_id")))
        for lock in runs.glob("*/.lock"):
            try:
                pid = json.loads(lock.read_text(encoding="utf-8")).get("pid")
                if os.name != "nt" and not Path(f"/proc/{pid}").exists():
                    stale_locks.append(str(lock))
            except (OSError, json.JSONDecodeError):
                stale_locks.append(str(lock))
        managed = [
            str(Path(item.get("path")).resolve())
            for state_file in runs.glob("*/run-state.json")
            for item in json.loads(state_file.read_text(encoding="utf-8")).get("cleanup_ownership", [])
            if item.get("kind") == "worktree"
        ]
        collisions = sorted({path for path in managed if managed.count(path) > 1})
    return Envelope(
        "doctor",
        result={
            "python": sys.version.split()[0],
            "git": True,
            "repository": str(path),
            "base_branch": config["git"]["base_branch"],
            "base_exists": bool(git.head(config["git"]["base_branch"])),
            "clean": git.clean(),
            "config": str(config_file),
            "config_version": config["version"],
            "schemas": len(required),
            "providers": health,
            "configured_models": configured_models,
            "interrupted_runs": interrupted,
            "stale_locks": stale_locks,
            "managed_worktree_collisions": collisions,
            "write_permissions": os.access(path, os.W_OK),
            "github_cli": bool(shutil.which("gh")),
        },
        warnings=["providers are probed without consuming model usage"],
    )


def cmd_validate_result(args: argparse.Namespace) -> Envelope:
    file = Path(args.file).resolve()
    try:
        value = json.loads(file.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValidationError(f"invalid result JSON: {exc}") from exc
    schema_name = {
        "coding-agent": "coding-result.schema.json",
        "completion-verifier": "completion-result.schema.json",
        "code-reviewer": "review-result.schema.json",
        "fix-agent": "fix-result.schema.json",
    }[args.role]
    schema = Path(__file__).resolve().parents[2] / "schemas" / schema_name
    validate_json_schema(value, schema)
    if value["role"] != args.role:
        raise ValidationError(f"result role {value['role']} does not match requested {args.role}")
    return Envelope("validate-result", result={"valid": True, "role": args.role, "status": value["status"], "schema": str(schema)})


def cmd_cleanup(path: Path, args: argparse.Namespace) -> Envelope:
    store = get_store(path, args.run_id)
    value = store.load()
    owned = value.get("cleanup_ownership", [])
    removed: list[str] = []
    if not args.dry_run:
        git = GitService(path)
        for item in owned:
            candidate = Path(item["path"]).resolve()
            if item.get("kind") != "worktree" or args.run_id not in candidate.parts:
                raise ValidationError("run contains unsafe cleanup ownership record")
            if candidate.exists():
                git.remove_worktree(candidate)
                removed.append(str(candidate))
        value["cleanup_at"] = utcnow()
        atomic_json(store.state_file, value)
    return Envelope("cleanup", changed=bool(removed), result={"owned_resources": owned, "removed": removed, "dry_run": args.dry_run})


def _maybe_open_pr(path: Path, run_id: str, config: dict) -> dict:
    value = get_store(path, run_id).load()
    if value.get("state") == "EPIC_READY_FOR_INTEGRATION" and value.get("manifest", {}).get("source", {}).get("kind") == "github":
        return prepare_final_pr(path, run_id, config)
    return value


def _execution_configured(path: Path, args: argparse.Namespace) -> bool:
    return config_path(path).exists() or bool(args.provider)


def dispatch(args: argparse.Namespace) -> Envelope:
    path = repo(args)
    command_name = args.command
    if command_name == "version":
        return Envelope("version", result={"version": "2.0.0"})
    if command_name == "capabilities":
        return Envelope("capabilities", result={"version": "2.0.0", "execution_modes": ["direct-plan", "task-execution"], "commands": COMMANDS})
    if command_name in {"self-check", "doctor"}:
        return cmd_doctor(path)
    if command_name == "init-config":
        target = config_path(path)
        if target.exists():
            return Envelope("init-config", result={"path": str(target), "created": False})
        atomic_json(target, DEFAULT_CONFIG)
        return Envelope("init-config", changed=True, result={"path": str(target), "created": True}, artifacts=[{"path": str(target), "action": "created"}])
    if command_name == "validate-config":
        target, value = load_config(path)
        validate_config(value)
        return Envelope("validate-config", result={"valid": True, "path": str(target), "version": value["version"]})
    if command_name == "providers":
        _, config = load_config(path)
        names = [args.name] if args.providers_command == "probe" and args.name else list(config["providers"])
        items = [provider(name, config).probe().__dict__ for name in names]
        return Envelope(f"providers.{args.providers_command}", result={"providers": items})
    if command_name == "run":
        _, config = load_config(path, args.config)
        run_id = args.run_id or f"run-{uuid.uuid4().hex[:12]}"
        spec = Path(args.spec).resolve() if args.spec else None
        manifest_path = Path(args.manifest).resolve() if args.manifest else None
        value = prepare_run(path, spec, config, run_id, args.dry_run, args.base_branch, epic=args.epic, manifest_path=manifest_path)
        if args.dry_run:
            return Envelope("run", result={**value, "dry_run": True})
        store = get_store(path, run_id)
        persisted = store.load()
        persisted.update({"requested_provider": args.provider, "requested_model": args.model, "requested_reasoning": args.reasoning})
        atomic_json(store.state_file, persisted)
        if _execution_configured(path, args):
            execute_run(path, run_id, config)
            value = _maybe_open_pr(path, run_id, config)
        else:
            value = store.load()
        value["state_path"] = str(store.root)
        return Envelope("run", changed=True, result=value, artifacts=[{"path": str(store.root), "action": "created"}])
    if command_name == "status":
        git = GitService(path)
        runs = git.common_dir() / "project-orchestrator" / "runs"
        if args.run_id:
            return Envelope("status", result=get_store(path, args.run_id).load())
        values = [json.loads(file.read_text(encoding="utf-8")) for file in sorted(runs.glob("*/run-state.json"))] if runs.exists() else []
        return Envelope("status", result={"runs": values})
    if command_name == "cancel":
        store = get_store(path, args.run_id)
        value = store.load()
        terminated: list[int] = []
        for pid_file in store.root.glob("work-items/*/attempts/*/process.pid"):
            try:
                pid = int(pid_file.read_text().strip())
                os.kill(pid, signal.SIGTERM)
                terminated.append(pid)
            except (OSError, ValueError):
                pass
        if value["state"] not in TERMINAL and value["state"] != "PROJECT_COMPLETED":
            value = store.transition("CANCELLED", {"reason": "explicit user cancellation"})
        return Envelope("cancel", changed=True, result={**value, "terminated_pids": terminated}, warnings=["Epic worktree, candidates, and evidence are preserved"])
    if command_name in {"resume", "retry"}:
        store = get_store(path, args.run_id)
        value = store.load()
        _, config = load_config(path)
        if value["state"] in TERMINAL:
            raise ValidationError(f"cannot {command_name} terminal run in state {value['state']}")
        value = execute_run(path, args.run_id, config)
        value = _maybe_open_pr(path, args.run_id, config)
        return Envelope(command_name, changed=True, result=value)
    if command_name == "integrate":
        _, config = load_config(path)
        value = integrate_run(path, args.run_id, config)
        return Envelope("integrate", changed=True, result=value)
    if command_name == "cleanup":
        return cmd_cleanup(path, args)
    if command_name == "validate-result":
        return cmd_validate_result(args)
    raise ValidationError(f"unknown command: {command_name}")


def main(argv: list[str] | None = None) -> int:
    raw = list(sys.argv[1:] if argv is None else argv)
    if "--json" in raw:
        raw.remove("--json")
        raw.insert(0, "--json")
    args = parser().parse_args(raw)
    command_name = args.command if args.command != "providers" else f"providers.{args.providers_command}"
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
