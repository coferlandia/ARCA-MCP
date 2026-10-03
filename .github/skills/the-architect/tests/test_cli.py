from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLI = ROOT / "scripts" / "the-architect-cli.py"


class CliTests(unittest.TestCase):
    def run_cli(self, *args: str, ok: bool = True) -> tuple[subprocess.CompletedProcess[str], dict]:
        proc = subprocess.run([sys.executable, str(CLI), *args], text=True, capture_output=True)
        data = json.loads(proc.stdout)
        if ok:
            self.assertEqual(0, proc.returncode, proc.stderr)
            self.assertTrue(data["ok"])
        else:
            self.assertNotEqual(0, proc.returncode)
            self.assertFalse(data["ok"])
        return proc, data

    def test_common_commands_and_json_flag_anywhere(self) -> None:
        _, version = self.run_cli("version", "--json")
        self.assertEqual("1.0.0", version["data"]["version"])
        _, capabilities = self.run_cli("--json", "capabilities")
        self.assertIn("home", capabilities["data"])
        _, check = self.run_cli("self-check", "--json")
        self.assertFalse(check["data"]["git_operations"])

    def test_end_to_end_home_is_idempotent_and_git_safe(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            common = ("--home", str(home))
            self.run_cli(*common, "home", "init")
            self.run_cli(*common, "home", "init")
            self.run_cli(*common, "project", "register", "--slug", "sample", "--title", "Sample")
            _, repeated = self.run_cli(*common, "project", "register", "--slug", "sample", "--title", "Sample")
            self.assertTrue(repeated["data"]["idempotent"])
            self.run_cli(*common, "component", "register", "--slug", "outbox", "--title", "Outbox")
            self.run_cli(*common, "application", "create", "--slug", "sample-outbox", "--title", "Outbox in Sample", "--project", "sample", "--component", "outbox")
            self.run_cli(*common, "engagement", "create", "--slug", "sample-release", "--title", "Sample release", "--project", "sample")
            self.run_cli(*common, "index", "rebuild")
            self.run_cli(*common, "index", "validate")
            self.run_cli(*common, "links", "validate")
            self.run_cli(*common, "home", "validate")
            self.assertFalse((home / ".obsidian").exists())
            self.assertFalse((home / ".git").exists())
            self.assertIn("APP-sample-outbox", (home / "dashboards" / "APPLICATIONS.md").read_text())
            self.assertIn("[[APP-sample-outbox]]", (home / "projects" / "sample" / "PROJECT-sample.md").read_text())
            self.assertIn("[[APP-sample-outbox]]", (home / "components" / "outbox" / "COMP-outbox.md").read_text())

    def test_dry_run_writes_nothing(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.run_cli("--home", str(home), "home", "init", "--dry-run")
            self.assertFalse(home.exists())

    def test_missing_relationships_are_rejected_before_entity_write(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.run_cli("--home", str(home), "home", "init")
            self.run_cli("--home", str(home), "application", "create", "--slug", "bad", "--title", "Bad", "--project", "missing", "--component", "missing", ok=False)
            self.assertFalse((home / "applications" / "APP-bad.md").exists())
            self.run_cli("--home", str(home), "finding", "create", "--slug", "bad-finding", "--title", "Bad finding", "--project", "missing", ok=False)
            self.assertFalse((home / "projects" / "missing" / "findings" / "ARCH-bad-finding.md").exists())

    def test_path_traversal_is_normalized_inside_home(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.run_cli("--home", str(home), "home", "init")
            self.run_cli("--home", str(home), "project", "register", "--slug", "../../outside", "--title", "Safe")
            self.assertFalse((Path(tmp) / "outside").exists())

    def test_no_material_change_creates_no_entity(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.run_cli("--home", str(home), "home", "init")
            _, result = self.run_cli("--home", str(home), "event", "create", "--slug", "none", "--title", "None", "--no-material-change")
            self.assertFalse(result["data"]["created"])
            self.assertEqual([], list((home / "events").glob("*.md")))

    def test_broken_managed_link_is_detected(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.run_cli("--home", str(home), "home", "init")
            bad = home / "events" / "EVENT-bad.md"
            bad.write_text("---\nid: EVENT-bad\ntype: architecture-event\ntitle: Bad\n---\n# Bad\n\n## Event\n[[COMP-missing]]\n\n## Evidence\n", encoding="utf-8")
            self.run_cli("--home", str(home), "links", "validate", ok=False)

    def test_stale_index_is_detected(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp) / "architecture"
            self.run_cli("--home", str(home), "home", "init")
            self.run_cli("--home", str(home), "project", "register", "--slug", "sample", "--title", "Sample")
            dashboard = home / "dashboards" / "PROJECTS.md"
            dashboard.write_text("# Projects\n\n<!-- the-architect:managed:start -->\n_No records._\n<!-- the-architect:managed:end -->\n", encoding="utf-8")
            self.run_cli("--home", str(home), "index", "validate", ok=False)
            self.run_cli("--home", str(home), "index", "rebuild")
            self.run_cli("--home", str(home), "index", "validate")


if __name__ == "__main__":
    unittest.main()
