"""Behavioral tests for product-knowledge-sync-cli.py."""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
CLI = ROOT / "skills/content/product-knowledge-sync/scripts/product-knowledge-sync-cli.py"
PROFILE_REL = Path(".coferlandia/product-knowledge/profile.json")


class ProductKnowledgeCliTests(unittest.TestCase):
    def run_cli(self, *args: str, cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(CLI), *args],
            cwd=str(cwd or ROOT),
            text=True,
            capture_output=True,
            check=False,
        )

    def make_project(self, profile: dict | None = None) -> Path:
        self._temp = tempfile.TemporaryDirectory()
        root = Path(self._temp.name)
        (root / "docs/help").mkdir(parents=True)
        (root / "docs/help/index.md").write_text("# Help\n", encoding="utf-8")
        if profile is None:
            profile = {
                "schema_version": 1,
                "surfaces": [
                    {
                        "id": "end-user-help",
                        "kind": "repository",
                        "paths": ["docs/help/**"],
                        "release_policy": "required",
                    },
                    {"id": "commercial", "kind": "external", "release_policy": "review"},
                ],
                "evidence": {"github": "optional", "archivist": "auto"},
                "verification": {
                    "require_impact_for_user_facing_changes": True,
                    "block_on_required_surface": True,
                },
            }
        if profile is not False:
            profile_path = root / PROFILE_REL
            profile_path.parent.mkdir(parents=True, exist_ok=True)
            profile_path.write_text(json.dumps(profile), encoding="utf-8")
        return root

    def tearDown(self) -> None:
        temp = getattr(self, "_temp", None)
        if temp:
            temp.cleanup()

    def fingerprint(self, root: Path) -> str:
        result = self.run_cli("fingerprint", "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 0, result.stderr)
        return json.loads(result.stdout)["profile_fingerprint"]

    def write_report(self, root: Path, payload: dict, name: str = "impact.json") -> Path:
        path = root / name
        path.write_text(json.dumps(payload), encoding="utf-8")
        return path

    def impact_report(self, root: Path, disposition: str = "updated", candidate: str = "sha-123") -> dict:
        return {
            "schema_version": 1,
            "mode": "impact",
            "subject": {"type": "issue", "reference": "#123"},
            "candidate": candidate,
            "profile_fingerprint": self.fingerprint(root),
            "user_facing": True,
            "rationale": "The visible workflow changed.",
            "behaviors": [
                {"id": "booking-change", "summary": "Visible booking behavior changed.", "evidence": ["#123"]}
            ],
            "surfaces": [
                {
                    "id": "end-user-help",
                    "disposition": disposition,
                    "references": ["docs/help/index.md"] if disposition == "updated" else [],
                    "reason": None if disposition == "updated" else "Owner review remains required.",
                },
                {
                    "id": "commercial",
                    "disposition": "manual-review-required",
                    "references": [],
                    "reason": "External owner must review claims.",
                },
            ],
            "handoffs": [],
        }

    def test_help_is_available(self) -> None:
        result = self.run_cli("--help")
        self.assertEqual(result.returncode, 0)
        self.assertIn("verify", result.stdout)
        for command in ("profile", "impact", "audit", "fingerprint"):
            self.assertIn(command, result.stdout)

    def test_valid_minimal_profile_and_deterministic_fingerprint(self) -> None:
        root = self.make_project()
        first = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        second = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(first.stdout, second.stdout)
        payload = json.loads(first.stdout)
        self.assertTrue(payload["valid"])
        self.assertEqual(len(payload["profile_fingerprint"]), 64)

    def test_duplicate_surface_id_is_invalid(self) -> None:
        root = self.make_project(
            {
                "schema_version": 1,
                "surfaces": [
                    {"id": "help", "kind": "external", "release_policy": "review"},
                    {"id": "help", "kind": "external", "release_policy": "optional"},
                ],
            }
        )
        result = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 2)
        self.assertIn("duplicate surface id", result.stderr)

    def test_invalid_schema_and_enum_are_invalid(self) -> None:
        root = self.make_project({"schema_version": 2, "surfaces": []})
        result = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 2)
        self.assertIn("schema_version", result.stderr)

        root_profile = root / PROFILE_REL
        root_profile.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "surfaces": [{"id": "help", "kind": "external", "release_policy": "mandatory"}],
                }
            ),
            encoding="utf-8",
        )
        result = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 2)
        self.assertIn("release_policy", result.stderr)

    def test_required_repository_surface_missing_is_invalid(self) -> None:
        root = self.make_project(
            {
                "schema_version": 1,
                "surfaces": [
                    {
                        "id": "help",
                        "kind": "repository",
                        "paths": ["docs/does-not-exist/**"],
                        "release_policy": "required",
                    }
                ],
            }
        )
        result = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 2)
        self.assertIn("matches no paths", result.stderr)

    def test_external_surface_is_accepted(self) -> None:
        root = self.make_project(
            {
                "schema_version": 1,
                "surfaces": [{"id": "commercial", "kind": "external", "release_policy": "review"}],
            }
        )
        result = self.run_cli("profile", "validate", "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_impact_validation_accepts_resolved_and_unresolved_dispositions(self) -> None:
        root = self.make_project()
        for disposition in ("updated", "manual-review-required"):
            report = self.impact_report(root, disposition=disposition)
            path = self.write_report(root, report, f"impact-{disposition}.json")
            result = self.run_cli(
                "impact", "validate", "--input", str(path), "--project-root", str(root), "--json"
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue(json.loads(result.stdout)["valid"])

    def test_audit_validation_requires_all_profile_surfaces(self) -> None:
        root = self.make_project()
        report = {
            "schema_version": 1,
            "mode": "audit",
            "subject": {"type": "range", "reference": "v1..v2"},
            "surfaces": [
                {"id": "end-user-help", "status": "covered", "references": ["docs/help/index.md"], "reason": "Current."}
            ],
            "handoffs": [],
        }
        path = self.write_report(root, report, "audit.json")
        result = self.run_cli("audit", "validate", "--input", str(path), "--project-root", str(root), "--json")
        self.assertEqual(result.returncode, 2)
        self.assertIn("commercial", result.stderr)

    def test_verify_pass_allows_nonblocking_external_manual_review(self) -> None:
        root = self.make_project()
        report = self.impact_report(root, disposition="updated")
        path = self.write_report(root, report)
        result = self.run_cli(
            "verify",
            "--project-root",
            str(root),
            "--candidate",
            "sha-123",
            "--input",
            str(path),
            "--json",
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["result"], "PASS")
        self.assertEqual(payload["manual_review_pending"], 1)
        self.assertEqual(payload["unresolved_required_surfaces"], 0)

    def test_verify_blocks_unresolved_required_surface(self) -> None:
        root = self.make_project()
        report = self.impact_report(root, disposition="manual-review-required")
        path = self.write_report(root, report)
        result = self.run_cli(
            "verify",
            "--project-root",
            str(root),
            "--candidate",
            "sha-123",
            "--input",
            str(path),
            "--json",
        )
        self.assertEqual(result.returncode, 3, result.stderr)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["result"], "BLOCKED")
        self.assertGreater(payload["unresolved_required_surfaces"], 0)

    def test_verify_rejects_candidate_mismatch_as_stale(self) -> None:
        root = self.make_project()
        report = self.impact_report(root, candidate="old-sha")
        path = self.write_report(root, report)
        result = self.run_cli(
            "verify", "--project-root", str(root), "--candidate", "new-sha", "--input", str(path), "--json"
        )
        self.assertEqual(result.returncode, 4)
        self.assertIn("candidate mismatch", result.stderr)

    def test_verify_rejects_profile_drift_as_stale(self) -> None:
        root = self.make_project()
        report = self.impact_report(root)
        path = self.write_report(root, report)
        profile_path = root / PROFILE_REL
        profile = json.loads(profile_path.read_text(encoding="utf-8"))
        profile["surfaces"].append({"id": "support", "kind": "external", "release_policy": "optional"})
        profile_path.write_text(json.dumps(profile), encoding="utf-8")
        result = self.run_cli(
            "verify", "--project-root", str(root), "--candidate", "sha-123", "--input", str(path), "--json"
        )
        self.assertEqual(result.returncode, 4)
        self.assertIn("profile fingerprint mismatch", result.stderr)

    def test_validation_is_idempotent_and_json_is_stable(self) -> None:
        root = self.make_project()
        report = self.impact_report(root)
        path = self.write_report(root, report)
        first = self.run_cli("impact", "validate", "--input", str(path), "--project-root", str(root), "--json")
        second = self.run_cli("impact", "validate", "--input", str(path), "--project-root", str(root), "--json")
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(first.stdout, second.stdout)


if __name__ == "__main__":
    unittest.main()
