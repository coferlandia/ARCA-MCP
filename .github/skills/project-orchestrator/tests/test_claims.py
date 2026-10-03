"""Durable claim and in-progress projection tests."""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import threading
import unittest
from pathlib import Path
from unittest.mock import patch

SKILL = Path(__file__).resolve().parents[1]
CLI = SKILL / "scripts" / "project-orchestrator-cli.py"
sys.path.insert(0, str(SKILL / "scripts"))

from project_orchestrator_cli.claims import ClaimConflict, ClaimStore, _project_claim


def run_cli(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run([sys.executable, str(CLI), *args], cwd=repo, text=True, capture_output=True)


class ClaimStoreTests(unittest.TestCase):
    def test_atomic_acquisition_allows_exactly_one_owner(self) -> None:
        common = Path(tempfile.mkdtemp())
        first = ClaimStore(common)
        second = ClaimStore(common)
        key = "task:github:coferlandia/example#39"
        barrier = threading.Barrier(2)
        results: list[tuple[str, str]] = []

        def acquire(store: ClaimStore, run_id: str) -> None:
            barrier.wait()
            try:
                value = store.acquire({"claim_key": key, "run_id": run_id, "scope": "task"})
                results.append(("owner", str(value["run_id"])))
            except ClaimConflict as exc:
                results.append(("conflict", str(exc.owner["run_id"])))

        threads = [
            threading.Thread(target=acquire, args=(first, "run-a")),
            threading.Thread(target=acquire, args=(second, "run-b")),
        ]
        for thread in threads:
            thread.start()
        for thread in threads:
            thread.join()

        self.assertEqual(sorted(result[0] for result in results), ["conflict", "owner"])
        owner = first.get(key)
        self.assertIsNotNone(owner)
        self.assertEqual(
            first.acquire({"claim_key": key, "run_id": owner["run_id"], "scope": "task"})["run_id"],
            owner["run_id"],
        )

    def test_claim_filename_is_digest_and_force_release_is_audited(self) -> None:
        common = Path(tempfile.mkdtemp())
        store = ClaimStore(common)
        key = "../../unsafe/path"
        store.acquire({"claim_key": key, "run_id": "run-owner", "scope": "task"})
        self.assertEqual(store.path_for(key).parent, store.root)
        self.assertEqual(store.path_for(key).name, f"{ClaimStore.digest(key)}.json")
        with self.assertRaises(ClaimConflict):
            store.release(key, "other-run", "not owner")
        released = store.release(key, "administrator", "abandoned run", force=True)
        self.assertEqual(released["released_by"], "administrative-force")
        self.assertFalse(store.path_for(key).exists())
        history = common / "project-orchestrator" / "runs" / "run-owner" / "claim-history"
        self.assertEqual(len(list(history.glob("*.json"))), 1)

    def test_malformed_claim_fails_closed(self) -> None:
        common = Path(tempfile.mkdtemp())
        store = ClaimStore(common)
        key = "epic:local:test"
        path = store.path_for(key)
        path.parent.mkdir(parents=True)
        path.write_text("{not-json", encoding="utf-8")
        with self.assertRaises(Exception):
            store.acquire({"claim_key": key, "run_id": "run-a", "scope": "epic"})


class ProjectProjectionTests(unittest.TestCase):
    def test_configured_project_sets_in_progress_and_persists_projection(self) -> None:
        common = Path(tempfile.mkdtemp())
        store = ClaimStore(common)
        record = store.acquire({
            "claim_key": "task:github:coferlandia/example#39",
            "run_id": "run-a",
            "scope": "task",
            "project_projection": {"configured": False, "applied": False},
        })

        class FakeService:
            def __init__(self, _repo: Path):
                pass

            def project_items(self, owner: str, number: int) -> list[dict]:
                return [{"content": {"number": 39}, "status": "Todo"}]

            def issue(self, ref) -> dict:
                return {"number": ref.number, "url": "https://example.invalid/issues/39"}

            def set_project_status(self, owner: str, number: int, issue: dict, status: str) -> None:
                self.applied = (owner, number, issue["number"], status)

        config = {"github_project": {"owner": "coferlandia", "number": 2}}
        with patch("project_orchestrator_cli.claims.GitHubService", FakeService):
            updated = _project_claim(Path.cwd(), config, store, record, "coferlandia/example", 39, "in_progress")

        self.assertTrue(updated["project_projection"]["applied"])
        self.assertEqual(updated["project_projection"]["previous_status"], "Todo")
        self.assertEqual(updated["project_projection"]["current_status"], "In Progress")


class ClaimLifecycleCliTests(unittest.TestCase):
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

    def test_duplicate_direct_plan_is_blocked_until_owner_is_cancelled(self) -> None:
        repo = self.make_repo()
        plan = repo / "plan.md"
        plan.write_text("# Detailed plan\n", encoding="utf-8")
        subprocess.run(["git", "-C", str(repo), "add", "plan.md"], check=True)
        subprocess.run(["git", "-C", str(repo), "commit", "-m", "plan"], check=True, capture_output=True)

        first = run_cli(repo, "run", "--spec", str(plan), "--run-id", "claim-owner", "--json")
        self.assertEqual(first.returncode, 0, first.stderr + first.stdout)
        second = run_cli(repo, "run", "--spec", str(plan), "--run-id", "claim-contender", "--json")
        self.assertNotEqual(second.returncode, 0)
        self.assertIn("already claimed", second.stdout)

        cancelled = run_cli(repo, "cancel", "claim-owner", "--json")
        self.assertEqual(cancelled.returncode, 0, cancelled.stderr + cancelled.stdout)
        retried = run_cli(repo, "run", "--spec", str(plan), "--run-id", "claim-contender", "--json")
        self.assertEqual(retried.returncode, 0, retried.stderr + retried.stdout)

        listed = run_cli(repo, "claims", "list", "--json")
        self.assertEqual(listed.returncode, 0, listed.stderr + listed.stdout)
        claims = json.loads(listed.stdout)["result"]["claims"]
        self.assertEqual([claim["run_id"] for claim in claims], ["claim-contender"])


if __name__ == "__main__":
    unittest.main()
