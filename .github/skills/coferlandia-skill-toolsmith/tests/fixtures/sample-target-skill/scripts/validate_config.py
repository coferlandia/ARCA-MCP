#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Fixture script for sample-target-skill.

Validates a config.json: must exist, be valid JSON, and contain a top-level
"name" string. Prints a message and exits 0 on success, non-zero on failure.

This is an internal tool the toolsmith is meant to discover in Phase 1 and
consolidate behind sample-target-skill-cli.py (e.g. as `config validate`).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: validate_config.py <config.json>", file=sys.stderr)
        return 2
    p = Path(sys.argv[1])
    if not p.is_file():
        print(f"config not found: {p}", file=sys.stderr)
        return 1
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        print(f"invalid json: {e}", file=sys.stderr)
        return 1
    if not isinstance(data, dict) or not isinstance(data.get("name"), str):
        print("config must have a top-level 'name' string", file=sys.stderr)
        return 3
    print("config ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
