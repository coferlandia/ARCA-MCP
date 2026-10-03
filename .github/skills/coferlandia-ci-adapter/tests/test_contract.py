import json
import re
import unittest
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]

class CIAdapterContractTests(unittest.TestCase):
    def test_skill_metadata_and_boundaries(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertRegex(text, r"name:\s*coferlandia-ci-adapter")
        self.assertRegex(text, r"category:\s*meta")
        self.assertIn(".coferlandia/ci/profile.json", text)
        self.assertIn("Do not generate repository-local copies", text)
        self.assertIn("does not run CI itself", text)
        for token in ("SecretarIA", "scripts/validate-all.sh", "fast-ci.yml"):
            self.assertNotIn(token, text)

    def test_cases_cover_activation_boundary(self):
        cases = json.loads((SKILL_ROOT / "tests" / "cases.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(cases["positive"]), 4)
        self.assertGreaterEqual(len(cases["negative"]), 4)

    def test_changelog_matches_version(self):
        skill = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        changelog = (SKILL_ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
        version = re.search(r'version:\s*["\']([^"\']+)', skill).group(1)
        self.assertIn(f"## {version} —", changelog)

if __name__ == "__main__":
    unittest.main()
