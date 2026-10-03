"""Black-box and contract tests for project-orchestrator v2."""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL = Path(__file__).resolve().parents[1]
CLI = SKILL / "scripts" / "project-orchestrator-cli.py"
sys.path.insert(0, str(SKILL / "scripts"))


def run_cli(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run([sys.executable, str(CLI), *args], cwd=repo, text=True, capture_output=True)


class CliTests(unittest.TestCase):
    def make_repo(self) -> Path:
        directory = Path(tempfile.mkdtemp())
        subprocess.run(["git", "init", "-b", "main", str(directory)], check=True, capture_output=True)
        subprocess.run(["git", "-C", str(directory), "config", "user.email", "test@example.invalid"], check=True)
        subprocess.run(["git", "-C", str(directory), "config", "user.name", "Test"], check=True)
        subprocess.run(["git", "-C", str(directory), "config", "core.autocrlf", "false"], check=True)
        (directory / "README.md").write_text("test\n", encoding="utf-8")
        subprocess.run(["git", "-C", str(directory), "add", "README.md"], check=True)
        subprocess.run(["git", "-C", str(directory), "commit", "-m", "initial"], check=True, capture_output=True)
        return directory

    def commit_all(self, repo: Path, message: str = "fixture") -> None:
        subprocess.run(["git", "-C", str(repo), "add", "."], check=True)
        subprocess.run(["git", "-C", str(repo), "commit", "-m", message], check=True, capture_output=True)

    def write_fake_provider(self, repo: Path, *, request_one_fix: bool = False) -> Path:
        fake = repo / "fake-provider.py"
        fake.write_text(
            """#!/usr/bin/env python3
import json, pathlib, re, sys
prompt = sys.stdin.read()
role = 'code-reviewer' if 'code-reviewer' in prompt else ('completion-verifier' if 'completion-verifier' in prompt else ('fix-agent' if 'fix-agent' in prompt else 'coding-agent'))
candidate = re.search(r'Candidate: ([0-9a-f]+)', prompt)
result = {'protocol_version':'1.0','role':role,'status':'approved' if role == 'code-reviewer' else 'completed','remaining_work':[],'requirement_completion':[],'findings':[],'candidate_commit':candidate.group(1) if candidate else '','base_commit':'base','changed_files':[],'tests':[],'blockers':[],'scope_deviations':[]}
readme = pathlib.Path('README.md')
if role == 'coding-agent':
    readme.write_text(readme.read_text() + 'coding:' + re.search(r'Assigned work item: ([^ ]+)', prompt).group(1) + '\\n')
if role == 'code-reviewer' and 'Holistic Epic review: no' in prompt and pathlib.Path('.request-one-fix').exists() and not pathlib.Path('.fix-applied').exists():
    result['status'] = 'changes-required'; result['findings'] = [{'severity':'medium','reason':'fixture requests one additive fix'}]
if role == 'fix-agent':
    readme.write_text(readme.read_text() + 'review-fix\\n'); pathlib.Path('.fix-applied').write_text('yes')
if role == 'coding-agent':
    result = {k: result[k] for k in ('protocol_version','role','status','remaining_work','changed_files','tests','blockers','scope_deviations')}
elif role == 'completion-verifier':
    result = {k: result[k] for k in ('protocol_version','role','status','requirement_completion','remaining_work','blockers','scope_deviations','tests')}
elif role == 'fix-agent':
    result = {k: result[k] for k in ('protocol_version','role','status','findings','changed_files','tests','blockers')}
elif role == 'code-reviewer':
    result = {k: result[k] for k in ('protocol_version','role','status','candidate_commit','base_commit','findings','tests','scope_deviations')}
print(json.dumps(result))
""",
            encoding="utf-8",
        )
        fake.chmod(0o755)
        if request_one_fix:
            (repo / ".request-one-fix").write_text("yes\n", encoding="utf-8")
        return fake

    def write_config(self, repo: Path, fake: Path) -> None:
        config = {
            "version": 2,
            "git": {
                "base_branch": "main",
                "branch_prefix": "orchestrator",
                "worktree_root": "../.worktrees",
                "require_clean_base_worktree": True,
                "remove_review_worktree_after_review": True,
                "remove_implementation_worktree_after_integration": True,
                "delete_epic_branch_after_integration": True,
            },
            "roles": {role: {"primary": {"client": "codex", "model": "fake"}} for role in ("orchestrator", "coding_agent", "completion_verifier", "code_reviewer", "fix_agent")},
            "providers": {"codex": {"command": str(fake), "enabled": True}, "opencode": {"enabled": False}},
            "retry": {"provider_wait_seconds": 1},
            "timeouts": {"coding_seconds": 5, "completion_verification_seconds": 5, "review_seconds": 5, "fix_seconds": 5, "process_termination_grace_seconds": 1, "test_seconds": 5},
            "loops": {"max_no_progress_cycles": 3},
            "protocol": {},
            "validation": {"commands": []},
        }
        directory = repo / ".project-orchestrator"
        directory.mkdir(exist_ok=True)
        (directory / "config.json").write_text(json.dumps(config), encoding="utf-8")

    def test_capabilities_expose_v2_modes_and_integrate(self) -> None:
        result = run_cli(self.make_repo(), "capabilities", "--json")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        payload = json.loads(result.stdout)
        self.assertEqual(payload["result"]["version"], "2.0.0")
        self.assertEqual(payload["result"]["execution_modes"], ["direct-plan", "task-execution"])
        self.assertIn("integrate", [item["name"] for item in payload["result"]["commands"]])

    def test_run_sources_are_mutually_exclusive(self) -> None:
        result = run_cli(self.make_repo(), "run", "--spec", "a.md", "--manifest", "b.json", "--json")
        self.assertNotEqual(result.returncode, 0)

    def test_direct_plan_dry_run_is_one_execution_unit_even_with_many_headings(self) -> None:
        repo = self.make_repo()
        spec = Path(tempfile.mkdtemp()) / "plan.md"
        spec.write_text("# Phase 1: first\n\n# Phase 2: second\n", encoding="utf-8")
        result = run_cli(repo, "run", "--spec", str(spec), "--dry-run", "--json")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        manifest = json.loads(result.stdout)["result"]["manifest"]
        self.assertEqual(manifest["execution_mode"], "direct-plan")
        self.assertEqual([item["id"] for item in manifest["tasks"]], ["DIRECT-PLAN"])
        branches = subprocess.run(["git", "-C", str(repo), "branch", "--list", "orchestrator/*"], text=True, capture_output=True).stdout
        self.assertEqual(branches.strip(), "")

    def test_manifest_rejects_cycle_and_missing_dependency(self) -> None:
        from project_orchestrator_cli.contracts import ValidationError
        from project_orchestrator_cli.work_items import validate_manifest

        base = {
            "schema_version": 2,
            "execution_mode": "task-execution",
            "source": {"kind": "local"},
            "epic": {"id": "E", "path": "EPIC.md"},
            "final_pr": None,
            "squash_sha": None,
        }
        with self.assertRaises(ValidationError):
            validate_manifest({**base, "tasks": [{"id": "A", "path": "A.md", "depends_on": ["MISSING"]}]})
        with self.assertRaises(ValidationError):
            validate_manifest({**base, "tasks": [{"id": "A", "path": "A.md", "depends_on": ["B"]}, {"id": "B", "path": "B.md", "depends_on": ["A"]}]})

    def test_manifest_topological_order_is_deterministic(self) -> None:
        from project_orchestrator_cli.work_items import validate_manifest

        manifest = validate_manifest({
            "schema_version": 2,
            "execution_mode": "task-execution",
            "source": {"kind": "local"},
            "epic": {"id": "E", "path": "EPIC.md"},
            "tasks": [
                {"id": "B", "path": "B.md", "depends_on": ["A"]},
                {"id": "C", "path": "C.md", "depends_on": []},
                {"id": "A", "path": "A.md", "depends_on": []},
            ],
        })
        self.assertEqual(manifest["execution_order"], ["A", "C", "B"])

    def test_git_staging_excludes_github_projection_files(self) -> None:
        from project_orchestrator_cli.git_service import GitService

        repo = self.make_repo()
        projection = repo / ".agent" / "work-items" / "epic-1" / "TASK.md"
        projection.parent.mkdir(parents=True)
        projection.write_text("projection\n", encoding="utf-8")
        (repo / "README.md").write_text("product change\n", encoding="utf-8")
        selected = GitService(repo).stage_product_changes(repo, include_work_items=False)
        self.assertEqual(selected, ["README.md"])
        staged = subprocess.run(["git", "-C", str(repo), "diff", "--cached", "--name-only"], text=True, capture_output=True).stdout.splitlines()
        self.assertEqual(staged, ["README.md"])

    def test_commit_messages_link_issue_and_epic_without_closing_keywords(self) -> None:
        from project_orchestrator_cli.engine import _commit_message
        manifest = {"source": {"kind": "github", "epic_issue": 1}}
        task = {"id": "TASK-2", "issue": 2, "title": "Implement thing"}
        candidate = _commit_message(manifest, task, kind="candidate")
        fix = _commit_message(manifest, task, kind="review-fix", review_round=1)
        self.assertIn("Issue: #2", candidate)
        self.assertIn("Epic: #1", candidate)
        self.assertIn("Review: round 1", fix)
        for word in ("Closes", "Fixes", "Resolves"):
            self.assertNotIn(word, candidate)
            self.assertNotIn(word, fix)

    def test_final_pr_body_owns_closing_references(self) -> None:
        from project_orchestrator_cli.integration import _pr_body
        state = {
            "final_reviewed_sha": "abc",
            "manifest": {
                "source": {"kind": "github", "epic_issue": 1},
                "execution_order": ["TASK-2", "TASK-3"],
                "tasks": [
                    {"id": "TASK-2", "issue": 2, "title": "A"},
                    {"id": "TASK-3", "issue": 3, "title": "B"},
                ],
            },
            "task_reviews": {},
            "holistic_reviews": [{}],
        }
        body = _pr_body(state)
        self.assertIn("Closes #2", body)
        self.assertIn("Closes #3", body)
        self.assertIn("Closes #1", body)

    def test_fake_provider_executes_two_tasks_in_one_epic_worktree_without_merging_main(self) -> None:
        repo = self.make_repo()
        fake = self.write_fake_provider(repo)
        self.write_config(repo, fake)
        contracts = repo / ".agent" / "work-items" / "local-epic"
        (contracts / "tasks").mkdir(parents=True)
        (contracts / "EPIC.md").write_text("# Local Epic\n", encoding="utf-8")
        (contracts / "tasks" / "TASK-A.md").write_text("# A\n", encoding="utf-8")
        (contracts / "tasks" / "TASK-B.md").write_text("# B\n", encoding="utf-8")
        manifest = {
            "schema_version": 2,
            "execution_mode": "task-execution",
            "source": {"kind": "local", "source_hash": "fixture", "contract_revision": 1},
            "epic": {"id": "LOCAL-EPIC", "title": "Local Epic", "path": ".agent/work-items/local-epic/EPIC.md"},
            "tasks": [
                {"id": "TASK-A", "title": "A", "path": ".agent/work-items/local-epic/tasks/TASK-A.md", "depends_on": [], "status": "pending", "commits": []},
                {"id": "TASK-B", "title": "B", "path": ".agent/work-items/local-epic/tasks/TASK-B.md", "depends_on": ["TASK-A"], "status": "pending", "commits": []},
            ],
        }
        (contracts / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        self.commit_all(repo)
        main_before = subprocess.run(["git", "-C", str(repo), "rev-parse", "main"], text=True, capture_output=True, check=True).stdout.strip()
        result = run_cli(repo, "run", "--manifest", str(contracts / "manifest.json"), "--run-id", "v2-two-task", "--json")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        state = json.loads(result.stdout)["result"]
        self.assertEqual(state["state"], "EPIC_READY_FOR_INTEGRATION", result.stdout)
        self.assertEqual(subprocess.run(["git", "-C", str(repo), "rev-parse", "main"], text=True, capture_output=True, check=True).stdout.strip(), main_before)
        worktree = Path(state["resources"]["implementation_worktree"])
        self.assertTrue(worktree.exists())
        history = subprocess.run(["git", "-C", str(worktree), "log", "--format=%s", "--reverse", f"{main_before}..HEAD"], text=True, capture_output=True, check=True).stdout.splitlines()
        self.assertEqual(len(history), 2)
        self.assertIn("A", history[0])
        self.assertIn("B", history[1])
        self.assertIn("coding:TASK-A", (worktree / "README.md").read_text())
        self.assertIn("coding:TASK-B", (worktree / "README.md").read_text())

    def test_review_fix_creates_additive_commit_and_new_review(self) -> None:
        repo = self.make_repo()
        fake = self.write_fake_provider(repo, request_one_fix=True)
        self.write_config(repo, fake)
        self.commit_all(repo)
        spec = Path(tempfile.mkdtemp()) / "plan.md"
        spec.write_text("Implement the fixture.\n", encoding="utf-8")
        result = run_cli(repo, "run", "--spec", str(spec), "--run-id", "v2-fix", "--json")
        self.assertEqual(result.returncode, 0, result.stderr + result.stdout)
        state = json.loads(result.stdout)["result"]
        self.assertEqual(state["state"], "EPIC_READY_FOR_INTEGRATION")
        task = state["manifest"]["tasks"][0]
        commits = task["commits"]
        self.assertEqual([item["kind"] for item in commits], ["candidate", "review-fix"])
        self.assertNotEqual(commits[0]["sha"], commits[1]["sha"])
        self.assertGreaterEqual(len(state["task_reviews"]["DIRECT-PLAN"]), 2)

    def test_validate_result_rejects_wrong_role(self) -> None:
        repo = self.make_repo()
        result_file = repo / "result.json"
        result_file.write_text(json.dumps({"protocol_version": "1.0", "role": "code-reviewer", "status": "approved"}), encoding="utf-8")
        result = run_cli(repo, "validate-result", "--role", "coding-agent", "--file", str(result_file), "--json")
        self.assertEqual(result.returncode, 3)
        self.assertEqual(json.loads(result.stdout)["status"], "failure")

    def test_run_lock_rejects_second_owner(self) -> None:
        from project_orchestrator_cli.state import RunLock
        common = Path(tempfile.mkdtemp())
        first = RunLock(common, "run-lock")
        second = RunLock(common, "run-lock")
        first.acquire()
        try:
            with self.assertRaises(Exception):
                second.acquire()
        finally:
            first.release()

    def test_simulated_clock_advances_retry_without_waiting(self) -> None:
        from project_orchestrator_cli.state import SimulatedClock, retry_due
        clock = SimulatedClock()
        retry_at = (clock.now() + __import__("datetime").timedelta(seconds=300)).isoformat()
        self.assertFalse(retry_due(retry_at, clock.now()))
        clock.sleep(300)
        self.assertTrue(retry_due(retry_at, clock.now()))


if __name__ == "__main__":
    unittest.main()
