#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Automated activation-gate tests for coferlandia-skill-toolsmith.

This skill is explicit-invocation ONLY. These tests mechanically assert that
every positive prompt in cases.json carries an explicit activation signal (the
skill name or the named process) and that no negative prompt does.

They do NOT depend on an LLM: the activation rule reduces to a deterministic,
string-level check, which is exactly what makes it auditable. Run with:

    python tests/test_activation.py            # exits 0 on pass, 1 on fail
    python -m unittest tests.test_activation   # via unittest
"""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
CASES_PATH = SKILL_DIR / "tests" / "cases.json"

# Explicit activation signals. The skill's activation rule (see SKILL.md
# "Activation rule") says it activates ONLY when the request names the skill or
# asks for "the Skill Toolsmith process" for a specific target skill. Anything
# else — similarity, token inefficiency, "improve/refactor", opportunities to
# automate — MUST NOT activate.
EXPLICIT_SIGNALS = (
    "coferlandia-skill-toolsmith",
    "skill toolsmith",
    "the skill toolsmith process",
    "the toolsmith process",
)


def _load_cases() -> dict[str, list[str]]:
    return json.loads(CASES_PATH.read_text(encoding="utf-8"))


def _has_explicit_signal(text: str) -> bool:
    low = text.lower()
    return any(sig in low for sig in EXPLICIT_SIGNALS)


class TestActivationGate(unittest.TestCase):
    """Positive prompts must carry an explicit signal; negatives must not."""

    def setUp(self) -> None:
        self.cases = _load_cases()

    def test_cases_file_structure(self) -> None:
        """cases.json must exist and have non-empty positive + negative lists."""
        self.assertIn("positive", self.cases, "cases.json missing 'positive'")
        self.assertIn("negative", self.cases, "cases.json missing 'negative'")
        self.assertGreaterEqual(
            len(self.cases["positive"]), 1, "need >=1 positive prompt"
        )
        self.assertGreaterEqual(
            len(self.cases["negative"]), 1, "need >=1 negative prompt"
        )

    def test_positive_prompts_carry_explicit_signal(self) -> None:
        """Every positive prompt must explicitly name the skill or its process.

        A positive prompt without an explicit signal would mean the skill can be
        activated by mere topic similarity — which is exactly the failure mode
        the activation rule forbids.
        """
        missing = [
            p for p in self.cases["positive"] if not _has_explicit_signal(p)
        ]
        self.assertEmpty(
            missing,
            f"Positive prompts must explicitly name the skill/process. "
            f"Offending: {missing}",
        )

    def test_negative_prompts_carry_no_explicit_signal(self) -> None:
        """No negative prompt may name the skill or its process.

        If a 'should-not-activate' prompt contains the explicit signal, the gate
        is trivially defeated and the test catches the contradiction.
        """
        leaked = [
            p for p in self.cases["negative"] if _has_explicit_signal(p)
        ]
        self.assertEmpty(
            leaked,
            f"Negative prompts must NOT name the skill/process. Offending: {leaked}",
        )

    # unittest has no built-in assertEmpty; add a small helper.
    def assertEmpty(self, seq, msg=None) -> None:  # noqa: N802
        self.assertEqual(len(seq), 0, msg or f"expected empty, got {seq}")


def main() -> int:
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromTestCase(TestActivationGate)
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())
