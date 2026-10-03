from __future__ import annotations

import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CASES = json.loads((ROOT / "tests" / "cases.json").read_text(encoding="utf-8"))
SKILL = " ".join((ROOT / "SKILL.md").read_text(encoding="utf-8").lower().split())

ARCH_SIGNALS = (
    "architecture preflight", "architecture assessment", "release closeout",
    "component extraction", "architectural history", "architecture home",
    "component application", "architecture gate", "reusable component", "component performed",
)
ROUTINE_SIGNALS = ("localized", "null-reference", "tiny retouch", "pull request", "style consistency")


class ActivationTests(unittest.TestCase):
    def test_cases_have_positive_negative_and_pressure_examples(self) -> None:
        self.assertGreaterEqual(len(CASES["positive"]), 5)
        self.assertGreaterEqual(len(CASES["negative"]), 5)
        self.assertGreaterEqual(len(CASES["evaluations"]), 7)

    def test_positive_prompts_carry_architecture_governance_intent(self) -> None:
        missing = [p for p in CASES["positive"] if not any(s in p.lower() for s in ARCH_SIGNALS)]
        self.assertEqual([], missing)

    def test_negative_prompts_are_routine_development(self) -> None:
        leaked = [p for p in CASES["negative"] if any(s in p.lower() for s in ARCH_SIGNALS)]
        self.assertEqual([], leaked)
        self.assertTrue(any(any(s in p.lower() for s in ROUTINE_SIGNALS) for p in CASES["negative"]))

    def test_skill_encodes_activation_and_non_activation_boundaries(self) -> None:
        for phrase in ("architecture preflight", "architecture assessment", "component extraction", "release closeout"):
            self.assertIn(phrase, SKILL)
        for phrase in ("routine localized", "ordinary bugs", "retouch mode", "generic code review"):
            self.assertIn(phrase, SKILL)

    def test_pressure_behaviors_are_normative(self) -> None:
        for phrase in (
            "read-only against the source project", "explicit extraction authority",
            "no material architectural change", "materiality gate", "must stop before implementation",
            "do not commit, push, merge, reset",
        ):
            self.assertIn(phrase, SKILL)


if __name__ == "__main__":
    unittest.main()
