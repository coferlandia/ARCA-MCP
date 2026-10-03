"""Pure policy evaluation for GitHub integration gates.

This module intentionally performs no GitHub, subprocess, filesystem, or run-state I/O.
It normalizes configured required gates and authoritative observations for one exact
integration-candidate SHA into a deterministic GREEN/PENDING/FAILED decision.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable

from .contracts import ValidationError

GREEN = "GREEN"
PENDING = "PENDING"
FAILED = "FAILED"

_PENDING_STATUSES = {"queued", "requested", "waiting", "pending", "in_progress"}


@dataclass(frozen=True)
class GateEvaluation:
    decision: str
    details: tuple[dict[str, Any], ...]


def integration_github_config(config: dict[str, Any]) -> dict[str, Any]:
    """Return normalized GitHub integration configuration with backward-compatible defaults."""
    integration = config.get("integration")
    if integration is None:
        return {"required_gates": [], "wait_seconds": 30, "max_wait_cycles": None}
    if not isinstance(integration, dict):
        raise ValidationError("configuration integration must be an object")
    github = integration.get("github", {})
    if not isinstance(github, dict):
        raise ValidationError("configuration integration.github must be an object")
    normalized = {
        "required_gates": github.get("required_gates", []),
        "wait_seconds": github.get("wait_seconds", 30),
        "max_wait_cycles": github.get("max_wait_cycles"),
    }
    validate_integration_config({"integration": {"github": normalized}})
    return normalized


def validate_integration_config(config: dict[str, Any]) -> None:
    integration = config.get("integration", {})
    if integration is None:
        return
    if not isinstance(integration, dict):
        raise ValidationError("configuration integration must be an object")
    github = integration.get("github", {})
    if not isinstance(github, dict):
        raise ValidationError("configuration integration.github must be an object")
    gates = github.get("required_gates", [])
    if not isinstance(gates, list):
        raise ValidationError("configuration integration.github.required_gates must be a list")
    wait_seconds = github.get("wait_seconds", 30)
    if not isinstance(wait_seconds, int) or isinstance(wait_seconds, bool) or wait_seconds <= 0:
        raise ValidationError("integration wait_seconds must be a positive integer")
    max_wait_cycles = github.get("max_wait_cycles")
    if max_wait_cycles is not None and (not isinstance(max_wait_cycles, int) or isinstance(max_wait_cycles, bool) or max_wait_cycles < 1):
        raise ValidationError("integration max_wait_cycles must be null or a positive integer")

    seen: set[str] = set()
    for gate in gates:
        if not isinstance(gate, dict):
            raise ValidationError("integration gate must be an object")
        gate_id = gate.get("id")
        if not isinstance(gate_id, str) or not gate_id.strip():
            raise ValidationError("integration gate requires non-empty id")
        if gate_id in seen:
            raise ValidationError(f"duplicate integration gate id: {gate_id}")
        seen.add(gate_id)
        kind = gate.get("kind")
        if kind not in {"workflow", "check_run"}:
            raise ValidationError(f"unsupported integration gate kind: {kind}")
        if kind == "workflow":
            workflow = gate.get("workflow")
            if not isinstance(workflow, (str, int)) or workflow == "":
                raise ValidationError(f"workflow gate {gate_id} requires workflow path or id")
        else:
            name = gate.get("name")
            if not isinstance(name, str) or not name.strip():
                raise ValidationError(f"check_run gate {gate_id} requires name")
            app = gate.get("app")
            if app is not None and (not isinstance(app, str) or not app.strip()):
                raise ValidationError(f"check_run gate {gate_id} app must be a non-empty string")
        allowed = gate.get("allowed_conclusions", ["success"])
        if not isinstance(allowed, list) or not allowed or any(not isinstance(item, str) or not item.strip() for item in allowed):
            raise ValidationError(f"integration gate {gate_id} requires allowed_conclusions")
        events = gate.get("events")
        if events is not None and (not isinstance(events, list) or not events or any(not isinstance(item, str) or not item.strip() for item in events)):
            raise ValidationError(f"integration gate {gate_id} events must be a non-empty string list")


def _matches(gate: dict[str, Any], observation: dict[str, Any], sha: str) -> bool:
    if observation.get("sha") != sha or observation.get("kind") != gate.get("kind"):
        return False
    events = gate.get("events")
    if events and observation.get("event") not in events:
        return False
    if gate["kind"] == "workflow":
        configured = str(gate.get("workflow"))
        observed = {str(observation.get("workflow", "")), str(observation.get("workflow_id", ""))}
        return configured in observed
    if observation.get("name") != gate.get("name"):
        return False
    configured_app = gate.get("app")
    return configured_app is None or observation.get("app") == configured_app


def _latest_authoritative(matches: list[dict[str, Any]]) -> tuple[dict[str, Any] | None, str | None]:
    if not matches:
        return None, "missing"
    if len(matches) == 1:
        return matches[0], None
    ranked: list[tuple[int, dict[str, Any]]] = []
    for item in matches:
        raw_id = item.get("id")
        try:
            ranked.append((int(raw_id), item))
        except (TypeError, ValueError):
            return None, "ambiguous"
    ranked.sort(key=lambda pair: pair[0], reverse=True)
    if len(ranked) > 1 and ranked[0][0] == ranked[1][0]:
        return None, "ambiguous"
    return ranked[0][1], None


def evaluate_required_gates(
    required_gates: Iterable[dict[str, Any]],
    observations: Iterable[dict[str, Any]],
    candidate_sha: str,
) -> GateEvaluation:
    gates = list(required_gates)
    obs = list(observations)
    details: list[dict[str, Any]] = []
    aggregate = GREEN
    for gate in gates:
        matches = [item for item in obs if _matches(gate, item, candidate_sha)]
        gate_id = str(gate.get("id"))
        item, match_error = _latest_authoritative(matches)
        if item is None:
            details.append({"id": gate_id, "decision": FAILED, "reason": match_error, "matches": len(matches)})
            aggregate = FAILED
            continue
        status = str(item.get("status") or "").lower()
        conclusion = item.get("conclusion")
        conclusion = str(conclusion).lower() if conclusion is not None else None
        if status in _PENDING_STATUSES:
            details.append({"id": gate_id, "decision": PENDING, "status": status, "conclusion": conclusion, "observation_id": item.get("id")})
            if aggregate != FAILED:
                aggregate = PENDING
            continue
        allowed = {str(value).lower() for value in gate.get("allowed_conclusions", ["success"])}
        if status == "completed" and conclusion in allowed:
            details.append({"id": gate_id, "decision": GREEN, "status": status, "conclusion": conclusion, "observation_id": item.get("id")})
            continue
        details.append({"id": gate_id, "decision": FAILED, "status": status, "conclusion": conclusion, "observation_id": item.get("id")})
        aggregate = FAILED
    return GateEvaluation(aggregate, tuple(details))
