from __future__ import annotations

import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class ReleasePublisherActivationTests(unittest.TestCase):
    def test_cases_cover_release_and_non_deployment_boundaries(self) -> None:
        cases = json.loads((ROOT / "tests/cases.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(cases["positive"]), 6)
        self.assertGreaterEqual(len(cases["negative"]), 6)
        self.assertGreaterEqual(len(cases["pressure"]), 6)

    def test_skill_contract_declares_exact_release_scope(self) -> None:
        text = (ROOT / "SKILL.md").read_text(encoding="utf-8")
        lowered = text.lower()
        self.assertIn("commit -> release", lowered)
        self.assertIn("annotated", lowered)
        self.assertIn("github release", lowered)
        self.assertIn("dry-run", lowered)
        self.assertIn("idempot", lowered)
        self.assertIn("deployment", lowered)
        self.assertIn("do not", lowered)
        self.assertIn("local", lowered)
        self.assertIn("published release", lowered)


if __name__ == "__main__":
    unittest.main()
