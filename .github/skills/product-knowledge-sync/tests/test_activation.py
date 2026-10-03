"""Lightweight activation-contract checks for product-knowledge-sync."""
from __future__ import annotations

import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
SKILL_DIR = ROOT / "skills/content/product-knowledge-sync"


class ProductKnowledgeActivationTests(unittest.TestCase):
    def test_description_contains_positive_trigger_vocabulary(self) -> None:
        text = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8").lower()
        for term in (
            "product behavior",
            "help",
            "support knowledge",
            "commercial material",
            "documentation drift",
            "release",
        ):
            self.assertIn(term, text)

    def test_description_contains_negative_boundaries(self) -> None:
        text = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8").lower()
        for term in ("generic copywriting", "developer onboarding", "archivist-only", "ordinary code review"):
            self.assertIn(term, text)

    def test_cases_are_natural_language_contracts(self) -> None:
        cases = json.loads((SKILL_DIR / "tests/cases.json").read_text(encoding="utf-8"))
        for group in ("positive", "negative", "pressure"):
            for case in cases[group]:
                self.assertIsInstance(case.get("prompt"), str)
                self.assertGreater(len(case["prompt"].split()), 5)
                self.assertIsInstance(case.get("expect"), str)


if __name__ == "__main__":
    unittest.main()
