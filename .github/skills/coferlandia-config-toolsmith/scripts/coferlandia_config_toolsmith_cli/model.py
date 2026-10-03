from __future__ import annotations

import hashlib
import json
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

from . import CONTRACT_SCHEMA_VERSION

FORBIDDEN_STATE_KEYS = {
    "current_value",
    "effective_value",
    "last_seen_value",
    "production_value",
    "staging_value",
    "development_value",
    "environment_values",
    "secret_value",
    "value_snapshot",
    "config_snapshot",
    "configuration_snapshot",
    "runtime_value",
}

SUPPORTED_TYPES = {"string", "integer", "number", "boolean", "enum", "array", "object"}
STANDARD_ADAPTERS = {
    "env",
    "dotenv",
    "json",
    "toml",
    "python-api",
    "dotnet-options",
    "command",
    "database",
    "remote",
    "custom",
}


class ToolsmithError(Exception):
    """Expected operational failure with a stable exit code."""

    def __init__(self, message: str, *, code: int = 1, details: Any | None = None):
        super().__init__(message)
        self.code = code
        self.details = details


@dataclass(frozen=True)
class ValidationResult:
    errors: list[str]
    warnings: list[str]

    @property
    def valid(self) -> bool:
        return not self.errors


def load_data(path: Path) -> Any:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as exc:
        raise ToolsmithError(f"cannot read {path}: {exc}", code=1) from exc
    try:
        return json.loads(text)
    except json.JSONDecodeError as json_exc:
        try:
            import yaml  # type: ignore
        except ImportError as exc:
            raise ToolsmithError(
                f"{path} is not JSON-compatible YAML; install PyYAML for full YAML support: {json_exc}",
                code=3,
            ) from exc
        try:
            return yaml.safe_load(text)
        except Exception as exc:  # pragma: no cover - optional dependency path
            raise ToolsmithError(f"invalid YAML in {path}: {exc}", code=3) from exc


def dump_data(data: Any) -> str:
    return json.dumps(data, indent=2, ensure_ascii=False, sort_keys=True) + "\n"


def atomic_write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temp_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temp_name, path)
    except Exception:
        try:
            os.unlink(temp_name)
        except OSError:
            pass
        raise


def file_fingerprint(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return f"sha256:{digest.hexdigest()}"


def stable_fingerprint(value: Any) -> str:
    raw = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return f"sha256:{hashlib.sha256(raw.encode('utf-8')).hexdigest()}"


def _walk_forbidden(value: Any, path: str, errors: list[str]) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            child_path = f"{path}.{key}" if path else str(key)
            if str(key).lower() in FORBIDDEN_STATE_KEYS:
                errors.append(f"forbidden state-bearing key: {child_path}")
            _walk_forbidden(child, child_path, errors)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _walk_forbidden(child, f"{path}[{index}]", errors)


def iter_fields(contract: dict[str, Any]) -> Iterable[tuple[dict[str, Any], dict[str, Any]]]:
    for module in contract.get("modules", []):
        if not isinstance(module, dict):
            continue
        for field in module.get("fields", []):
            if isinstance(field, dict):
                yield module, field


def validate_field(field: dict[str, Any], *, module_name: str | None = None) -> ValidationResult:
    errors: list[str] = []
    warnings: list[str] = []
    required = ("key", "description", "type", "writable", "secret", "binding")
    for name in required:
        if name not in field:
            errors.append(f"field missing required key: {name}")
    key = field.get("key")
    if not isinstance(key, str) or not key.strip():
        errors.append("field key must be a non-empty string")
    elif module_name and not key.startswith(f"{module_name}."):
        warnings.append(f"field {key} does not use module prefix {module_name}.")
    field_type = field.get("type")
    if field_type not in SUPPORTED_TYPES:
        errors.append(f"field {key or '<unknown>'} has unsupported type: {field_type}")
    if not isinstance(field.get("writable"), bool):
        errors.append(f"field {key or '<unknown>'} writable must be boolean")
    if not isinstance(field.get("secret"), bool):
        errors.append(f"field {key or '<unknown>'} secret must be boolean")
    binding = field.get("binding")
    if not isinstance(binding, dict):
        errors.append(f"field {key or '<unknown>'} binding must be an object")
    else:
        adapter = binding.get("adapter")
        if adapter not in STANDARD_ADAPTERS:
            errors.append(f"field {key or '<unknown>'} has unsupported adapter: {adapter}")
        if field.get("secret") and field.get("writable"):
            method = binding.get("secret_write_method")
            if method not in {"stdin", "native-provider"}:
                errors.append(
                    f"secret field {key or '<unknown>'} requires secret_write_method stdin or native-provider"
                )
    if field_type == "enum":
        choices = field.get("validation", {}).get("choices") if isinstance(field.get("validation"), dict) else None
        if not isinstance(choices, list) or not choices:
            errors.append(f"enum field {key or '<unknown>'} requires validation.choices")
    if "user_intents" not in field and not field.get("intent_mapping_not_applicable"):
        warnings.append(f"field {key or '<unknown>'} has no user_intents")
    _walk_forbidden(field, f"field[{key or '?'}]", errors)
    return ValidationResult(errors, warnings)


def validate_contract(contract: Any) -> ValidationResult:
    errors: list[str] = []
    warnings: list[str] = []
    if not isinstance(contract, dict):
        return ValidationResult(["contract must be an object"], [])
    _walk_forbidden(contract, "", errors)
    if contract.get("schema_version") != CONTRACT_SCHEMA_VERSION:
        errors.append(f"schema_version must equal {CONTRACT_SCHEMA_VERSION}")
    application = contract.get("application")
    if not isinstance(application, dict):
        errors.append("application must be an object")
    else:
        for key in ("name", "command", "native_authority"):
            if key not in application:
                errors.append(f"application missing required key: {key}")
        if not isinstance(application.get("native_authority"), dict):
            errors.append("application.native_authority must be an object")
    modules = contract.get("modules")
    if not isinstance(modules, list):
        errors.append("modules must be a list")
        return ValidationResult(errors, warnings)
    module_names: set[str] = set()
    field_keys: set[str] = set()
    for index, module in enumerate(modules):
        if not isinstance(module, dict):
            errors.append(f"modules[{index}] must be an object")
            continue
        name = module.get("name")
        if not isinstance(name, str) or not name:
            errors.append(f"modules[{index}].name must be non-empty")
            continue
        if name in module_names:
            errors.append(f"duplicate module name: {name}")
        module_names.add(name)
        if not isinstance(module.get("description"), str) or not module.get("description"):
            errors.append(f"module {name} requires description")
        fields = module.get("fields")
        if not isinstance(fields, list):
            errors.append(f"module {name} fields must be a list")
            continue
        for field in fields:
            if not isinstance(field, dict):
                errors.append(f"module {name} contains non-object field")
                continue
            result = validate_field(field, module_name=name)
            errors.extend(result.errors)
            warnings.extend(result.warnings)
            field_key = field.get("key")
            if isinstance(field_key, str):
                if field_key in field_keys:
                    errors.append(f"duplicate field key: {field_key}")
                field_keys.add(field_key)
    if not modules:
        warnings.append("contract has no modules")
    return ValidationResult(errors, warnings)


def find_field(contract: dict[str, Any], key: str) -> tuple[dict[str, Any], dict[str, Any]]:
    for module, field in iter_fields(contract):
        if field.get("key") == key:
            return module, field
    raise ToolsmithError(f"unknown field: {key}", code=2)


def add_field(contract: dict[str, Any], field: dict[str, Any], module_name: str) -> None:
    for module in contract.get("modules", []):
        if module.get("name") == module_name:
            module.setdefault("fields", []).append(field)
            module["fields"] = sorted(module["fields"], key=lambda item: item.get("key", ""))
            return
    contract.setdefault("modules", []).append(
        {"name": module_name, "description": field.get("module_description", module_name), "fields": [field]}
    )
    contract["modules"] = sorted(contract["modules"], key=lambda item: item.get("name", ""))
