from __future__ import annotations

import copy
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_publisher.model import ReleaseError
from release_publisher.operations import build_plan, inspect_release, publish_release, verify_release
from release_publisher.policy import DEFAULT_POLICY


class StatefulGit:
    def __init__(self) -> None:
        self.target = "c" * 40
        self.tags: dict[str, dict[str, str]] = {}
        self.created = 0
        self.pushed = 0
        self.refreshed = 0

    def refresh_tags(self) -> None: self.refreshed += 1
    def resolve_commit(self, revision: str) -> str: return self.target
    def is_reachable_from(self, commit: str, ref: str) -> bool: return commit == self.target and ref.endswith("main")
    def remote_tag_info(self, tag: str): return self.tags.get(tag)
    def tag_info(self, tag: str): return self.tags.get(tag)
    def list_tags(self): return sorted(self.tags)
    def tags_for_commit(self, commit: str): return sorted(tag for tag, item in self.tags.items() if item["commit"] == commit)
    def is_ancestor(self, ancestor: str, descendant: str) -> bool: return ancestor == descendant
    def distance(self, ancestor: str, descendant: str) -> int: return 0 if ancestor == descendant else 1
    def commits_between(self, previous, target): return [{"sha": target, "subject": "release feature"}]
    def changed_paths(self, previous, target): return [{"status": "M", "path": "app.py"}]

    def create_annotated_tag(self, tag: str, commit: str, message: str, sign: bool = False) -> None:
        existing = self.tags.get(tag)
        if existing and (existing["kind"] != "annotated" or existing["commit"] != commit):
            raise ReleaseError("conflicting tag")
        if not existing:
            self.tags[tag] = {"tag": tag, "kind": "annotated", "commit": commit}
            self.created += 1

    def push_tag(self, tag: str) -> None:
        self.pushed += 1


class StatefulGitHub:
    def __init__(self) -> None:
        self.releases: list[dict] = []
        self.contents: dict[int, str] = {}
        self.next_release = 1
        self.next_asset = 100
        self.drafts_created = 0
        self.published = 0
        self.immutable_status = {"enabled": False, "enforced_by_owner": False}

    def repository_info(self, repository): return {"repository": repository, "default_branch": "main"}
    def list_releases(self, repository): return [copy.deepcopy(item) for item in self.releases]
    def checks_for_commit(self, repository, sha): return []
    def immutable_releases_status(self, repository): return dict(self.immutable_status)

    def release_by_tag(self, repository, tag):
        item = next((item for item in self.releases if item["tag"] == tag), None)
        return copy.deepcopy(item) if item else None

    def release_by_id(self, repository, release_id):
        return copy.deepcopy(next(item for item in self.releases if item["id"] == release_id))

    def create_draft_release(self, repository, tag, title, body, prerelease):
        release = {
            "id": self.next_release, "tag": tag, "title": title, "body": body,
            "draft": True, "prerelease": prerelease, "immutable": False,
            "created_at": "2026-09-05T20:00:00Z", "published_at": None,
            "html_url": "https://example.invalid/release", "assets": [],
        }
        self.next_release += 1
        self.releases.append(release)
        self.drafts_created += 1
        return copy.deepcopy(release)

    def upload_asset(self, repository, release_id, path: Path, name=None):
        data = path.read_bytes()
        asset = {
            "id": self.next_asset, "name": name or path.name, "size": len(data),
            "sha256": hashlib.sha256(data).hexdigest(), "url": "https://example.invalid/asset",
        }
        self.next_asset += 1
        release = next(item for item in self.releases if item["id"] == release_id)
        release["assets"].append(asset)
        self.contents[asset["id"]] = data.decode("utf-8")
        return copy.deepcopy(asset)

    def publish_release(self, repository, release_id):
        release = next(item for item in self.releases if item["id"] == release_id)
        release["draft"] = False
        release["published_at"] = "2026-09-05T20:01:00Z"
        self.published += 1
        return copy.deepcopy(release)

    def download_text_asset(self, repository, asset_id):
        return self.contents[asset_id]


class PublicationLifecycleTests(unittest.TestCase):
    def _plan(self, root: Path, git: StatefulGit, github: StatefulGitHub, *, provenance: str = "optional"):
        policy = copy.deepcopy(DEFAULT_POLICY)
        inspection = inspect_release(root, "coferlandia/demo", "HEAD", policy, git=git, github=github)
        return build_plan(root, "coferlandia/demo", inspection, policy, impact="minor", version="1.0.0", title="Initial release", release_notes="Summary", provenance=provenance, git=git, github=github)

    def test_new_release_publishes_once_and_second_execution_is_idempotent(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), StatefulGitHub()
            plan = self._plan(root, git, github)
            first = publish_release(root, plan, git=git, github=github)
            self.assertEqual(first["status"], "published")
            self.assertEqual(first["release"]["commit"], git.target)
            self.assertEqual(first["release"]["version"], "1.0.0")
            self.assertEqual(first["release"]["consistency"], "pass")
            self.assertEqual(git.created, 1)
            self.assertEqual(git.pushed, 1)
            self.assertEqual(github.drafts_created, 1)
            self.assertEqual(github.published, 1)
            manifest = first["release"]["provenance"]
            self.assertEqual(manifest["commit"], git.target)
            self.assertEqual(manifest["policy_fingerprint"], plan.policy_fingerprint)
            self.assertIn("validation", manifest)

            second = publish_release(root, plan, git=git, github=github)
            self.assertEqual(second["status"], "already_consistent")
            self.assertEqual(git.created, 1)
            self.assertEqual(git.pushed, 1)
            self.assertEqual(github.drafts_created, 1)
            self.assertEqual(github.published, 1)

    def test_conflicting_tag_fails_before_creating_release(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), StatefulGitHub()
            plan = self._plan(root, git, github, provenance="disabled")
            git.tags[plan.tag] = {"tag": plan.tag, "kind": "annotated", "commit": "d" * 40}
            with self.assertRaises(ReleaseError):
                publish_release(root, plan, git=git, github=github)
            self.assertEqual(github.drafts_created, 0)
            self.assertEqual(git.pushed, 0)

    def test_stale_plan_fails_before_tag_creation(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), StatefulGitHub()
            plan = self._plan(root, git, github, provenance="disabled")
            github.releases.append({
                "id": 99, "tag": "v0.9.0", "title": "external", "body": "",
                "draft": False, "prerelease": False, "immutable": False,
                "created_at": "2026-09-05T19:00:00Z", "published_at": "2026-09-05T19:01:00Z",
                "html_url": "https://example.invalid/external", "assets": [],
            })
            with self.assertRaisesRegex(ReleaseError, "stale"):
                publish_release(root, plan, git=git, github=github)
            self.assertEqual(git.created, 0)
            self.assertEqual(git.pushed, 0)

    def test_publish_rejects_tampered_plan_before_remote_mutation(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), StatefulGitHub()
            plan = self._plan(root, git, github, provenance="disabled")
            plan.tag = "v9.9.9"
            with self.assertRaisesRegex(ReleaseError, "plan"):
                publish_release(root, plan, git=git, github=github)
            self.assertEqual(git.created, 0)
            self.assertEqual(git.pushed, 0)
            self.assertEqual(github.drafts_created, 0)

    def test_verify_rejects_non_semver_and_prerelease_flag_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), StatefulGitHub()
            git.tags["release-2026"] = {"tag": "release-2026", "kind": "annotated", "commit": git.target}
            github.releases.append({
                "id": 7, "tag": "release-2026", "title": "legacy", "body": "", "draft": False,
                "prerelease": False, "immutable": False, "created_at": None, "published_at": None,
                "html_url": None, "assets": [],
            })
            legacy = verify_release(root, "coferlandia/demo", "release-2026", copy.deepcopy(DEFAULT_POLICY), git=git, github=github)
            self.assertEqual(legacy["consistency"], "fail")
            self.assertTrue(any("SemVer" in error for error in legacy["errors"]))

            git.tags.clear(); github.releases.clear()
            git.tags["v1.0.0-rc.1"] = {"tag": "v1.0.0-rc.1", "kind": "annotated", "commit": git.target}
            github.releases.append({
                "id": 8, "tag": "v1.0.0-rc.1", "title": "rc", "body": "", "draft": False,
                "prerelease": False, "immutable": False, "created_at": None, "published_at": None,
                "html_url": None, "assets": [],
            })
            drift = verify_release(root, "coferlandia/demo", "v1.0.0-rc.1", copy.deepcopy(DEFAULT_POLICY), git=git, github=github)
            self.assertEqual(drift["consistency"], "fail")
            self.assertTrue(any("prerelease flag" in error for error in drift["errors"]))


class NoDigestGitHub(StatefulGitHub):
    def upload_asset(self, repository, release_id, path: Path, name=None):
        asset = super().upload_asset(repository, release_id, path, name)
        stored = next(item for item in self.releases if item["id"] == release_id)["assets"][-1]
        asset.pop("sha256", None)
        stored.pop("sha256", None)
        return asset

    def download_asset_bytes(self, repository, asset_id):
        return self.contents[asset_id].encode("utf-8")


class AssetDigestFallbackTests(unittest.TestCase):
    def test_provenance_publish_succeeds_when_asset_digest_metadata_is_missing(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), NoDigestGitHub()
            policy = copy.deepcopy(DEFAULT_POLICY)
            inspection = inspect_release(root, "coferlandia/demo", "HEAD", policy, git=git, github=github)
            plan = build_plan(
                root, "coferlandia/demo", inspection, policy,
                impact="minor", version="1.0.0", title="Initial release",
                release_notes="Summary", provenance="optional", git=git, github=github,
            )
            result = publish_release(root, plan, git=git, github=github)
            self.assertEqual(result["status"], "published")
            self.assertEqual(result["release"]["consistency"], "pass")


if __name__ == "__main__":
    unittest.main()
