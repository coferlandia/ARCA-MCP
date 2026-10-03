from __future__ import annotations

import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
REPORTING = SKILL_ROOT / "scripts" / "lib" / "reporting.py"
ARCHIVIST = SKILL_ROOT / "scripts" / "lib" / "archivist.py"
BOARD_ACTIONS = SKILL_ROOT / "scripts" / "lib" / "board_actions.py"


class GitHubNativePhase1ContractTests(unittest.TestCase):
    def test_pm_contract_is_github_native(self) -> None:
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("GitHub Issues", text)
        self.assertIn("GitHub Projects", text)
        self.assertIn("Never recreate TODO.md/HISTORY.md as PM state", text)
        self.assertIn("Obsidian", text)
        self.assertIn("projection", text.lower())
        self.assertNotIn("## Feature Pipeline", text)
        self.assertNotIn("## Bug Pipeline", text)
        self.assertNotIn("## Required Task Statuses", text)

    def test_pm_config_keeps_design_superpowers_only(self) -> None:
        config = json.loads((SKILL_ROOT / "templates" / "config.template.json").read_text(encoding="utf-8"))
        required = set(config["superpowers"]["required_skills"])
        optional = set(config["superpowers"]["optional_skills"])
        self.assertEqual(required, {"brainstorming", "writing-plans"})
        self.assertIn("preserving-productive-tensions", optional)
        self.assertIn("writing-skills", optional)
        self.assertNotIn("executing-plans", required)
        self.assertNotIn("using-git-worktrees", required)
        self.assertNotIn("checkpointing", config)
        self.assertNotIn("execution", config)

    def test_obsidian_templates_are_github_projections(self) -> None:
        project = (SKILL_ROOT / "templates" / "obsidian-project.template.md").read_text(encoding="utf-8")
        issue = (SKILL_ROOT / "templates" / "obsidian-task.template.md").read_text(encoding="utf-8")
        self.assertIn("repository:", project)
        self.assertIn("github_project:", project)
        self.assertIn("github_native:", project)
        self.assertIn('source: "github"', issue)
        self.assertIn("github_issue:", issue)
        self.assertIn("github_url:", issue)

    def test_changed_python_modules_compile(self) -> None:
        for module in (REPORTING, ARCHIVIST, BOARD_ACTIONS):
            result = subprocess.run(["python", "-m", "py_compile", str(module)], text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_changed_shell_entrypoints_are_syntax_clean(self) -> None:
        for script in (
            SKILL_ROOT / "scripts" / "pm-manage-projects.sh",
            SKILL_ROOT / "scripts" / "pm-task-report.sh",
        ):
            result = subprocess.run(["bash", "-n", str(script)], text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_project_registry_can_store_github_coordinates(self) -> None:
        # Static contract check: the manager must accept both repository and Project coordinates.
        text = (SKILL_ROOT / "scripts" / "pm-manage-projects.sh").read_text(encoding="utf-8")
        self.assertIn("--repository", text)
        self.assertIn("--github-project-owner", text)
        self.assertIn("--github-project-number", text)
        self.assertIn("configure-github", text)

    def test_task_report_requires_github_issue_identity(self) -> None:
        spec = importlib.util.spec_from_file_location("phase1_reporting", REPORTING)
        module = importlib.util.module_from_spec(spec)
        assert spec and spec.loader
        spec.loader.exec_module(module)
        self.assertEqual(module._parse_issue_reference("secretaria#142"), ("secretaria", None, 142))
        self.assertEqual(module._parse_issue_reference("#142"), (None, None, 142))
        self.assertEqual(module._parse_issue_reference("https://github.com/acme/repo/issues/142"), (None, "acme/repo", 142))
        self.assertEqual(module._parse_issue_reference("not-an-issue"), (None, None, None))

    def test_report_templates_no_longer_expose_old_task_state_machine(self) -> None:
        portfolio = (SKILL_ROOT / "templates" / "portfolio-report.template.md").read_text(encoding="utf-8")
        project = (SKILL_ROOT / "templates" / "project-report.template.md").read_text(encoding="utf-8")
        task = (SKILL_ROOT / "templates" / "task-report.template.md").read_text(encoding="utf-8")
        self.assertIn("Open Issues", portfolio)
        self.assertIn("GitHub-native", project)
        self.assertIn("Issue", task)
        self.assertNotIn("Ready-for-agent", portfolio)
        self.assertNotIn("Review state", task)


if __name__ == "__main__":
    unittest.main()
