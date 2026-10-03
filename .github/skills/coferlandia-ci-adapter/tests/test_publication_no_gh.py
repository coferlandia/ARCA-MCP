from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
MODULE = SKILL_ROOT / "scripts" / "coferlandia_ci_adapter_cli" / "publication.py"


def load_module():
    spec = importlib.util.spec_from_file_location("publication_module", MODULE)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class PublicationWorkflowTests(unittest.TestCase):
    def test_generated_workflow_uses_python_rest_not_gh_cli(self) -> None:
        module = load_module()
        workflow = module.render_workflow(
            publisher_path=".agents/skills/coferlandia-release-publisher/scripts/coferlandia-release.py",
            runs_on=["self-hosted", "Linux", "ARM64", "coferlandia-ci", "docker"],
        )
        self.assertNotIn("gh api", workflow)
        self.assertNotIn("GH_TOKEN", workflow)
        self.assertIn("GITHUB_TOKEN", workflow)
        self.assertIn("from urllib.request import Request, urlopen", workflow)
        self.assertIn("merge_sha=${{ steps.authority.outputs.merge_sha }}", workflow.replace('MERGE_SHA: ${{ steps.authority.outputs.merge_sha }}', 'merge_sha=${{ steps.authority.outputs.merge_sha }}'))
        self.assertIn("actions/setup-python@v5", workflow)
        self.assertLess(workflow.index("actions/setup-python@v5"), workflow.index("Require release authority"))
        self.assertIn("Check out exact publication control plane", workflow)
        self.assertIn("ref: ${{ github.sha }}", workflow)
        self.assertIn("path: control-plane", workflow)
        self.assertIn("path: release-target", workflow)
        self.assertIn("working-directory: release-target", workflow)
        self.assertIn("$GITHUB_WORKSPACE/control-plane/.agents/skills/coferlandia-release-publisher/scripts/coferlandia-release.py", workflow)
        self.assertIn('runs-on: ["self-hosted","Linux","ARM64","coferlandia-ci","docker"]', workflow)
        self.assertNotIn("deploy", workflow.lower())
        self.assertIn("Retry the existing Coferlandia publication request.", workflow)
        self.assertIn("issues/{issue_number}/comments?per_page=100&page={page}", workflow)
        self.assertIn("comment_id >= current_id", workflow)
        self.assertIn("permission_for(author) not in {'admin', 'maintain'}", workflow)
        self.assertIn("retry requested but no prior authorized valid publication request exists", workflow)
        self.assertIn("request_comment_id={request_comment_id}", workflow)
        self.assertIn("coferlandia-publication-request.json", workflow)
        self.assertNotIn("types: [created, edited]", workflow)


if __name__ == "__main__":
    unittest.main()
