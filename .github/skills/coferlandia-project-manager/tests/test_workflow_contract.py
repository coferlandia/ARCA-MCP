from __future__ import annotations

import unittest
from pathlib import Path


SKILL = Path(__file__).resolve().parents[1] / "SKILL.md"


class ProjectManagerWorkflowContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = SKILL.read_text(encoding="utf-8")

    def test_epic_planner_and_execution_strategy_are_explicit(self) -> None:
        self.assertIn("### Epic Planner", self.text)
        self.assertIn("## Execution Strategy", self.text)
        self.assertIn("Decomposition: Analyst | none", self.text)
        self.assertIn("Execution: Project Orchestrator | standalone development roles", self.text)

    def test_direct_capable_agent_path_does_not_require_analyst(self) -> None:
        self.assertIn("Direct capable-agent execution", self.text)
        self.assertIn("Analyst decomposition is not mandatory", self.text)

    def test_local_fallback_does_not_restore_pm_task_database(self) -> None:
        self.assertIn(".agent/work-items/<epic>/", self.text)
        self.assertIn("do not recreate `TODO.md`, `HISTORY.md`", self.text)
        self.assertIn("projects.json` limited to portfolio membership/integration metadata", self.text)

    def test_github_prerequisites_are_mode_specific(self) -> None:
        self.assertIn("Required only for GitHub-backed operations", self.text)
        self.assertIn("does not by itself block", self.text)


if __name__ == "__main__":
    unittest.main()
