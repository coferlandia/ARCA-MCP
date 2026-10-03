from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_publisher.model import ReleaseError
from release_publisher.operations import build_plan, inspect_release
from release_publisher.policy import DEFAULT_POLICY


class FakeGit:
    def __init__(self) -> None:
        self.target = "c" * 40
        self.a = "a" * 40
        self.b = "b" * 40
        self.tags = {
            "v1.0.0": {"tag": "v1.0.0", "kind": "annotated", "commit": self.a},
            "v1.1.0": {"tag": "v1.1.0", "kind": "annotated", "commit": self.b},
        }
        self.refreshed = 0

    def refresh_tags(self) -> None: self.refreshed += 1
    def resolve_commit(self, revision: str) -> str: return self.target
    def is_reachable_from(self, commit: str, ref: str) -> bool: return ref.endswith("trunk")
    def remote_tag_info(self, tag: str): return self.tags.get(tag)
    def tag_info(self, tag: str): return self.tags.get(tag)
    def is_ancestor(self, ancestor: str, descendant: str) -> bool: return ancestor in {self.a, self.b} and descendant == self.target
    def distance(self, ancestor: str, descendant: str) -> int: return 2 if ancestor == self.a else 1
    def commits_between(self, previous, target): return [{"sha": self.target, "subject": "feature"}]
    def changed_paths(self, previous, target): return [{"status": "M", "path": "app.py"}]
    def tags_for_commit(self, commit): return []


class FakeGitHub:
    def __init__(self) -> None:
        self.releases = [
            {"id": 1, "tag": "v1.0.0", "draft": False, "prerelease": False, "published_at": "2026-01-01"},
            {"id": 2, "tag": "v1.1.0", "draft": False, "prerelease": False, "published_at": "2026-02-01"},
        ]
        self.checks = []
        self.immutable = {"enabled": True, "enforced_by_owner": False}

    def repository_info(self, repository): return {"repository": repository, "default_branch": "trunk"}
    def list_releases(self, repository): return list(self.releases)
    def checks_for_commit(self, repository, sha): return list(self.checks)
    def immutable_releases_status(self, repository): return dict(self.immutable)
    def release_by_tag(self, repository, tag): return next((item for item in self.releases if item["tag"] == tag), None)


class ReleaseOperationsTests(unittest.TestCase):
    def test_inspect_uses_default_branch_and_nearest_ancestor(self) -> None:
        git, github = FakeGit(), FakeGitHub()
        result = inspect_release(Path("."), "coferlandia/demo", "HEAD", copy.deepcopy(DEFAULT_POLICY), git=git, github=github)
        self.assertTrue(result["eligible"])
        self.assertEqual(result["allowed_release_refs"], ["refs/heads/trunk"])
        self.assertEqual(result["previous_release"]["tag"], "v1.1.0")
        self.assertEqual(result["target_commit"], git.target)
        self.assertEqual(git.refreshed, 1)

    def test_required_check_and_immutable_release_policy_fail_closed(self) -> None:
        git, github = FakeGit(), FakeGitHub()
        policy = copy.deepcopy(DEFAULT_POLICY)
        policy["validation"]["required_github_checks"] = ["release-ci"]
        policy["github_release"]["immutability"] = "required"
        github.checks = [{"name": "release-ci", "status": "completed", "conclusion": "failure", "url": "https://example.invalid"}]
        github.immutable = {"enabled": False, "enforced_by_owner": False}
        result = inspect_release(Path("."), "coferlandia/demo", "HEAD", policy, git=git, github=github)
        self.assertFalse(result["eligible"])
        self.assertTrue(any("not successful" in item for item in result["errors"]))
        self.assertTrue(any("not enabled" in item for item in result["errors"]))

    def test_plan_rejects_existing_non_semver_release_history(self) -> None:
        git, github = FakeGit(), FakeGitHub()
        inspection = inspect_release(Path("."), "coferlandia/demo", "HEAD", copy.deepcopy(DEFAULT_POLICY), git=git, github=github)
        inspection["non_semver_release_tags"] = ["release-2026-09"]
        with self.assertRaises(ReleaseError):
            build_plan(Path("."), "coferlandia/demo", inspection, copy.deepcopy(DEFAULT_POLICY), impact="minor", version="1.2.0", title="Demo", release_notes="Notes", git=git, github=github)

    def test_plan_is_machine_readable_and_contains_only_planned_mutations(self) -> None:
        git, github = FakeGit(), FakeGitHub()
        inspection = inspect_release(Path("."), "coferlandia/demo", "HEAD", copy.deepcopy(DEFAULT_POLICY), git=git, github=github)
        plan = build_plan(Path("."), "coferlandia/demo", inspection, copy.deepcopy(DEFAULT_POLICY), impact="minor", version="1.2.0", title="Demo", release_notes="Notes", git=git, github=github)
        self.assertEqual(plan.tag, "v1.2.0")
        self.assertEqual(plan.target_commit, git.target)
        self.assertEqual(plan.previous_release["tag"], "v1.1.0")
        self.assertEqual(plan.observed_state, "NEW")
        self.assertTrue(any("annotated tag" in item for item in plan.operations))
        self.assertTrue(any("draft GitHub Release" in item for item in plan.operations))

    def test_prerelease_flag_must_match_semver(self) -> None:
        git, github = FakeGit(), FakeGitHub()
        inspection = inspect_release(Path("."), "coferlandia/demo", "HEAD", copy.deepcopy(DEFAULT_POLICY), git=git, github=github)
        with self.assertRaises(ReleaseError):
            build_plan(Path("."), "coferlandia/demo", inspection, copy.deepcopy(DEFAULT_POLICY), impact="minor", version="1.2.0-rc.1", title="RC", release_notes="Notes", prerelease=False, git=git, github=github)


if __name__ == "__main__":
    unittest.main()
