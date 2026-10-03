from __future__ import annotations

import copy
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(TESTS))

from release_publisher.operations import build_plan, inspect_release, publish_release
from release_publisher.policy import DEFAULT_POLICY
from test_publication import StatefulGit, StatefulGitHub


class DraftResumeTests(unittest.TestCase):
    def test_existing_tag_and_draft_resume_without_duplicate_mutation(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            git, github = StatefulGit(), StatefulGitHub()
            policy = copy.deepcopy(DEFAULT_POLICY)
            inspection = inspect_release(root, "coferlandia/demo", "HEAD", policy, git=git, github=github)
            plan = build_plan(
                root,
                "coferlandia/demo",
                inspection,
                policy,
                impact="minor",
                version="1.0.0",
                title="Initial release",
                release_notes="Summary",
                provenance="disabled",
                git=git,
                github=github,
            )

            git.tags[plan.tag] = {"tag": plan.tag, "kind": "annotated", "commit": git.target}
            github.releases.append({
                "id": 42,
                "tag": plan.tag,
                "title": plan.title,
                "body": plan.release_notes,
                "draft": True,
                "prerelease": False,
                "immutable": False,
                "created_at": "2026-09-13T01:43:13Z",
                "published_at": None,
                "html_url": "https://example.invalid/draft",
                "assets": [],
            })

            result = publish_release(root, plan, git=git, github=github)

            self.assertEqual(result["status"], "published")
            self.assertEqual(result["release"]["consistency"], "pass")
            self.assertEqual(git.created, 0)
            self.assertEqual(git.pushed, 0)
            self.assertEqual(github.drafts_created, 0)
            self.assertEqual(github.published, 1)


if __name__ == "__main__":
    unittest.main()
