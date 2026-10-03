from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from . import CLI_VERSION
from .model import ToolsmithError, load_data, validate_contract
from .operations import (
    check_drift,
    decide_candidate,
    find_candidate,
    generate_docs,
    generate_facade,
    inspect_contract,
    load_candidates,
    mark_candidate_implemented,
    self_check,
)


def envelope(
    command: str,
    *,
    status: str = "success",
    changed: bool = False,
    result: Any | None = None,
    artifacts: list[dict[str, Any]] | None = None,
    warnings: list[Any] | None = None,
    errors: list[Any] | None = None,
) -> dict[str, Any]:
    return {
        "status": status,
        "skill": "coferlandia-config-toolsmith",
        "command": command,
        "changed": changed,
        "result": result if result is not None else {},
        "artifacts": artifacts or [],
        "warnings": warnings or [],
        "errors": errors or [],
    }


def emit(payload: dict[str, Any], *, compact: bool = False) -> None:
    print(json.dumps(payload, ensure_ascii=False, sort_keys=True, indent=None if compact else 2))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Deterministic Config Toolsmith operations.")
    parser.add_argument("--compact", action="store_true", help="emit compact JSON")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("version")
    commands.add_parser("capabilities")
    self_parser = commands.add_parser("self-check")
    self_parser.add_argument("--repo", type=Path)

    contract = commands.add_parser("contract")
    contract_commands = contract.add_subparsers(dest="contract_command", required=True)
    for name in ("validate", "inspect"):
        sub = contract_commands.add_parser(name)
        sub.add_argument("--contract", required=True, type=Path)

    candidates = commands.add_parser("candidates")
    candidate_commands = candidates.add_subparsers(dest="candidate_command", required=True)
    list_cmd = candidate_commands.add_parser("list")
    list_cmd.add_argument("--candidates", required=True, type=Path)
    show_cmd = candidate_commands.add_parser("show")
    show_cmd.add_argument("candidate_id")
    show_cmd.add_argument("--candidates", required=True, type=Path)
    for action in ("approve", "reject", "defer", "intentionally-unmanaged"):
        sub = candidate_commands.add_parser(action)
        sub.add_argument("candidate_id")
        sub.add_argument("--candidates", required=True, type=Path)
        sub.add_argument("--contract", required=True, type=Path)
        sub.add_argument("--decisions", required=True, type=Path)
        sub.add_argument("--expected-fingerprint")
        sub.add_argument("--dry-run", action="store_true")
        if action == "approve":
            sub.add_argument("--target-root", required=True, type=Path)
            sub.add_argument("--output-dir", required=True, type=Path)
            sub.add_argument(
                "--platform", choices=("auto", "python", "dotnet", "fallback-python"), default="auto"
            )

    docs = commands.add_parser("docs")
    docs_commands = docs.add_subparsers(dest="docs_command", required=True)
    docs_generate = docs_commands.add_parser("generate")
    docs_generate.add_argument("--contract", required=True, type=Path)
    docs_generate.add_argument("--output-dir", required=True, type=Path)
    docs_generate.add_argument("--dry-run", action="store_true")

    generate = commands.add_parser("generate")
    generate.add_argument("--contract", required=True, type=Path)
    generate.add_argument("--target-root", required=True, type=Path)
    generate.add_argument(
        "--platform", choices=("auto", "python", "dotnet", "fallback-python"), default="auto"
    )
    generate.add_argument("--dry-run", action="store_true")

    drift = commands.add_parser("drift")
    drift_commands = drift.add_subparsers(dest="drift_command", required=True)
    drift_check = drift_commands.add_parser("check")
    drift_check.add_argument("--contract", required=True, type=Path)
    drift_check.add_argument("--output-dir", required=True, type=Path)
    return parser


def capabilities() -> dict[str, Any]:
    return {
        "version": CLI_VERSION,
        "commands": [
            {"name": "contract.validate", "mutating": False, "supports_json": True},
            {"name": "contract.inspect", "mutating": False, "supports_json": True},
            {"name": "candidates.list", "mutating": False, "supports_json": True},
            {"name": "candidates.show", "mutating": False, "supports_json": True},
            {"name": "candidates.approve", "mutating": True, "supports_dry_run": True},
            {"name": "candidates.reject", "mutating": True, "supports_dry_run": True},
            {"name": "candidates.defer", "mutating": True, "supports_dry_run": True},
            {"name": "docs.generate", "mutating": True, "supports_dry_run": True},
            {"name": "generate", "mutating": True, "supports_dry_run": True},
            {"name": "drift.check", "mutating": False, "supports_json": True},
        ],
        "contract_schema_version": 1,
        "platforms": ["python", "dotnet", "fallback-python"],
    }


def require_valid_contract(path: Path) -> tuple[dict[str, Any], list[str]]:
    contract = load_data(path)
    validation = validate_contract(contract)
    if not validation.valid:
        raise ToolsmithError("contract validation failed", code=3, details=validation.errors)
    return contract, validation.warnings


def run(args: argparse.Namespace) -> tuple[dict[str, Any], int]:
    if args.command == "version":
        return envelope("version", result={"version": CLI_VERSION}), 0
    if args.command == "capabilities":
        return envelope("capabilities", result=capabilities()), 0
    if args.command == "self-check":
        result = self_check(args.repo)
        code = 0 if result["healthy"] else 4
        return envelope("self-check", status="success" if code == 0 else "failure", result=result, errors=[] if code == 0 else result["missing"]), code
    if args.command == "contract":
        contract = load_data(args.contract)
        validation = validate_contract(contract)
        if args.contract_command == "validate":
            code = 0 if validation.valid else 3
            return envelope(
                "contract.validate",
                status="success" if validation.valid else "failure",
                result={"valid": validation.valid},
                warnings=validation.warnings,
                errors=validation.errors,
            ), code
        if not validation.valid:
            raise ToolsmithError("contract validation failed", code=3, details=validation.errors)
        return envelope("contract.inspect", result=inspect_contract(contract), warnings=validation.warnings), 0
    if args.command == "candidates":
        ledger = load_candidates(args.candidates)
        if args.candidate_command == "list":
            return envelope("candidates.list", result=ledger), 0
        if args.candidate_command == "show":
            return envelope("candidates.show", result=find_candidate(ledger, args.candidate_id)), 0
        result = decide_candidate(
            action=args.candidate_command,
            candidate_id=args.candidate_id,
            candidates_path=args.candidates,
            contract_path=args.contract,
            decisions_path=args.decisions,
            expected_fingerprint=args.expected_fingerprint,
            dry_run=args.dry_run,
        )
        if args.candidate_command == "approve" and not args.dry_run:
            contract, warnings = require_valid_contract(args.contract)
            facade = generate_facade(contract, args.target_root, args.platform, dry_run=False)
            docs = generate_docs(contract, args.output_dir, dry_run=False)
            drift = check_drift(contract, args.output_dir)
            if not drift["clean"]:
                raise ToolsmithError("generated documentation drifted immediately after approval", code=3, details=drift["drift"])
            mark_candidate_implemented(args.candidates, args.candidate_id)
            result["status"] = "implemented"
            result["regenerated"] = {"facade": facade, "docs": docs, "drift": drift}
            result["artifacts"].extend(facade["artifacts"] + docs["artifacts"])
            result["warnings"] = warnings
        return envelope(
            f"candidates.{args.candidate_command}",
            changed=(result["contract_changed"] or args.candidate_command == "approve") and not args.dry_run,
            result=result,
            artifacts=result["artifacts"],
            warnings=result.get("warnings", []),
        ), 0
    if args.command == "docs":
        contract, warnings = require_valid_contract(args.contract)
        result = generate_docs(contract, args.output_dir, dry_run=args.dry_run)
        return envelope(
            "docs.generate",
            changed=not args.dry_run,
            result=result,
            artifacts=result["artifacts"],
            warnings=warnings,
        ), 0
    if args.command == "generate":
        contract, warnings = require_valid_contract(args.contract)
        result = generate_facade(contract, args.target_root, args.platform, dry_run=args.dry_run)
        return envelope(
            "generate",
            changed=not args.dry_run,
            result=result,
            artifacts=result["artifacts"],
            warnings=warnings,
        ), 0
    if args.command == "drift":
        contract, warnings = require_valid_contract(args.contract)
        result = check_drift(contract, args.output_dir)
        code = 0 if result["clean"] else 3
        return envelope(
            "drift.check",
            status="success" if code == 0 else "failure",
            result=result,
            warnings=warnings,
            errors=[] if code == 0 else result["drift"],
        ), code
    raise ToolsmithError(f"unsupported command: {args.command}", code=2)


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    values = list(sys.argv[1:] if argv is None else argv)
    values = [value for value in values if value != "--json"]
    args = parser.parse_args(values)
    try:
        payload, code = run(args)
    except ToolsmithError as exc:
        payload = envelope(
            getattr(args, "command", "unknown"),
            status="failure",
            errors=[{"message": str(exc), "details": exc.details}],
        )
        code = exc.code
    except Exception as exc:  # pragma: no cover - defensive boundary
        payload = envelope(
            getattr(args, "command", "unknown"),
            status="failure",
            errors=[{"message": f"unexpected failure: {exc}"}],
        )
        code = 1
    emit(payload, compact=args.compact)
    return code


if __name__ == "__main__":
    raise SystemExit(main())
