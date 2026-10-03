#!/usr/bin/env python3
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
SCRIPTS_DIR = SKILL_DIR / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

from coferlandia_config_toolsmith_cli.model import load_data
from coferlandia_config_toolsmith_cli.operations import generate_facade

FIXTURES = SKILL_DIR / "tests" / "fixtures"


class SetupDefaultContractTests(unittest.TestCase):
    def test_setup_defaults_to_minimal_and_reconfigure_expands_scope(self) -> None:
        contract = load_data(FIXTURES / "valid-contract.yaml")
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            (root / "pyproject.toml").write_text("[project]\nname='sample'\n", encoding="utf-8")
            generate_facade(contract, root, "python", dry_run=False)
            source = (root / "scripts/sample-config-cli.py").read_text(encoding="utf-8")

        self.assertIn("quick=not args.reconfigure", source)
        self.assertIn("--quick and --reconfigure cannot be combined", source)
        self.assertNotIn("result = []\n    result = []", source)


if __name__ == "__main__":
    unittest.main()
