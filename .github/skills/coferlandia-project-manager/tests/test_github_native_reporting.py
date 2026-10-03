from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

LIB = Path(__file__).resolve().parents[1] / "scripts" / "lib" / "reporting.py"
spec = importlib.util.spec_from_file_location("reporting", LIB)
reporting = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(reporting)


def _runtime_module():
    """Return the module whose globals back the exported reporting functions."""
    return getattr(reporting, "_core", reporting)


class GitHubNativeReportingTests(unittest.TestCase):
    def init_repo(self, root: Path) -> None:
        subprocess.run(["git", "init", "-q", str(root)], check=True)
        subprocess.run(["git", "-C", str(root), "checkout", "-q", "-b", "main"], check=True)
        subprocess.run(["git", "-C", str(root), "config", "user.email", "test@example.com"], check=True)
        subprocess.run(["git", "-C", str(root), "config", "user.name", "Test"], check=True)
        for name in ("README.md", "AGENTS.md", "DECISIONS.md", "RUNBOOK.md"):
            (root / name).write_text(f"# {name}\n", encoding="utf-8")
        subprocess.run(["git", "-C", str(root), "add", "."], check=True)
        subprocess.run(["git", "-C", str(root), "commit", "-q", "-m", "init"], check=True)

    @staticmethod
    def fake_gh_json(project_path: Path, args: list[str]):
        del project_path
        if args[:2] == ["issue", "list"]:
            return [
                {"number": 11, "title": "Implement sync", "state": "OPEN", "stateReason": None, "url": "https://github.com/acme/repo/issues/11", "createdAt": "2026-07-01T00:00:00Z", "updatedAt": "2026-07-27T00:00:00Z", "closedAt": None, "labels": [], "assignees": [], "parent": None, "subIssuesSummary": {}, "blockedBy": [], "blocking": [], "projectItems": []},
                {"number": 12, "title": "Review auth", "state": "OPEN", "stateReason": None, "url": "https://github.com/acme/repo/issues/12", "createdAt": "2026-07-01T00:00:00Z", "updatedAt": "2026-07-27T00:00:00Z", "closedAt": None, "labels": [], "assignees": [], "parent": None, "subIssuesSummary": {}, "blockedBy": [], "blocking": [], "projectItems": []},
                {"number": 13, "title": "Blocked task", "state": "OPEN", "stateReason": None, "url": "https://github.com/acme/repo/issues/13", "createdAt": "2026-07-01T00:00:00Z", "updatedAt": "2026-07-27T00:00:00Z", "closedAt": None, "labels": [], "assignees": [], "parent": None, "subIssuesSummary": {}, "blockedBy": [{"number": 99}], "blocking": [], "projectItems": []},
            ], None
        if args[:2] == ["project", "item-list"]:
            return {"items": [
                {"content": {"number": 11}, "status": "In Progress"},
                {"content": {"number": 12}, "status": "Review"},
                {"content": {"number": 13}, "status": "In Progress"},
            ]}, None
        if args[:2] == ["issue", "view"]:
            number = int(args[2])
            return {"number": number, "title": "Issue", "state": "OPEN", "url": f"https://github.com/acme/repo/issues/{number}"}, None
        return None, f"unsupported fake gh args: {args}"

    def test_portfolio_reads_github_issues_and_project_status(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            base = Path(td)
            repo = base / "repo"
            repo.mkdir()
            self.init_repo(repo)
            projects = base / "projects.json"
            projects.write_text(json.dumps({"version": 1, "projects": [{"slug": "repo", "path": str(repo), "status": "active", "repository": "acme/repo", "github_project": {"owner": "acme", "number": 1}}]}), encoding="utf-8")

            runtime = _runtime_module()
            original_gh_json = runtime._gh_json
            runtime._gh_json = self.fake_gh_json
            try:
                args = type("A", (), {"projects_file": projects, "default_branch": "main", "stale_days": 30})()
                payload = reporting.cmd_portfolio_report(args)
            finally:
                runtime._gh_json = original_gh_json

            summary = payload["summary"]
            self.assertEqual(summary["open_issues"], 3)
            self.assertEqual(summary["issues_in_progress"], 1)
            self.assertEqual(summary["issues_in_review"], 1)
            self.assertEqual(summary["blocked_issues"], 1)
            self.assertEqual(payload["projects"][0]["operational_mode"], "github")

    def test_unresolved_legacy_repo_uses_explicit_migration_mode(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            base = Path(td)
            repo = base / "legacy"
            repo.mkdir()
            self.init_repo(repo)
            (repo / "TODO.md").write_text("- [ ] TASK-1 Legacy work\n", encoding="utf-8")
            (repo / "HISTORY.md").write_text("# History\n", encoding="utf-8")
            projects = base / "projects.json"
            projects.write_text(json.dumps({"version": 1, "projects": [{"slug": "legacy", "path": str(repo), "status": "active"}]}), encoding="utf-8")

            runtime = _runtime_module()
            original_repo_name = runtime._repo_name
            runtime._repo_name = lambda path, entry=None: None
            try:
                args = type("A", (), {"projects_file": projects, "default_branch": "main", "stale_days": 30})()
                payload = reporting.cmd_portfolio_report(args)
            finally:
                runtime._repo_name = original_repo_name

            project = payload["projects"][0]
            self.assertEqual(project["operational_mode"], "legacy-migration")
            self.assertFalse(project["github_native"])
            self.assertEqual(len(project["open_issues"]), 1)


if __name__ == "__main__":
    unittest.main()
