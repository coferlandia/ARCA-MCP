from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLI = ROOT / "scripts" / "the-architect-cli.py"


class HomeValidationTests(unittest.TestCase):
    def run_cli(self, *args: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run([sys.executable, str(CLI), *args], text=True, capture_output=True)

    def test_malformed_managed_entity_is_not_silently_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.assertEqual(0, self.run_cli("--home", str(home), "home", "init").returncode)
            bad = home / "projects" / "bad" / "PROJECT-bad.md"
            bad.parent.mkdir(parents=True)
            bad.write_text("# Missing frontmatter\n", encoding="utf-8")
            result = self.run_cli("--home", str(home), "home", "validate")
            self.assertEqual(2, result.returncode)
            payload = json.loads(result.stdout)
            self.assertFalse(payload["ok"])
            self.assertIn("invalid managed entity", payload["error"])


if __name__ == "__main__":
    unittest.main()
