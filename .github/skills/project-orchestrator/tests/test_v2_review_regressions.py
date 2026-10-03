from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

SKILL = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SKILL / "scripts"))


class ReviewRegressionTests(unittest.TestCase):
    def test_execution_strategy_template_is_not_a_resolved_decision(self) -> None:
        from project_orchestrator_cli.contracts import ValidationError
        from project_orchestrator_cli.work_items import parse_execution_strategy

        template = """## Execution Strategy
Tracking: GitHub | local fallback
Decomposition: Analyst | none
Execution: Project Orchestrator | standalone development roles
Worker profile: Basic coding agents | capable coding agent
Review: Per-task + final Epic review | final independent review
Integration: Single PR / squash merge | explicitly selected alternative
"""
        with self.assertRaises(ValidationError):
            parse_execution_strategy(template)

    def test_concrete_execution_strategy_is_accepted(self) -> None:
        from project_orchestrator_cli.work_items import execution_mode_from_strategy, parse_execution_strategy

        resolved = """## Execution Strategy
Tracking: GitHub
Decomposition: Analyst
Execution: Project Orchestrator
Worker profile: Basic coding agents
Review: Per-task + final Epic review
Integration: Single PR / squash merge
"""
        strategy = parse_execution_strategy(resolved)
        self.assertEqual(execution_mode_from_strategy(strategy), "task-execution")

    def test_issue_comment_pagination_flattens_slurped_pages(self) -> None:
        from project_orchestrator_cli.github_service import GitHubService, IssueRef

        service = GitHubService(Path.cwd())
        seen = {}

        def fake_json(*args: str):
            seen["args"] = args
            return [[{"id": 1, "body": "one"}], [{"id": 2, "body": "two"}]]

        service._json = fake_json  # type: ignore[method-assign]
        comments = service.comments(IssueRef("acme/repo", 7))
        self.assertEqual([item["id"] for item in comments], [1, 2])
        self.assertIn("--slurp", seen["args"])

    def test_python_provider_path_is_available_cross_platform(self) -> None:
        from project_orchestrator_cli.providers import CodexProvider

        provider = CodexProvider(str(Path(sys.executable)))
        self.assertTrue(provider.command_available())

        script = Path(tempfile.mkdtemp()) / "provider.py"
        script.write_text("print('ok')\n", encoding="utf-8")
        scripted = CodexProvider(str(script))
        self.assertTrue(scripted.command_available())
        self.assertEqual(scripted.command_argv("--version")[:2], [sys.executable, str(script)])

    def test_candidate_states_can_block_on_no_progress(self) -> None:
        from project_orchestrator_cli.state import RunStore

        for initial in ("CANDIDATE_PREPARING", "FIX_COMMIT_PREPARING", "HOLISTIC_FIX_COMMIT_PREPARING"):
            common = Path(tempfile.mkdtemp())
            store = RunStore(common, "regression")
            store.create({"run_id": "regression", "state": initial})
            value = store.transition("BLOCKED_BY_NO_PROGRESS", {"reason": "no changes"})
            self.assertEqual(value["state"], "BLOCKED_BY_NO_PROGRESS")


if __name__ == "__main__":
    unittest.main()
