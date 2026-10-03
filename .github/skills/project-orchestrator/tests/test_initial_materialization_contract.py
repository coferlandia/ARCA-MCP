"""Static regression tests for the one-time contract initialization boundary."""
from __future__ import annotations

import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]


class InitialMaterializationContractTests(unittest.TestCase):
    def read(self, relative: str) -> str:
        return (REPO / relative).read_text(encoding="utf-8")

    def test_planner_and_analyst_each_write_one_representation(self) -> None:
        planner = self.read("skills/ops/coferlandia-project-manager/SKILL.md")
        analyst = self.read("skills/engineering/software-development/SKILL.md")
        self.assertIn("writes exactly one complete representation per invocation", planner)
        self.assertIn("writes exactly one complete representation per invocation", analyst)
        self.assertIn("<!-- coferlandia-analysis-contract -->", analyst)
        self.assertIn("Initial counterpart materialization performed: none", analyst)

    def test_orchestrator_contract_freezes_snapshot_after_initialization(self) -> None:
        skill = self.read("skills/ops/project-orchestrator/SKILL.md")
        reference = self.read("skills/ops/project-orchestrator/references/initial-contract-materialization.md")
        for text in (skill, reference):
            self.assertIn("Initial Contract Materialization", text)
            self.assertIn("frozen", text.lower())
            self.assertIn("not contract synchronization", text)
        self.assertIn("ANALYSIS.md", skill)

    def test_execution_loop_does_not_refresh_contracts_between_tasks(self) -> None:
        engine = self.read("skills/ops/project-orchestrator/scripts/project_orchestrator_cli/engine.py")
        state = self.read("skills/ops/project-orchestrator/scripts/project_orchestrator_cli/state.py")
        materialization = self.read("skills/ops/project-orchestrator/scripts/project_orchestrator_cli/materialization.py")
        self.assertNotIn("verify_github_freshness", engine)
        self.assertNotIn("verify_github_freshness", materialization)
        self.assertNotIn("BLOCKED_BY_STALE_CONTRACT", engine)
        self.assertNotIn("BLOCKED_BY_STALE_CONTRACT", state)

    def test_behavior_cases_cover_single_store_and_no_continuous_sync(self) -> None:
        cases = self.read("skills/engineering/software-development/tests/cases.json")
        self.assertIn("analyst-github-canonical-analysis", cases)
        self.assertIn("analyst-local-output-for-github-initialization", cases)
        self.assertIn("analyst-does-not-maintain-contract-sync", cases)


if __name__ == "__main__":
    unittest.main()
