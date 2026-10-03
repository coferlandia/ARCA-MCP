from __future__ import annotations

import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/coferlandia-release.py"
sys.path.insert(0, str(ROOT / "scripts"))

from release_publisher.operations import classify_consistency


class ReleasePublisherCliTests(unittest.TestCase):
    def test_consistency_state_machine(self) -> None:
        sha = "a" * 40
        annotated = {"kind": "annotated", "commit": sha}
        self.assertEqual(classify_consistency(None, None, sha), "NEW")
        self.assertEqual(classify_consistency(annotated, None, sha), "TAG_ONLY_CORRECT")
        self.assertEqual(classify_consistency(annotated, {"tag": "v1.0.0", "draft": True}, sha), "DRAFT_CORRECT")
        self.assertEqual(classify_consistency(annotated, {"tag": "v1.0.0", "draft": False}, sha), "PUBLISHED_CONSISTENT")
        self.assertEqual(classify_consistency({"kind": "annotated", "commit": "b" * 40}, None, sha), "INCONSISTENT")
        self.assertEqual(classify_consistency({"kind": "lightweight", "commit": sha}, None, sha), "INCONSISTENT")
        self.assertEqual(classify_consistency(None, {"tag": "v1.0.0", "draft": False}, sha), "INCONSISTENT")

    def test_version_and_capabilities_are_machine_readable(self) -> None:
        version = subprocess.run([sys.executable, str(SCRIPT), "version"], text=True, capture_output=True, check=False)
        self.assertEqual(version.returncode, 0, version.stderr + version.stdout)
        self.assertIn("version", json.loads(version.stdout))
        caps = subprocess.run([sys.executable, str(SCRIPT), "capabilities"], text=True, capture_output=True, check=False)
        self.assertEqual(caps.returncode, 0, caps.stderr + caps.stdout)
        values = json.loads(caps.stdout)["capabilities"]
        for expected in ("doctor", "inspect", "plan", "publish", "verify", "resolve"):
            self.assertIn(expected, values)


if __name__ == "__main__":
    unittest.main()
