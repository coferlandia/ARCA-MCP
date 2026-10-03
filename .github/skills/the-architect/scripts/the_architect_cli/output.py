from __future__ import annotations

import json
from typing import Any


def envelope(command: str, *, ok: bool = True, data: Any = None, warnings: list[str] | None = None, error: str | None = None) -> dict[str, Any]:
    return {
        "ok": ok,
        "command": command,
        "data": data,
        "warnings": warnings or [],
        "error": error,
    }


def emit(value: dict[str, Any]) -> None:
    print(json.dumps(value, indent=2, ensure_ascii=False, sort_keys=True))
