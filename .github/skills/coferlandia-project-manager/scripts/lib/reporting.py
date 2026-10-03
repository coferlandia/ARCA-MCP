#!/usr/bin/env python3
"""Compatibility facade for GitHub-native PM reporting.

Keeps the reporting implementation unchanged while normalizing managed project paths
correctly on native Windows and WSL/POSIX environments.
"""
from __future__ import annotations

import importlib.util
import os
import re
import subprocess
import sys
from pathlib import Path

_CORE_PATH = Path(__file__).with_name("_reporting_core.py")
_SPEC = importlib.util.spec_from_file_location("_coferlandia_reporting_core", _CORE_PATH)
if _SPEC is None or _SPEC.loader is None:
    raise ImportError(f"Could not load reporting core from {_CORE_PATH}")
_core = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_core)


def _run(cmd: list[str], *, cwd: Path, check: bool = False) -> subprocess.CompletedProcess[str]:
    try:
        result = subprocess.run(cmd, cwd=cwd, text=True, capture_output=True, check=False)
    except OSError as exc:
        result = subprocess.CompletedProcess(cmd, 127, stdout="", stderr=str(exc))
    if check and result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "command failed")
    return result


def _resolve_project_path(raw_path: str) -> Path:
    cleaned = raw_path.replace("\\", "/")
    match = re.match(r"^([A-Za-z]):/(.*)$", cleaned)
    if match and os.name != "nt":
        cleaned = f"/mnt/{match.group(1).lower()}/{match.group(2)}"
    return Path(cleaned).resolve()


_core._run = _run
_core._resolve_project_path = _resolve_project_path

for _name in dir(_core):
    if not _name.startswith("__"):
        globals()[_name] = getattr(_core, _name)

# Re-export the corrected helpers after copying the core namespace.
globals()["_run"] = _run
globals()["_resolve_project_path"] = _resolve_project_path

if __name__ == "__main__":
    sys.exit(_core.main())
