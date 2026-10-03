#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# ///
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REQUIRED_FILES = (
    "README.md",
    "AGENTS.md",
    "DECISIONS.md",
    "RUNBOOK.md",
    ".agent/catalog/SOURCE_INDEX.md",
    ".agent/catalog/PROCESSING_RUNS.md",
)
LEGACY_OPERATIONAL_FILES = (
    "TODO.md",
    "HISTORY.md",
    ".agent/catalog/OPEN_QUESTIONS.md",
)
ARCHIVE_LINK_RE = re.compile(r"\[\[(\.agent/archive/[^\]]+)\]\]|\[[^\]]+\]\((\.agent/archive/[^)]+)\)")
SOURCE_INDEX_ARCHIVED_RE = re.compile(r"^\|\s*archived\s*\|\s*([^|]+)\|\s*([^|]+)\|", re.I)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate a GitHub-native Archivist catalog.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument(
        "--require-github-native",
        action="store_true",
        help="Fail if legacy TODO.md, HISTORY.md, or OPEN_QUESTIONS.md still exist.",
    )
    return parser.parse_args()


def has_frontmatter(text: str) -> bool:
    return text.startswith("---\n") and "\n---\n" in text


def has_catalog_status_processed(text: str) -> bool:
    if not has_frontmatter(text):
        return False
    header = text.split("\n---\n", 1)[0]
    return re.search(r"(?m)^catalog_status:\s*processed\s*$", header) is not None


def validate_required_files(root: Path, failures: list[str]) -> None:
    for rel in REQUIRED_FILES:
        if not (root / rel).is_file():
            failures.append(f"Missing required file: {rel}")


def validate_agents(root: Path, failures: list[str]) -> None:
    path = root / "AGENTS.md"
    if not path.is_file():
        return
    text = path.read_text(encoding="utf-8", errors="replace")
    for header in (
        "## Critical Instructions for Agents",
        "## Project Essentials",
        "## Documentation Index",
        "## Maintenance Notes",
    ):
        if header not in text:
            failures.append(f"Missing required section '{header}' in AGENTS.md")


def validate_processing_runs(root: Path, failures: list[str]) -> None:
    path = root / ".agent/catalog/PROCESSING_RUNS.md"
    if path.is_file() and "# Processing Runs" not in path.read_text(encoding="utf-8", errors="replace"):
        failures.append("Missing '# Processing Runs' title in .agent/catalog/PROCESSING_RUNS.md")


def validate_archive_frontmatter(root: Path, failures: list[str]) -> None:
    archive = root / ".agent/archive"
    if not archive.exists():
        return
    for path in archive.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".md", ".txt", ".rst", ".adoc"}:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        if not has_catalog_status_processed(text):
            failures.append(
                "Archived text document missing catalog_status: processed frontmatter: "
                f"{path.relative_to(root)}"
            )


def validate_archive_links(root: Path, failures: list[str]) -> None:
    for rel in REQUIRED_FILES:
        path = root / rel
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for match in ARCHIVE_LINK_RE.finditer(text):
            target = (match.group(1) or match.group(2) or "").split("#", 1)[0].strip()
            if target and not (root / target).exists():
                failures.append(f"Broken archive link in {rel}: {target}")


def validate_source_index_vs_inbox(root: Path, failures: list[str]) -> None:
    path = root / ".agent/catalog/SOURCE_INDEX.md"
    if not path.is_file():
        return
    inbox = root / "docs/inbox"
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = SOURCE_INDEX_ARCHIVED_RE.match(line)
        if not match:
            continue
        original = match.group(1).strip().strip("`")
        archived = match.group(2).strip().strip("`")
        candidate = root / original
        if original and candidate.exists() and inbox in candidate.parents:
            failures.append(f"Source marked archived is still present in docs/inbox/: {original} -> {archived}")


def legacy_files(root: Path) -> list[str]:
    return [rel for rel in LEGACY_OPERATIONAL_FILES if (root / rel).exists()]


def main() -> int:
    args = parse_args()
    root = Path(args.project_root).resolve()
    if not root.exists():
        print(f"Project root does not exist: {root}", file=sys.stderr)
        return 1

    failures: list[str] = []
    validate_required_files(root, failures)
    validate_agents(root, failures)
    validate_processing_runs(root, failures)
    validate_archive_frontmatter(root, failures)
    validate_archive_links(root, failures)
    validate_source_index_vs_inbox(root, failures)

    legacy = legacy_files(root)
    if args.require_github_native and legacy:
        failures.extend(f"Legacy operational artifact still exists: {rel}" for rel in legacy)

    if failures:
        print("Documentation catalog validation failed:")
        for item in failures:
            print(f"- {item}")
        return 1

    print("Documentation catalog validation passed.")
    print(f"Project root: {root}")
    if legacy:
        print("Migration warnings:")
        for rel in legacy:
            print(f"- Legacy operational artifact detected: {rel}")
    else:
        print("GitHub-native cutover: complete")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
