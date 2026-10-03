import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
CLI = SKILL_ROOT / "scripts" / "coferlandia-ci-adapter-cli.py"
PKG = SKILL_ROOT / "scripts" / "coferlandia_ci_adapter_cli" / "profile.py"


def load_profile_module():
    spec = importlib.util.spec_from_file_location("profile_module", PKG)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class AdapterCLITests(unittest.TestCase):
    def sample(self):
        return {
            "schema_version": 1,
            "profile_version": "1.0.0",
            "repository": "example/repo",
            "documentation": ["AGENTS.md"],
            "local": {
                "working_directory": ".",
                "qualification_commands": [
                    {"id": "canonical", "command": "python -m unittest", "purpose": "canonical qualification"}
                ],
                "required_services": [],
                "environment": ["CI_MODE"]
            },
            "github": {
                "submission": {"mode": "existing-pr-events", "workflow": ".github/workflows/ci.yml"},
                "gates": [
                    {"id": "ci", "kind": "workflow", "workflow": ".github/workflows/ci.yml", "allowed_conclusions": ["success"]}
                ],
                "merge_group": {"supported": False, "authoritative_when_present": False}
            },
            "identity": {"base_sensitive": True, "profile_sensitive": True},
            "exceptions": []
        }

    def test_validation_and_fingerprint_are_deterministic(self):
        module = load_profile_module()
        profile = self.sample()
        module.validate_profile(profile)
        fp1 = module.profile_fingerprint(profile)
        reordered = json.loads(json.dumps(profile, sort_keys=True))
        fp2 = module.profile_fingerprint(reordered)
        self.assertEqual(fp1, fp2)
        with_fingerprint = dict(profile, fingerprint=fp1)
        module.validate_profile(with_fingerprint, verify_fingerprint=True)

    def test_rejects_unsafe_or_ambiguous_profiles(self):
        module = load_profile_module()
        profile = self.sample()
        profile["token"] = "abc"
        with self.assertRaises(ValueError):
            module.validate_profile(profile)
        profile = self.sample()
        profile["github"]["gates"][0]["allowed_conclusions"] = []
        with self.assertRaises(ValueError):
            module.validate_profile(profile)
        profile = self.sample()
        profile["local"]["qualification_commands"] = []
        with self.assertRaises(ValueError):
            module.validate_profile(profile)
        profile = self.sample()
        profile["local"]["environment"] = ["API_TOKEN=super-secret"]
        with self.assertRaises(ValueError):
            module.validate_profile(profile)

    def test_cli_validate_fingerprint_and_render(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            source = root / "plan.json"
            source.write_text(json.dumps(self.sample()), encoding="utf-8")
            validate = subprocess.run([sys.executable, str(CLI), "profile", "validate", "--profile", str(source), "--json"], text=True, capture_output=True)
            self.assertEqual(validate.returncode, 0, validate.stderr or validate.stdout)
            fp = subprocess.run([sys.executable, str(CLI), "profile", "fingerprint", "--profile", str(source), "--json"], text=True, capture_output=True)
            self.assertEqual(fp.returncode, 0, fp.stderr or fp.stdout)
            fingerprint = json.loads(fp.stdout)["fingerprint"]
            dry = subprocess.run([sys.executable, str(CLI), "profile", "render", "--input", str(source), "--target-root", str(root), "--dry-run", "--json"], text=True, capture_output=True)
            self.assertEqual(dry.returncode, 0, dry.stderr or dry.stdout)
            target = root / ".coferlandia" / "ci" / "profile.json"
            self.assertFalse(target.exists())
            apply = subprocess.run([sys.executable, str(CLI), "profile", "render", "--input", str(source), "--target-root", str(root), "--json"], text=True, capture_output=True)
            self.assertEqual(apply.returncode, 0, apply.stderr or apply.stdout)
            self.assertTrue(target.exists())
            stored = json.loads(target.read_text(encoding="utf-8"))
            self.assertEqual(stored["fingerprint"], fingerprint)

    def test_publication_render_preserves_repo_fields_and_materializes_workflow(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            source = root / "policy.json"
            source.write_text(json.dumps({
                "schema_version": 1,
                "publication": {
                    "publisher_skill": "coferlandia-release-publisher",
                    "first_release_version": "control-decision-required"
                }
            }), encoding="utf-8")
            publisher = ".agents/skills/coferlandia-release-publisher/scripts/coferlandia-release.py"
            result = subprocess.run([
                sys.executable, str(CLI), "publication", "render",
                "--policy", str(source),
                "--target-root", str(root),
                "--publisher-path", publisher,
                "--json"
            ], text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr or result.stdout)
            policy_target = root / ".coferlandia" / "release" / "policy.json"
            workflow_target = root / ".github" / "workflows" / "coferlandia-release-publish.yml"
            stored = json.loads(policy_target.read_text(encoding="utf-8"))
            self.assertEqual(stored["publication"]["publisher_skill"], "coferlandia-release-publisher")
            self.assertEqual(stored["publication"]["github"]["mode"], "issue-comment")
            self.assertEqual(stored["publication"]["github"]["runs_on"], "ubuntu-latest")
            workflow = workflow_target.read_text(encoding="utf-8")
            for token in (
                "issue_comment:",
                "coferlandia-release-publication-request:v1",
                "target_sha",
                "version",
                "impact",
                "title",
                "notes",
                "permissions:",
                "contents: write",
                'runs-on: "ubuntu-latest"',
                "'admin', 'maintain'",
                "merge_commit_sha",
                'test "$MERGE_SHA" = "$TARGET_SHA"',
                "body.count(marker) != 1",
                "len(matches) != 1",
                "from urllib.request import Request, urlopen",
                publisher,
            ):
                self.assertIn(token, workflow)
            self.assertNotIn("gh api", workflow)
            self.assertNotIn("deploy", workflow.lower())

    def test_publication_render_supports_explicit_self_hosted_runner_labels(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            source = root / "policy.json"
            source.write_text(json.dumps({
                "schema_version": 1,
                "publication": {"publisher_skill": "coferlandia-release-publisher"}
            }), encoding="utf-8")
            publisher = ".agents/skills/coferlandia-release-publisher/scripts/coferlandia-release.py"
            labels = ["self-hosted", "Linux", "ARM64", "coferlandia-ci", "docker"]
            result = subprocess.run([
                sys.executable, str(CLI), "publication", "render",
                "--policy", str(source),
                "--target-root", str(root),
                "--publisher-path", publisher,
                "--runs-on", *labels,
                "--json"
            ], text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr or result.stdout)
            stored = json.loads((root / ".coferlandia" / "release" / "policy.json").read_text(encoding="utf-8"))
            self.assertEqual(stored["publication"]["github"]["runs_on"], labels)
            workflow = (root / ".github" / "workflows" / "coferlandia-release-publish.yml").read_text(encoding="utf-8")
            self.assertIn('runs-on: ["self-hosted","Linux","ARM64","coferlandia-ci","docker"]', workflow)

    def test_publication_validate_rejects_ambiguous_runner_labels(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            source = root / "policy.json"
            source.write_text(json.dumps({
                "schema_version": 1,
                "publication": {
                    "github": {
                        "mode": "issue-comment",
                        "workflow": ".github/workflows/coferlandia-release-publish.yml",
                        "runs_on": ["self-hosted", "self-hosted"]
                    }
                }
            }), encoding="utf-8")
            result = subprocess.run([
                sys.executable, str(CLI), "publication", "validate",
                "--policy", str(source), "--json"
            ], text=True, capture_output=True)
            self.assertEqual(result.returncode, 2)
            self.assertIn("duplicate", result.stdout)

    def test_publication_render_is_opt_in_and_rejects_unsafe_publisher_path(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            source = root / "policy.json"
            source.write_text(json.dumps({"schema_version": 1}), encoding="utf-8")
            dry = subprocess.run([
                sys.executable, str(CLI), "publication", "render",
                "--policy", str(source),
                "--target-root", str(root),
                "--publisher-path", "../publisher.py",
                "--dry-run", "--json"
            ], text=True, capture_output=True)
            self.assertEqual(dry.returncode, 2)
            self.assertFalse((root / ".coferlandia" / "release" / "policy.json").exists())
            self.assertFalse((root / ".github" / "workflows" / "coferlandia-release-publish.yml").exists())


if __name__ == "__main__":
    unittest.main()
