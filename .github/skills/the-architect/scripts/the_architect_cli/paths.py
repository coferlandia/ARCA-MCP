from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any

from .errors import ConfigurationError, ValidationError

DEFAULT_CONFIG = Path.home() / ".coferlandia" / "the-architect" / "config.json"


def load_config(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise ConfigurationError(f"configuration not found: {path}")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ConfigurationError(f"invalid configuration JSON: {exc}") from exc
    if value.get("schema_version") != 1:
        raise ConfigurationError("configuration schema_version must be 1")
    home = value.get("home_repository")
    if not isinstance(home, dict) or not home.get("path"):
        raise ConfigurationError("configuration requires home_repository.path")
    return value


def resolve_home(config_path: Path, override: str | None = None) -> Path:
    if override:
        return Path(override).expanduser().resolve()
    value = load_config(config_path)
    return Path(value["home_repository"]["path"]).expanduser().resolve()


def confined(home: Path, relative: str | Path) -> Path:
    target = (home / relative).resolve()
    try:
        target.relative_to(home.resolve())
    except ValueError as exc:
        raise ValidationError(f"path escapes architecture home: {relative}") from exc
    return target


def atomic_write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(content)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(tmp_name, path)
    finally:
        if os.path.exists(tmp_name):
            os.unlink(tmp_name)
