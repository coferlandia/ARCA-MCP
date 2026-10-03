#!/usr/bin/env python3
"""Deterministic Product Knowledge profile/report validator and verification gate."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

EXIT_OK = 0
EXIT_INVALID = 2
EXIT_BLOCKED = 3
EXIT_STALE = 4
EXIT_ENVIRONMENT = 5

PROFILE_RELATIVE = Path(".coferlandia/product-knowledge/profile.json")
SURFACE_ID_RE = re.compile(r"^[a-z0-9][a-z0-9-]*$")
HEX64_RE = re.compile(r"^[0-9a-f]{64}$")

PROFILE_KEYS = {"schema_version", "surfaces", "evidence", "verification"}
SURFACE_KEYS = {"id", "kind", "paths", "release_policy"}
EVIDENCE_KEYS = {"github", "archivist"}
VERIFICATION_KEYS = {"require_impact_for_user_facing_changes", "block_on_required_surface"}

SURFACE_KINDS = {"repository", "external"}
RELEASE_POLICIES = {"required", "review", "optional"}
GITHUB_EVIDENCE = {"optional", "required", "disabled"}
ARCHIVIST_EVIDENCE = {"auto", "required", "disabled"}
SUBJECT_TYPES = {"issue", "pull-request", "commit", "range", "manual"}
IMPACT_DISPOSITIONS = {
    "updated",
    "verified-no-change",
    "not-applicable",
    "manual-review-required",
    "blocked",
}
AUDIT_STATUSES = {
    "covered",
    "partial",
    "missing",
    "stale",
    "contradictory",
    "not-applicable",
    "manual-review-required",
}
REQUIRED_IMPACT_RESOLVED = {"updated", "verified-no-change", "not-applicable"}
REQUIRED_AUDIT_RESOLVED = {"covered", "not-applicable"}


class ContractError(Exception):
    """Expected invalid profile/report contract."""


class StaleEvidenceError(Exception):
    """Evidence is structurally valid enough to identify as stale."""


def _canonical_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def _fingerprint(value: Any) -> str:
    return hashlib.sha256(_canonical_bytes(value)).hexdigest()


def _read_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except FileNotFoundError as exc:
        raise ContractError(f"JSON file does not exist: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ContractError(
            f"Invalid JSON in {path}: line {exc.lineno} column {exc.colno}: {exc.msg}"
        ) from exc
    except OSError as exc:
        raise OSError(f"Cannot read {path}: {exc}") from exc


def _reject_unknown_keys(value: dict[str, Any], allowed: set[str], where: str) -> None:
    unknown = sorted(set(value) - allowed)
    if unknown:
        raise ContractError(f"{where} contains unknown field(s): {', '.join(unknown)}")


def _require_nonempty_string(value: Any, where: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ContractError(f"{where} must be a non-empty string")
    return value


def _require_bool(value: Any, where: str) -> bool:
    if not isinstance(value, bool):
        raise ContractError(f"{where} must be boolean")
    return value


def _validate_repo_pattern(pattern: Any, where: str) -> str:
    normalized = _require_nonempty_string(pattern, where).replace("\\", "/")
    candidate = Path(normalized)
    if candidate.is_absolute() or normalized.startswith("/"):
        raise ContractError(f"{where} must be repository-relative: {normalized}")
    if ".." in candidate.parts:
        raise ContractError(f"{where} may not escape the project root: {normalized}")
    return normalized


def _pattern_exists(project_root: Path, pattern: str) -> bool:
    try:
        return any(project_root.glob(pattern))
    except (OSError, ValueError) as exc:
        raise OSError(f"Cannot evaluate repository surface pattern '{pattern}': {exc}") from exc


def validate_profile(profile: Any, project_root: Path, check_paths: bool = True) -> dict[str, Any]:
    if not isinstance(profile, dict):
        raise ContractError("profile must be a JSON object")
    _reject_unknown_keys(profile, PROFILE_KEYS, "profile")
    if profile.get("schema_version") != 1:
        raise ContractError("profile.schema_version must equal 1")

    surfaces = profile.get("surfaces")
    if not isinstance(surfaces, list) or not surfaces:
        raise ContractError("profile.surfaces must be a non-empty list")

    normalized_surfaces: list[dict[str, Any]] = []
    seen: set[str] = set()
    required_count = 0
    for index, raw in enumerate(surfaces):
        where = f"profile.surfaces[{index}]"
        if not isinstance(raw, dict):
            raise ContractError(f"{where} must be an object")
        _reject_unknown_keys(raw, SURFACE_KEYS, where)

        surface_id = _require_nonempty_string(raw.get("id"), f"{where}.id")
        if not SURFACE_ID_RE.fullmatch(surface_id):
            raise ContractError(
                f"{where}.id must use lowercase letters, digits and hyphens: {surface_id}"
            )
        if surface_id in seen:
            raise ContractError(f"duplicate surface id: {surface_id}")
        seen.add(surface_id)

        kind = raw.get("kind")
        if kind not in SURFACE_KINDS:
            raise ContractError(f"{where}.kind must be one of: {', '.join(sorted(SURFACE_KINDS))}")
        policy = raw.get("release_policy")
        if policy not in RELEASE_POLICIES:
            raise ContractError(
                f"{where}.release_policy must be one of: {', '.join(sorted(RELEASE_POLICIES))}"
            )
        if policy == "required":
            required_count += 1

        normalized_surface: dict[str, Any] = {
            "id": surface_id,
            "kind": kind,
            "release_policy": policy,
        }
        if kind == "repository":
            paths = raw.get("paths")
            if not isinstance(paths, list) or not paths:
                raise ContractError(f"{where}.paths must be a non-empty list for repository surfaces")
            normalized_paths = [_validate_repo_pattern(item, f"{where}.paths") for item in paths]
            normalized_surface["paths"] = normalized_paths
            if (
                check_paths
                and policy == "required"
                and not any(_pattern_exists(project_root, pattern) for pattern in normalized_paths)
            ):
                raise ContractError(
                    f"required repository surface '{surface_id}' matches no paths under {project_root}: "
                    + ", ".join(normalized_paths)
                )
        elif "paths" in raw:
            raise ContractError(f"{where}.paths is forbidden for external surfaces")
        normalized_surfaces.append(normalized_surface)

    evidence = profile.get("evidence", {})
    if not isinstance(evidence, dict):
        raise ContractError("profile.evidence must be an object")
    _reject_unknown_keys(evidence, EVIDENCE_KEYS, "profile.evidence")
    if "github" in evidence and evidence["github"] not in GITHUB_EVIDENCE:
        raise ContractError("profile.evidence.github must be optional|required|disabled")
    if "archivist" in evidence and evidence["archivist"] not in ARCHIVIST_EVIDENCE:
        raise ContractError("profile.evidence.archivist must be auto|required|disabled")

    verification = profile.get("verification", {})
    if not isinstance(verification, dict):
        raise ContractError("profile.verification must be an object")
    _reject_unknown_keys(verification, VERIFICATION_KEYS, "profile.verification")
    for key, value in verification.items():
        _require_bool(value, f"profile.verification.{key}")
    if required_count and verification.get("block_on_required_surface") is False:
        raise ContractError(
            "profile.verification.block_on_required_surface cannot be false when required surfaces exist"
        )

    normalized: dict[str, Any] = {"schema_version": 1, "surfaces": normalized_surfaces}
    if evidence:
        normalized["evidence"] = dict(sorted(evidence.items()))
    if verification:
        normalized["verification"] = dict(sorted(verification.items()))
    return normalized


def _load_profile(
    project_root: Path, required: bool = True
) -> tuple[dict[str, Any] | None, str | None]:
    path = project_root / PROFILE_RELATIVE
    if not path.exists():
        if required:
            raise ContractError(f"Product Knowledge profile not found: {path}")
        return None, None
    normalized = validate_profile(_read_json(path), project_root, check_paths=True)
    return normalized, _fingerprint(normalized)


def _validate_subject(raw: Any, where: str) -> dict[str, str]:
    if not isinstance(raw, dict):
        raise ContractError(f"{where} must be an object")
    _reject_unknown_keys(raw, {"type", "reference"}, where)
    subject_type = raw.get("type")
    if subject_type not in SUBJECT_TYPES:
        raise ContractError(f"{where}.type must be one of: {', '.join(sorted(SUBJECT_TYPES))}")
    return {
        "type": subject_type,
        "reference": _require_nonempty_string(raw.get("reference"), f"{where}.reference"),
    }


def _validate_candidate_fields(report: dict[str, Any], where: str) -> None:
    if "candidate" in report:
        _require_nonempty_string(report["candidate"], f"{where}.candidate")
    if "profile_fingerprint" in report:
        fingerprint = _require_nonempty_string(
            report["profile_fingerprint"], f"{where}.profile_fingerprint"
        )
        if not HEX64_RE.fullmatch(fingerprint):
            raise ContractError(f"{where}.profile_fingerprint must be a lowercase SHA-256 hex string")


def _validate_references(value: Any, where: str) -> list[str]:
    if value is None:
        return []
    if not isinstance(value, list):
        raise ContractError(f"{where} must be a list")
    return [
        _require_nonempty_string(entry, f"{where}[{index}]")
        for index, entry in enumerate(value)
    ]


def _validate_handoffs(value: Any, where: str) -> list[dict[str, Any]]:
    if value is None:
        return []
    if not isinstance(value, list):
        raise ContractError(f"{where} must be a list")
    result: list[dict[str, Any]] = []
    allowed = {"owner", "type", "summary", "blocking"}
    for index, raw in enumerate(value):
        item_where = f"{where}[{index}]"
        if not isinstance(raw, dict):
            raise ContractError(f"{item_where} must be an object")
        _reject_unknown_keys(raw, allowed, item_where)
        result.append(
            {
                "owner": _require_nonempty_string(raw.get("owner"), f"{item_where}.owner"),
                "type": _require_nonempty_string(raw.get("type"), f"{item_where}.type"),
                "summary": _require_nonempty_string(raw.get("summary"), f"{item_where}.summary"),
                "blocking": _require_bool(raw.get("blocking"), f"{item_where}.blocking"),
            }
        )
    return result


def _profile_surface_map(profile: dict[str, Any] | None) -> dict[str, dict[str, Any]]:
    if not profile:
        return {}
    return {surface["id"]: surface for surface in profile["surfaces"]}


def validate_impact(report: Any, profile: dict[str, Any] | None) -> dict[str, Any]:
    if not isinstance(report, dict):
        raise ContractError("impact report must be an object")
    _reject_unknown_keys(
        report,
        {
            "schema_version",
            "mode",
            "subject",
            "candidate",
            "profile_fingerprint",
            "user_facing",
            "rationale",
            "behaviors",
            "surfaces",
            "handoffs",
        },
        "impact",
    )
    if report.get("schema_version") != 1 or report.get("mode") != "impact":
        raise ContractError("impact report requires schema_version=1 and mode='impact'")
    _validate_candidate_fields(report, "impact")

    subject = _validate_subject(report.get("subject"), "impact.subject")
    user_facing = _require_bool(report.get("user_facing"), "impact.user_facing")
    rationale = _require_nonempty_string(report.get("rationale"), "impact.rationale")

    behaviors = report.get("behaviors")
    if not isinstance(behaviors, list):
        raise ContractError("impact.behaviors must be a list")
    if user_facing and not behaviors:
        raise ContractError("impact.behaviors must be non-empty when user_facing=true")

    normalized_behaviors: list[dict[str, Any]] = []
    behavior_ids: set[str] = set()
    for index, raw in enumerate(behaviors):
        where = f"impact.behaviors[{index}]"
        if not isinstance(raw, dict):
            raise ContractError(f"{where} must be an object")
        _reject_unknown_keys(raw, {"id", "summary", "evidence"}, where)
        behavior_id = _require_nonempty_string(raw.get("id"), f"{where}.id")
        if behavior_id in behavior_ids:
            raise ContractError(f"duplicate impact behavior id: {behavior_id}")
        behavior_ids.add(behavior_id)
        normalized_behaviors.append(
            {
                "id": behavior_id,
                "summary": _require_nonempty_string(raw.get("summary"), f"{where}.summary"),
                "evidence": _validate_references(raw.get("evidence"), f"{where}.evidence"),
            }
        )

    surfaces = report.get("surfaces")
    if not isinstance(surfaces, list):
        raise ContractError("impact.surfaces must be a list")
    profile_map = _profile_surface_map(profile)
    normalized_surfaces: list[dict[str, Any]] = []
    seen: set[str] = set()
    for index, raw in enumerate(surfaces):
        where = f"impact.surfaces[{index}]"
        if not isinstance(raw, dict):
            raise ContractError(f"{where} must be an object")
        _reject_unknown_keys(raw, {"id", "disposition", "references", "reason"}, where)
        surface_id = _require_nonempty_string(raw.get("id"), f"{where}.id")
        if surface_id in seen:
            raise ContractError(f"duplicate impact surface id: {surface_id}")
        seen.add(surface_id)
        if profile_map and surface_id not in profile_map:
            raise ContractError(f"impact references unknown profile surface: {surface_id}")

        disposition = raw.get("disposition")
        if disposition not in IMPACT_DISPOSITIONS:
            raise ContractError(f"{where}.disposition is invalid: {disposition}")
        references = _validate_references(raw.get("references"), f"{where}.references")
        reason = raw.get("reason")
        if disposition in {
            "verified-no-change",
            "not-applicable",
            "manual-review-required",
            "blocked",
        }:
            reason = _require_nonempty_string(reason, f"{where}.reason")
        elif reason is not None and not isinstance(reason, str):
            raise ContractError(f"{where}.reason must be string or null")
        normalized_surfaces.append(
            {
                "id": surface_id,
                "disposition": disposition,
                "references": references,
                "reason": reason,
            }
        )

    if profile_map and user_facing:
        missing = sorted(set(profile_map) - seen)
        if missing:
            raise ContractError("impact is missing configured surface(s): " + ", ".join(missing))

    normalized: dict[str, Any] = {
        "schema_version": 1,
        "mode": "impact",
        "subject": subject,
        "user_facing": user_facing,
        "rationale": rationale,
        "behaviors": normalized_behaviors,
        "surfaces": normalized_surfaces,
        "handoffs": _validate_handoffs(report.get("handoffs"), "impact.handoffs"),
    }
    for optional in ("candidate", "profile_fingerprint"):
        if optional in report:
            normalized[optional] = report[optional]
    return normalized


def validate_audit(report: Any, profile: dict[str, Any] | None) -> dict[str, Any]:
    if not isinstance(report, dict):
        raise ContractError("audit report must be an object")
    _reject_unknown_keys(
        report,
        {
            "schema_version",
            "mode",
            "subject",
            "candidate",
            "profile_fingerprint",
            "surfaces",
            "handoffs",
        },
        "audit",
    )
    if report.get("schema_version") != 1 or report.get("mode") != "audit":
        raise ContractError("audit report requires schema_version=1 and mode='audit'")
    _validate_candidate_fields(report, "audit")

    subject = _validate_subject(report.get("subject"), "audit.subject")
    surfaces = report.get("surfaces")
    if not isinstance(surfaces, list):
        raise ContractError("audit.surfaces must be a list")
    profile_map = _profile_surface_map(profile)
    normalized_surfaces: list[dict[str, Any]] = []
    seen: set[str] = set()
    for index, raw in enumerate(surfaces):
        where = f"audit.surfaces[{index}]"
        if not isinstance(raw, dict):
            raise ContractError(f"{where} must be an object")
        _reject_unknown_keys(raw, {"id", "status", "references", "reason"}, where)
        surface_id = _require_nonempty_string(raw.get("id"), f"{where}.id")
        if surface_id in seen:
            raise ContractError(f"duplicate audit surface id: {surface_id}")
        seen.add(surface_id)
        if profile_map and surface_id not in profile_map:
            raise ContractError(f"audit references unknown profile surface: {surface_id}")

        status = raw.get("status")
        if status not in AUDIT_STATUSES:
            raise ContractError(f"{where}.status is invalid: {status}")
        normalized_surfaces.append(
            {
                "id": surface_id,
                "status": status,
                "references": _validate_references(raw.get("references"), f"{where}.references"),
                "reason": _require_nonempty_string(raw.get("reason"), f"{where}.reason"),
            }
        )

    if profile_map:
        missing = sorted(set(profile_map) - seen)
        if missing:
            raise ContractError("audit is missing configured surface(s): " + ", ".join(missing))

    normalized: dict[str, Any] = {
        "schema_version": 1,
        "mode": "audit",
        "subject": subject,
        "surfaces": normalized_surfaces,
        "handoffs": _validate_handoffs(report.get("handoffs"), "audit.handoffs"),
    }
    for optional in ("candidate", "profile_fingerprint"):
        if optional in report:
            normalized[optional] = report[optional]
    return normalized


def _validate_report(raw: Any, profile: dict[str, Any] | None) -> dict[str, Any]:
    if not isinstance(raw, dict):
        raise ContractError("report must be an object")
    if raw.get("mode") == "impact":
        return validate_impact(raw, profile)
    if raw.get("mode") == "audit":
        return validate_audit(raw, profile)
    raise ContractError(f"report.mode must be 'impact' or 'audit', got: {raw.get('mode')!r}")


def _require_current_identity(
    raw: Any,
    candidate: str,
    profile_fingerprint: str,
    path: Path,
) -> None:
    """Classify candidate/profile mismatch before current-profile shape validation."""
    if not isinstance(raw, dict):
        raise ContractError(f"report {path} must be an object")
    report_candidate = raw.get("candidate")
    if report_candidate != candidate:
        raise StaleEvidenceError(
            f"report {path} candidate mismatch: expected '{candidate}', got '{report_candidate}'"
        )
    report_profile_fingerprint = raw.get("profile_fingerprint")
    if report_profile_fingerprint != profile_fingerprint:
        raise StaleEvidenceError(
            f"report {path} profile fingerprint mismatch: expected {profile_fingerprint}, "
            f"got {report_profile_fingerprint}"
        )


def verify(project_root: Path, candidate: str, input_paths: list[Path]) -> dict[str, Any]:
    candidate = _require_nonempty_string(candidate, "candidate")
    profile, profile_fingerprint = _load_profile(project_root, required=True)
    assert profile is not None and profile_fingerprint is not None
    if not input_paths:
        raise ContractError("verify requires at least one --input report")

    surface_map = _profile_surface_map(profile)
    required_ids = {
        surface_id
        for surface_id, surface in surface_map.items()
        if surface["release_policy"] == "required"
    }
    policy_totals = {policy: 0 for policy in sorted(RELEASE_POLICIES)}
    for surface in surface_map.values():
        policy_totals[surface["release_policy"]] += 1

    blockers: list[dict[str, str]] = []
    report_fingerprints: list[dict[str, str]] = []
    manual_review_pending = 0
    user_facing_changes = 0
    resolved_entries = 0

    for path in input_paths:
        raw = _read_json(path)
        _require_current_identity(raw, candidate, profile_fingerprint, path)
        report = _validate_report(raw, profile)
        report_fingerprints.append({"path": str(path), "sha256": _fingerprint(report)})
        if report["mode"] == "impact" and report["user_facing"]:
            user_facing_changes += 1

        for surface in report["surfaces"]:
            surface_id = surface["id"]
            policy = surface_map[surface_id]["release_policy"]
            state = surface["disposition"] if report["mode"] == "impact" else surface["status"]
            if state == "manual-review-required":
                manual_review_pending += 1
            if policy == "required":
                resolved_states = (
                    REQUIRED_IMPACT_RESOLVED
                    if report["mode"] == "impact"
                    else REQUIRED_AUDIT_RESOLVED
                )
                if state not in resolved_states:
                    blockers.append(
                        {
                            "report": str(path),
                            "surface": surface_id,
                            "state": state,
                            "mode": report["mode"],
                        }
                    )
                else:
                    resolved_entries += 1
            else:
                resolved_entries += 1

        for handoff in report["handoffs"]:
            if handoff["blocking"]:
                blockers.append(
                    {
                        "report": str(path),
                        "surface": "handoff",
                        "state": handoff["type"],
                        "mode": report["mode"],
                    }
                )

    report_fingerprints.sort(key=lambda item: (item["path"], item["sha256"]))
    blockers.sort(key=lambda item: (item["report"], item["surface"], item["state"], item["mode"]))
    unresolved_required_surfaces = sum(
        1 for blocker in blockers if blocker["surface"] != "handoff"
    )
    blocking_handoffs = sum(1 for blocker in blockers if blocker["surface"] == "handoff")
    result = "BLOCKED" if blockers else "PASS"
    return {
        "schema_version": 1,
        "mode": "verify",
        "candidate": candidate,
        "profile_fingerprint": profile_fingerprint,
        "reports": report_fingerprints,
        "surface_policy_totals": policy_totals,
        "required_surface_ids": sorted(required_ids),
        "user_facing_changes": user_facing_changes,
        "resolved_entries": resolved_entries,
        "unresolved_required_surfaces": unresolved_required_surfaces,
        "blocking_handoffs": blocking_handoffs,
        "manual_review_pending": manual_review_pending,
        "blockers": blockers,
        "result": result,
    }


def _emit(payload: Any, as_json: bool) -> None:
    if as_json:
        print(json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False))
        return
    if isinstance(payload, dict):
        for key in sorted(payload):
            value = payload[key]
            if isinstance(value, (dict, list)):
                value = json.dumps(value, sort_keys=True, ensure_ascii=False)
            print(f"{key}: {value}")
        return
    print(payload)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="product-knowledge-sync-cli.py",
        description="Validate Product Knowledge profiles/reports and verify candidate-bound anti-drift evidence.",
    )
    commands = parser.add_subparsers(dest="command", required=True)

    profile = commands.add_parser("profile", help="Profile operations")
    profile_subcommands = profile.add_subparsers(dest="profile_command", required=True)
    profile_validate = profile_subcommands.add_parser("validate", help="Validate project profile")
    profile_validate.add_argument("--project-root", required=True)
    profile_validate.add_argument("--json", action="store_true")

    for name in ("impact", "audit"):
        mode_parser = commands.add_parser(name, help=f"{name.capitalize()} report operations")
        mode_subcommands = mode_parser.add_subparsers(dest=f"{name}_command", required=True)
        validate_parser = mode_subcommands.add_parser("validate", help=f"Validate {name} report")
        validate_parser.add_argument("--input", required=True)
        validate_parser.add_argument("--project-root", required=True)
        validate_parser.add_argument("--json", action="store_true")

    fingerprint = commands.add_parser("fingerprint", help="Print canonical project profile SHA-256")
    fingerprint.add_argument("--project-root", required=True)
    fingerprint.add_argument("--json", action="store_true")

    verification = commands.add_parser("verify", help="Verify candidate-bound Product Knowledge evidence")
    verification.add_argument("--project-root", required=True)
    verification.add_argument("--candidate", required=True)
    verification.add_argument("--input", action="append", required=True)
    verification.add_argument("--json", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)
    try:
        project_root = Path(args.project_root).resolve()
        if not project_root.exists() or not project_root.is_dir():
            raise OSError(f"project root is not an existing directory: {project_root}")

        if args.command == "profile":
            profile, fingerprint = _load_profile(project_root, required=True)
            assert profile is not None and fingerprint is not None
            _emit(
                {
                    "valid": True,
                    "profile_fingerprint": fingerprint,
                    "surfaces": len(profile["surfaces"]),
                },
                args.json,
            )
            return EXIT_OK

        if args.command == "fingerprint":
            _profile, fingerprint = _load_profile(project_root, required=True)
            assert fingerprint is not None
            _emit({"profile_fingerprint": fingerprint}, args.json)
            return EXIT_OK

        if args.command in {"impact", "audit"}:
            profile, profile_fingerprint = _load_profile(project_root, required=False)
            raw = _read_json(Path(args.input))
            report = validate_impact(raw, profile) if args.command == "impact" else validate_audit(raw, profile)
            _emit(
                {
                    "valid": True,
                    "mode": args.command,
                    "profile_fingerprint": profile_fingerprint,
                    "report_fingerprint": _fingerprint(report),
                    "profile_configured": profile is not None,
                },
                args.json,
            )
            return EXIT_OK

        if args.command == "verify":
            result = verify(project_root, args.candidate, [Path(value) for value in args.input])
            _emit(result, args.json)
            return EXIT_BLOCKED if result["result"] == "BLOCKED" else EXIT_OK

        parser.error("unhandled command")
        return EXIT_INVALID
    except StaleEvidenceError as exc:
        print(f"STALE: {exc}", file=sys.stderr)
        return EXIT_STALE
    except ContractError as exc:
        print(f"INVALID: {exc}", file=sys.stderr)
        return EXIT_INVALID
    except OSError as exc:
        print(f"ENVIRONMENT: {exc}", file=sys.stderr)
        return EXIT_ENVIRONMENT


if __name__ == "__main__":
    raise SystemExit(main())
