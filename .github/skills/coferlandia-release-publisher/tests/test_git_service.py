from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_publisher.git_service import GitService


def git(path: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(path), *args], text=True, capture_output=True, check=True)
    return result.stdout.strip()


class GitServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.repo = base / "repo"
        self.remote = base / "remote.git"
        subprocess.run(["git", "init", "--bare", str(self.remote)], check=True, capture_output=True)
        subprocess.run(["git", "init", "-b", "main", str(self.repo)], check=True, capture_output=True)
        git(self.repo, "config", "user.email", "test@example.invalid")
        git(self.repo, "config", "user.name", "Release Test")
        git(self.repo, "remote", "add", "origin", str(self.remote))
        self.commits: list[str] = []
        for index in range(6):
            (self.repo / "history.txt").write_text(f"{index}\n", encoding="utf-8")
            git(self.repo, "add", "history.txt")
            git(self.repo, "commit", "-m", f"commit {index}")
            self.commits.append(git(self.repo, "rev-parse", "HEAD"))
        git(self.repo, "push", "-u", "origin", "main")
        self.service = GitService(self.repo)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_resolves_historical_commit_without_checkout(self) -> None:
        before = git(self.repo, "rev-parse", "HEAD")
        self.assertEqual(self.service.resolve_commit(self.commits[3]), self.commits[3])
        self.assertEqual(git(self.repo, "rev-parse", "HEAD"), before)

    def test_reachability_and_ancestry(self) -> None:
        self.assertTrue(self.service.is_ancestor(self.commits[2], self.commits[5]))
        self.assertTrue(self.service.is_reachable_from(self.commits[3], "refs/heads/main"))
        git(self.repo, "checkout", "--orphan", "detached-line")
        (self.repo / "other.txt").write_text("other", encoding="utf-8")
        git(self.repo, "add", "other.txt")
        git(self.repo, "commit", "-m", "other")
        other = git(self.repo, "rev-parse", "HEAD")
        self.assertFalse(self.service.is_reachable_from(other, "refs/heads/main"))

    def test_annotated_tag_is_distinguished_and_peels_to_commit(self) -> None:
        git(self.repo, "checkout", "main")
        self.service.create_annotated_tag("v1.0.0", self.commits[3], "Release v1.0.0")
        info = self.service.tag_info("v1.0.0")
        self.assertEqual(info["kind"], "annotated")
        self.assertEqual(info["commit"], self.commits[3])
        git(self.repo, "tag", "v1.0.1", self.commits[4])
        light = self.service.tag_info("v1.0.1")
        self.assertEqual(light["kind"], "lightweight")

    def test_push_tag_never_uses_force(self) -> None:
        git(self.repo, "checkout", "main")
        self.service.create_annotated_tag("v1.0.0", self.commits[3], "Release v1.0.0")
        self.service.push_tag("v1.0.0")
        target = git(self.repo, "ls-remote", "origin", "refs/tags/v1.0.0^{}")
        self.assertIn(self.commits[3], target)

    def test_dirty_worktree_does_not_change_identity_queries(self) -> None:
        git(self.repo, "checkout", "main")
        (self.repo / "uncommitted.txt").write_text("dirty", encoding="utf-8")
        self.assertEqual(self.service.resolve_commit(self.commits[3]), self.commits[3])
        self.assertTrue(self.service.is_reachable_from(self.commits[3], "refs/heads/main"))


if __name__ == "__main__":
    unittest.main()
