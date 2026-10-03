from __future__ import annotations
import json
import unittest
from pathlib import Path

SKILL=Path(__file__).resolve().parent.parent
TEXT=(SKILL/"SKILL.md").read_text(encoding="utf-8")
GUIDE=(SKILL/"references/guide-mode.md").read_text(encoding="utf-8")
FALLBACK=(SKILL/"references/exhaustive-fallback.md").read_text(encoding="utf-8")
CASES=json.loads((SKILL/"tests/cases.json").read_text(encoding="utf-8"))

class TestConfigDevOpsContract(unittest.TestCase):
    def test_activation_cases(self):
        self.assertTrue(CASES["positive"]); self.assertTrue(CASES["negative"])
        self.assertTrue(all("coferlandia-config-devops" in p.lower() or "config operator" in p.lower() for p in CASES["positive"]))
        self.assertTrue(all("coferlandia-config-devops" not in p.lower() and "config operator" not in p.lower() for p in CASES["negative"]))
    def test_execute_and_guide_are_separate(self):
        self.assertIn("Execute Mode",TEXT); self.assertIn("Guide Mode",TEXT); self.assertIn("Execution status: NOT EXECUTED",TEXT)
    def test_control_tower_batches_commands(self):
        for phrase in ("control-tower", "Minimize copy/paste", "Prepare", "Apply", "Activate", "go-around"):
            self.assertIn(phrase,GUIDE)
    def test_search_is_not_authoritative(self):
        self.assertIn("non-authoritative",FALLBACK); self.assertIn("must never automatically",FALLBACK)
    def test_no_direct_edit_or_contract_mutation(self):
        self.assertIn("must not bypass it",TEXT); self.assertIn("cannot promote",TEXT)

if __name__ == "__main__": unittest.main()
