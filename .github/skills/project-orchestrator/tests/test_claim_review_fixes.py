"""Regression tests for findings discovered during durable-claim review."""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SKILL = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SKILL / "scripts"))

from project_orchestrator_cli.claims import ClaimStore
from project_orchestrator_cli.claims_runtime import prepare_claimed_run, stable_epic_key


class ClaimReviewFixTests(unittest.TestCase):
    def test_local_manifest_identity_is_independent_of_copy_path(self) -> None:
        manifest = {
            "execution_mode": "task-execution",
            "source": {"kind": "local", "source_hash": "sha256:stable"},
            "epic": {"id": "EPIC-1"},
        }
        first = Path(tempfile.mkdtemp()) / "manifest.json"
        second = Path(tempfile.mkdtemp()) / "copy.json"
        first.write_text(json.dumps(manifest), encoding="utf-8")
        second.write_text(json.dumps(manifest), encoding="utf-8")
        self.assertEqual(stable_epic_key(manifest, first), stable_epic_key(manifest, second))

    def test_direct_plan_identity_changes_with_content(self) -> None:
        plan = Path(tempfile.mkdtemp()) / "plan.md"
        manifest = {
            "execution_mode": "direct-plan",
            "source": {"kind": "local"},
            "epic": {"id": "DIRECT-PLAN"},
        }
        plan.write_text("first\n", encoding="utf-8")
        first = stable_epic_key(manifest, plan)
        plan.write_text("second\n", encoding="utf-8")
        self.assertNotEqual(first, stable_epic_key(manifest, plan))

    def test_projection_failure_before_run_creation_releases_claim(self) -> None:
        common = Path(tempfile.mkdtemp())
        repo = Path(tempfile.mkdtemp())
        manifest = {
            "execution_mode": "task-execution",
            "source": {
                "kind": "github",
                "repository": "coferlandia/example",
                "epic_issue": 39,
            },
            "epic": {"id": "EPIC-39"},
            "tasks": [],
        }
        expected_key = stable_epic_key(manifest)

        class FakeGit:
            def __init__(self, _repo: Path):
                pass

            def ensure_repo(self) -> None:
                pass

            def common_dir(self) -> Path:
                return common

        with (
            patch("project_orchestrator_cli.claims_runtime.GitService", FakeGit),
            patch(
                "project_orchestrator_cli.claims_runtime._claims._manifest_before_prepare",
                return_value=(manifest, None),
            ),
            patch(
                "project_orchestrator_cli.claims_runtime._claims._project_claim",
                side_effect=RuntimeError("project unavailable"),
            ),
        ):
            with self.assertRaisesRegex(RuntimeError, "project unavailable"):
                prepare_claimed_run(
                    repo,
                    None,
                    {"github_project": {"owner": "coferlandia", "number": 2}},
                    "run-projection-failure",
                    False,
                    epic="#39",
                )

        self.assertIsNone(ClaimStore(common).get(expected_key))


if __name__ == "__main__":
    unittest.main()
