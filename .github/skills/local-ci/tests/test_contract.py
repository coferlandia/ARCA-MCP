import json
import re
import unittest
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[4]

class LocalCIContractTests(unittest.TestCase):
    def test_skill_contract(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertRegex(text, r"name:\s*local-ci")
        self.assertRegex(text, r"category:\s*engineering")
        self.assertIn("READY_FOR_CI", text)
        self.assertIn("READY_FOR_MERGE", text)
        self.assertIn(".coferlandia/ci/profile.json", text)
        self.assertIn("LOCAL", text)
        self.assertIn("Invoking this skill selects `LOCAL`", text)
        self.assertIn("no separate strategy router", text)
        self.assertIn("must not run GitHub-native CI", text)
        self.assertIn("LOCAL_QUALIFICATION_BLOCKED", text)
        self.assertIn("Base SHA synchronized", text)
        self.assertIn("DEVELOPMENT_CURRENTIZATION_REQUIRED", text)
        self.assertIn("must not currentize or mutate the candidate inside Qualification", text)
        self.assertIn("Do not change the candidate here", text)
        self.assertIn("Do not activate merely because a bounded controller such as `chat-coder` reached `READY_FOR_CI`", text)
        self.assertIn("Explicit Chat `ci` invocation", text)
        self.assertIn("must not merge", text)
        for token in ("SecretarIA", "scripts/validate-all.sh", "fast-ci.yml"):
            self.assertNotIn(token, text)

    def test_changelog_matches_version(self):
        skill = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        changelog = (SKILL_ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
        version = re.search(r'version:\s*["\']([^"\']+)', skill).group(1)
        self.assertIn(f"## {version} —", changelog)

    def test_activation_cases_have_positive_and_negative(self):
        cases = json.loads((SKILL_ROOT / "tests" / "cases.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(cases["positive"]), 5)
        self.assertGreaterEqual(len(cases["negative"]), 5)
        self.assertTrue(any("Qualification" in case and "agente local" in case for case in cases["positive"]))
        self.assertTrue(any("chat coder" in case and "READY_FOR_CI" in case for case in cases["negative"]))
        self.assertTrue(any("gh ci" in case for case in cases["negative"]))

if __name__ == "__main__":
    unittest.main()
