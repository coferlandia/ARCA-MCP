#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Fixture script for sample-target-skill.

Rebuilds an index file by listing *.md under docs/ and writing one path per line
to index.txt. Idempotent: re-running produces the same file.

This is an internal tool the toolsmith is meant to discover in Phase 1 and
consolidate behind sample-target-skill-cli.py (e.g. as `index rebuild`).
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    ap = argparse.ArgumentParser(description="Rebuild the docs index.")
    ap.add_argument("--quiet", action="store_true", help="suppress output")
    ap.add_argument("--root", default=".", help="repository root (default: cwd)")
    args = ap.parse_args()

    root = Path(args.root)
    docs = root / "docs"
    if not docs.is_dir():
        print(f"docs dir not found: {docs}", file=sys.stderr)
        return 1

    entries = sorted(str(p.relative_to(root)) for p in docs.rglob("*.md"))
    out = root / "index.txt"
    out.write_text("\n".join(entries) + ("\n" if entries else ""), encoding="utf-8")
    if not args.quiet:
        print(f"index rebuilt: {len(entries)} entries -> {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
