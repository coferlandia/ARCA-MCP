from __future__ import annotations
import json
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
CASES = json.loads((SKILL_DIR / "tests" / "cases.json").read_text(encoding="utf-8"))
SIGNALS = ("coferlandia-config-toolsmith", "config toolsmith process")

class TestActivation(unittest.TestCase):
    def test_structure(self):
        self.assertTrue(CASES["positive"]); self.assertTrue(CASES["negative"])
    def test_positive_explicit(self):
        self.assertFalse([p for p in CASES["positive"] if not any(s in p.lower() for s in SIGNALS)])
    def test_negative_not_explicit(self):
        self.assertFalse([p for p in CASES["negative"] if any(s in p.lower() for s in SIGNALS)])

if __name__ == "__main__": unittest.main()
