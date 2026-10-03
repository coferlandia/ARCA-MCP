from __future__ import annotations

from pathlib import Path
from typing import Any, Callable

from . import operations
from .model import ToolsmithError

_INSTALLED = False


def normalize_python_facade_source(source: str) -> str:
    """Apply the generated Python facade contract before persistence."""
    duplicate = "def parse_assignments(items):\n    result = []\n    result = []\n"
    source = source.replace(duplicate, "def parse_assignments(items):\n    result = []\n", 1)

    setup = (
        '        if args.command == "setup":\n'
        "            return interactive_wizard(root, contract, quick=args.quick)\n"
    )
    replacement = (
        '        if args.command == "setup":\n'
        "            if args.quick and args.reconfigure:\n"
        '                raise ValueError("--quick and --reconfigure cannot be combined")\n'
        "            return interactive_wizard(root, contract, quick=not args.reconfigure)\n"
    )
    if setup not in source:
        raise ToolsmithError("generated Python setup contract anchor is missing", code=4)
    return source.replace(setup, replacement, 1)


def install() -> None:
    """Install source normalization around the deterministic facade generator once."""
    global _INSTALLED
    if _INSTALLED:
        return

    original: Callable[..., dict[str, Any]] = operations.generate_facade

    def normalized_generate_facade(
        contract: dict[str, Any], target_root: Path, platform: str, *, dry_run: bool
    ) -> dict[str, Any]:
        result = original(contract, target_root, platform, dry_run=dry_run)
        effective_platform = result.get("platform")
        if dry_run or effective_platform not in {"python", "fallback-python"}:
            return result

        command = contract["application"]["command"]
        output = target_root / "scripts" / f"{command}-config-cli.py"
        source = output.read_text(encoding="utf-8")
        normalized = normalize_python_facade_source(source)
        if normalized != source:
            operations.atomic_write(output, normalized)
        return result

    operations.generate_facade = normalized_generate_facade
    _INSTALLED = True
