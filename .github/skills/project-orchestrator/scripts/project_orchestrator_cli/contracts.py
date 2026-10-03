"""Stable output and error contracts for project-orchestrator."""
from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Any
import json
from pathlib import Path

try:
    from jsonschema import Draft202012Validator
except ImportError:  # pragma: no cover - doctor reports this dependency
    Draft202012Validator = None


EXIT_OPERATIONAL = 1
EXIT_ARGUMENTS = 2
EXIT_VALIDATION = 3
EXIT_DEPENDENCY = 4
EXIT_UNSAFE = 5
EXIT_PARTIAL = 6


class OrchestratorError(Exception):
    code = EXIT_OPERATIONAL


class ValidationError(OrchestratorError):
    code = EXIT_VALIDATION


class DependencyError(OrchestratorError):
    code = EXIT_DEPENDENCY


class UnsafeOperation(OrchestratorError):
    code = EXIT_UNSAFE


def validate_json_schema(value: Any, schema_path: Path) -> None:
    if Draft202012Validator is None:
        raise DependencyError("jsonschema is required for protocol validation")
    try:
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValidationError(f"cannot read schema {schema_path}: {exc}") from exc
    errors = sorted(Draft202012Validator(schema).iter_errors(value), key=lambda e: list(e.path))
    if errors:
        details = "; ".join(f"{'.'.join(map(str, e.path)) or '<root>'}: {e.message}" for e in errors[:8])
        raise ValidationError(f"schema validation failed: {details}")


@dataclass
class Envelope:
    command: str
    changed: bool = False
    result: dict[str, Any] = field(default_factory=dict)
    artifacts: list[dict[str, str]] = field(default_factory=list)
    warnings: list[Any] = field(default_factory=list)
    errors: list[Any] = field(default_factory=list)
    status: str = "success"

    def payload(self) -> dict[str, Any]:
        return {"status": self.status, "skill": "project-orchestrator", **asdict(self)}


def failure(command: str, error: Exception) -> Envelope:
    return Envelope(command=command, status="failure", errors=[str(error)])
