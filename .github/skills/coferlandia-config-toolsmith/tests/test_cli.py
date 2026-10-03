from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL = Path(__file__).resolve().parent.parent
SCRIPTS = SKILL / "scripts"
ENTRYPOINT = SCRIPTS / "coferlandia-config-toolsmith-cli.py"
sys.path.insert(0, str(SCRIPTS))
from coferlandia_config_toolsmith_cli.model import load_data, validate_contract
from coferlandia_config_toolsmith_cli.operations import (
    check_drift,
    decide_candidate,
    generate_docs,
    generate_facade,
)

FIXTURES = SKILL / "tests" / "fixtures"


class TestContract(unittest.TestCase):
    def test_valid_contract(self):
        result = validate_contract(load_data(FIXTURES / "valid-contract.yaml"))
        self.assertTrue(result.valid, result.errors)

    def test_state_projection_rejected(self):
        result = validate_contract(load_data(FIXTURES / "invalid-state-contract.yaml"))
        self.assertFalse(result.valid)
        self.assertTrue(any("effective_value" in error for error in result.errors))

    def test_toolsmith_cli_accepts_json_flag_anywhere(self):
        completed = subprocess.run(
            [sys.executable, str(ENTRYPOINT), "contract", "validate", "--contract", str(FIXTURES / "valid-contract.yaml"), "--json"],
            capture_output=True,
            text=True,
        )
        self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
        self.assertTrue(json.loads(completed.stdout)["result"]["valid"])


class TestCandidateLifecycle(unittest.TestCase):
    def test_approve_requires_regeneration_before_implemented(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            contract = root / "contract.yaml"
            candidates = root / "candidates.yaml"
            decisions = root / "decisions.yaml"
            target = root / "target"
            docs = target / "docs" / "configuration"
            target.mkdir()
            (target / "pyproject.toml").write_text("[project]\nname='sample'\n", encoding="utf-8")
            contract.write_text((FIXTURES / "valid-contract.yaml").read_text(), encoding="utf-8")
            candidates.write_text((FIXTURES / "candidates.yaml").read_text(), encoding="utf-8")

            direct = decide_candidate(
                action="approve",
                candidate_id="CFG-CAND-001",
                candidates_path=candidates,
                contract_path=contract,
                decisions_path=decisions,
                expected_fingerprint="sha256:test-source",
                dry_run=False,
            )
            self.assertTrue(direct["contract_changed"])
            self.assertEqual(load_data(candidates)["candidates"][0]["status"], "approved")

            # Restore the fixture and exercise the public atomic approval path.
            contract.write_text((FIXTURES / "valid-contract.yaml").read_text(), encoding="utf-8")
            candidates.write_text((FIXTURES / "candidates.yaml").read_text(), encoding="utf-8")
            decisions.unlink(missing_ok=True)
            completed = subprocess.run(
                [
                    sys.executable,
                    str(ENTRYPOINT),
                    "candidates",
                    "approve",
                    "CFG-CAND-001",
                    "--candidates",
                    str(candidates),
                    "--contract",
                    str(contract),
                    "--decisions",
                    str(decisions),
                    "--expected-fingerprint",
                    "sha256:test-source",
                    "--target-root",
                    str(target),
                    "--output-dir",
                    str(docs),
                    "--platform",
                    "python",
                    "--json",
                ],
                capture_output=True,
                text=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
            payload = json.loads(completed.stdout)
            self.assertEqual(payload["result"]["status"], "implemented")
            self.assertEqual(load_data(candidates)["candidates"][0]["status"], "implemented")
            self.assertTrue((target / "scripts" / "sample-config-cli.py").exists())
            self.assertTrue((docs / "CONFIG-AGENT-HANDBOOK.md").exists())

            data = load_data(candidates)
            data["candidates"][0]["status"] = "pending"
            candidates.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaises(Exception):
                decide_candidate(
                    action="approve",
                    candidate_id="CFG-CAND-001",
                    candidates_path=candidates,
                    contract_path=contract,
                    decisions_path=decisions,
                    expected_fingerprint="sha256:wrong",
                    dry_run=False,
                )
            self.assertEqual(load_data(candidates)["candidates"][0]["status"], "stale")


class TestGeneration(unittest.TestCase):
    def make_project(self, root: Path):
        (root / "pyproject.toml").write_text("[project]\nname='sample'\n", encoding="utf-8")
        (root / ".env").write_text("REMINDER_LEAD_MINUTES=60\nUNRELATED=keep\n", encoding="utf-8")
        (root / "config").mkdir()
        (root / "config/native.json").write_text('{"notifications":{"email":{"enabled":true}}}\n', encoding="utf-8")

    def test_docs_are_deterministic_and_drift_detected(self):
        contract = load_data(FIXTURES / "valid-contract.yaml")
        with tempfile.TemporaryDirectory() as td:
            out = Path(td)
            generate_docs(contract, out, dry_run=False)
            first = {path.name: path.read_text() for path in out.iterdir()}
            generate_docs(contract, out, dry_run=False)
            second = {path.name: path.read_text() for path in out.iterdir()}
            self.assertEqual(first, second)
            self.assertTrue(check_drift(contract, out)["clean"])
            (out / "CONFIG-SAFETY.md").write_text("drift", encoding="utf-8")
            self.assertFalse(check_drift(contract, out)["clean"])

    def test_generated_python_facade_reads_writes_and_unsets_native_stores(self):
        contract = load_data(FIXTURES / "valid-contract.yaml")
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            self.make_project(root)
            result = generate_facade(contract, root, "python", dry_run=False)
            self.assertEqual(result["platform"], "python")
            cli = root / "scripts/sample-config-cli.py"

            get = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "get", "notifications.reminder_lead_minutes", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertEqual(get.returncode, 0, get.stderr)
            self.assertEqual(json.loads(get.stdout)["result"]["value"], "60")

            dry = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "set", "notifications.reminder_lead_minutes", "120", "--dry-run", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertEqual(dry.returncode, 0)
            self.assertIn("REMINDER_LEAD_MINUTES=60", (root / ".env").read_text())

            applied = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "set", "notifications.reminder_lead_minutes", "120", "--confirm", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertEqual(applied.returncode, 0, applied.stdout + applied.stderr)
            text = (root / ".env").read_text()
            self.assertIn("REMINDER_LEAD_MINUTES=120", text)
            self.assertIn("UNRELATED=keep", text)

            unset = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "unset", "notifications.reminder_lead_minutes", "--confirm", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertEqual(unset.returncode, 0, unset.stdout + unset.stderr)
            self.assertTrue(json.loads(unset.stdout)["result"]["verified"])
            self.assertNotIn("REMINDER_LEAD_MINUTES", (root / ".env").read_text())

            secret = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "secret", "set", "notifications.telegram.token", "--stdin", "--confirm", "--json"],
                input="top-secret\n",
                capture_output=True,
                text=True,
            )
            self.assertEqual(secret.returncode, 0, secret.stdout + secret.stderr)
            self.assertNotIn("top-secret", secret.stdout)
            self.assertTrue(json.loads(secret.stdout)["result"]["verified"])
            self.assertIn("TELEGRAM_TOKEN=top-secret", (root / ".env").read_text())

    def prepare_plan(self, root: Path, cli: Path, value="120"):
        plan = root / "plan.json"
        prep = subprocess.run(
            [
                sys.executable,
                str(cli),
                "--root",
                str(root),
                "config",
                "prepare-change",
                "--set",
                f"notifications.reminder_lead_minutes={value}",
                "--output",
                str(plan),
                "--json",
            ],
            capture_output=True,
            text=True,
        )
        self.assertEqual(prep.returncode, 0, prep.stdout + prep.stderr)
        return plan, json.loads(prep.stdout)["result"]["plan_hash"]

    def test_stale_plan_rejected(self):
        contract = load_data(FIXTURES / "valid-contract.yaml")
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            self.make_project(root)
            generate_facade(contract, root, "python", dry_run=False)
            cli = root / "scripts/sample-config-cli.py"
            plan, plan_hash = self.prepare_plan(root, cli)
            (root / ".env").write_text("REMINDER_LEAD_MINUTES=90\n")
            apply = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "apply-plan", "--plan-file", str(plan), "--expect-hash", plan_hash, "--confirm", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertEqual(apply.returncode, 3)
            self.assertIn("stale_plan", apply.stdout)

    def test_tampered_plan_rejected_before_write(self):
        contract = load_data(FIXTURES / "valid-contract.yaml")
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            self.make_project(root)
            generate_facade(contract, root, "python", dry_run=False)
            cli = root / "scripts/sample-config-cli.py"
            plan, plan_hash = self.prepare_plan(root, cli)
            data = json.loads(plan.read_text())
            data["changes"][0]["to"] = 999
            plan.write_text(json.dumps(data), encoding="utf-8")
            apply = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "apply-plan", "--plan-file", str(plan), "--expect-hash", plan_hash, "--confirm", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(apply.returncode, 0)
            self.assertIn("plan hash mismatch", apply.stdout)
            self.assertIn("REMINDER_LEAD_MINUTES=60", (root / ".env").read_text())

    def test_wizard_fails_closed_without_tty(self):
        contract = load_data(FIXTURES / "valid-contract.yaml")
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            self.make_project(root)
            generate_facade(contract, root, "python", dry_run=False)
            cli = root / "scripts/sample-config-cli.py"
            completed = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "notifications", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertEqual(completed.returncode, 4)
            self.assertIn("interactive_terminal_required", completed.stdout)

    def test_contract_path_cannot_escape_target_root(self):
        contract = load_data(FIXTURES / "valid-contract.yaml")
        contract["modules"][0]["fields"][0]["binding"]["path"] = "../outside.env"
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            self.make_project(root)
            generate_facade(contract, root, "python", dry_run=False)
            cli = root / "scripts/sample-config-cli.py"
            completed = subprocess.run(
                [sys.executable, str(cli), "--root", str(root), "config", "get", "notifications.reminder_lead_minutes", "--json"],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("escapes repository root", completed.stdout)


if __name__ == "__main__":
    unittest.main()
