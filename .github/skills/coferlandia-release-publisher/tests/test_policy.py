from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_publisher.policy import DEFAULT_POLICY, load_policy


class PolicyTests(unittest.TestCase):
    def test_missing_policy_uses_safe_defaults(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            policy = load_policy(Path(temp))
        self.assertEqual(policy["schema_version"], 1)
        self.assertEqual(policy["versioning"]["scheme"], "semver")
        self.assertEqual(policy["versioning"]["tag_prefix"], "v")
        self.assertEqual(policy["release_refs"], [])
        self.assertEqual(policy["tag"]["type"], "annotated")
        self.assertEqual(policy["github_release"]["immutability"], "observe")
        self.assertNotIn("publication", policy)

    def test_explicit_policy_wins_over_default_path(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            default = root / ".coferlandia/release/policy.json"
            default.parent.mkdir(parents=True)
            default.write_text(json.dumps({**DEFAULT_POLICY, "release_refs": ["refs/heads/main"]}), encoding="utf-8")
            explicit = root / "custom.json"
            explicit.write_text(json.dumps({**DEFAULT_POLICY, "release_refs": ["refs/heads/release/1.x"]}), encoding="utf-8")
            policy = load_policy(root, explicit)
        self.assertEqual(policy["release_refs"], ["refs/heads/release/1.x"])

    def test_unknown_schema_and_unsupported_tag_type_fail(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "policy.json"
            path.write_text(json.dumps({**DEFAULT_POLICY, "schema_version": 2}), encoding="utf-8")
            with self.assertRaises(ValueError):
                load_policy(root, path)
            broken = json.loads(json.dumps(DEFAULT_POLICY))
            broken["tag"]["type"] = "lightweight"
            path.write_text(json.dumps(broken), encoding="utf-8")
            with self.assertRaises(ValueError):
                load_policy(root, path)

    def test_workflow_dispatch_publication_transport_is_validated_without_owning_sibling_fields(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "policy.json"
            policy = json.loads(json.dumps(DEFAULT_POLICY))
            policy["publication"] = {
                "publisher_skill": "coferlandia-release-publisher",
                "first_release_version": "control-decision-required",
                "github": {
                    "mode": "workflow-dispatch",
                    "workflow": ".github/workflows/release-publish.yml",
                },
            }
            path.write_text(json.dumps(policy), encoding="utf-8")
            loaded = load_policy(root, path)
        self.assertEqual(loaded["publication"]["publisher_skill"], "coferlandia-release-publisher")
        self.assertEqual(loaded["publication"]["github"]["workflow"], ".github/workflows/release-publish.yml")

    def test_invalid_publication_transport_fails_closed(self) -> None:
        invalid_github_values = [
            "workflow-dispatch",
            {"mode": "other", "workflow": ".github/workflows/release.yml"},
            {"mode": "workflow-dispatch"},
            {"mode": "workflow-dispatch", "workflow": "release.yml"},
            {"mode": "workflow-dispatch", "workflow": ".github/workflows/release.txt"},
            {"mode": "workflow-dispatch", "workflow": ".github/workflows/../release.yml"},
            {"mode": "workflow-dispatch", "workflow": "./.github/workflows/release.yml"},
            {"mode": "none", "workflow": ".github/workflows/release.yml"},
        ]
        for github in invalid_github_values:
            with self.subTest(github=github), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                path = root / "policy.json"
                policy = json.loads(json.dumps(DEFAULT_POLICY))
                policy["publication"] = {"github": github}
                path.write_text(json.dumps(policy), encoding="utf-8")
                with self.assertRaises(ValueError):
                    load_policy(root, path)

    def test_policy_loading_never_creates_default_file(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            load_policy(root)
            self.assertFalse((root / ".coferlandia/release/policy.json").exists())


if __name__ == "__main__":
    unittest.main()
