from __future__ import annotations

import importlib.util
import json
import os
import tempfile
import textwrap
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


class FakeResponse:
    def __init__(self, value): self.value = value
    def __enter__(self): return self
    def __exit__(self, *args): return False
    def read(self): return json.dumps(self.value).encode("utf-8")


class RetryRuntimeTests(unittest.TestCase):
    def _script(self):
        workflow = load_module().render_workflow(
            publisher_path=".agents/skills/coferlandia-release-publisher/scripts/coferlandia-release.py"
        )
        section = workflow.split("      - name: Parse immutable publication request", 1)[1]
        script = section.split("          python - <<'PY'\n", 1)[1].split("\n          PY", 1)[0]
        script = textwrap.dedent(script)
        return script.replace(
            "from urllib.request import Request, urlopen",
            "from urllib.request import Request\nurlopen = fake_urlopen",
        )

    def _request(self, title="Original"):
        payload = {
            "schema": 1,
            "target_sha": "a" * 40,
            "version": "1.0.0",
            "impact": "major",
            "title": title,
            "notes": "notes",
        }
        return "<!-- coferlandia-release-publication-request:v1 -->\n```json\n" + json.dumps(payload) + "\n```"

    def test_retry_skips_newer_malformed_and_unauthorized_requests(self):
        comments = [
            {"id": 100, "body": self._request("Authorized original"), "user": {"login": "good"}},
            {"id": 200, "body": self._request("Unauthorized override"), "user": {"login": "evil"}},
            {"id": 250, "body": "<!-- coferlandia-release-publication-request:v1 -->\n```json\n{bad}\n```", "user": {"login": "good"}},
        ]

        def fake_urlopen(request, timeout=30):
            url = request.full_url
            if "/issues/77/comments" in url:
                return FakeResponse(comments)
            if "/collaborators/good/permission" in url:
                return FakeResponse({"permission": "maintain"})
            if "/collaborators/evil/permission" in url:
                return FakeResponse({"permission": "read"})
            raise AssertionError(url)

        with tempfile.TemporaryDirectory() as temp_dir:
            temp = Path(temp_dir)
            event = {
                "issue": {"number": 77},
                "comment": {"id": 300, "body": "Retry the existing Coferlandia publication request."},
            }
            event_path = temp / "event.json"
            output_path = temp / "out.txt"
            event_path.write_text(json.dumps(event), encoding="utf-8")
            old = os.environ.copy()
            try:
                os.environ.update({
                    "GITHUB_EVENT_PATH": str(event_path),
                    "GITHUB_REPOSITORY": "coferlandia/demo",
                    "GITHUB_TOKEN": "token",
                    "GITHUB_OUTPUT": str(output_path),
                    "RUNNER_TEMP": str(temp),
                })
                exec(self._script(), {"fake_urlopen": fake_urlopen})
            finally:
                os.environ.clear(); os.environ.update(old)
            resolved = json.loads((temp / "coferlandia-publication-request.json").read_text(encoding="utf-8"))
            self.assertEqual(resolved["title"], "Authorized original")
            self.assertIn("request_comment_id=100", output_path.read_text(encoding="utf-8"))

    def test_retry_fails_when_no_prior_authorized_valid_request_exists(self):
        comments = [{"id": 100, "body": self._request("No authority"), "user": {"login": "evil"}}]

        def fake_urlopen(request, timeout=30):
            if "/issues/77/comments" in request.full_url:
                return FakeResponse(comments)
            if "/collaborators/evil/permission" in request.full_url:
                return FakeResponse({"permission": "read"})
            raise AssertionError(request.full_url)

        with tempfile.TemporaryDirectory() as temp_dir:
            temp = Path(temp_dir)
            event_path = temp / "event.json"
            event_path.write_text(json.dumps({
                "issue": {"number": 77},
                "comment": {"id": 300, "body": "Retry the existing Coferlandia publication request."},
            }), encoding="utf-8")
            old = os.environ.copy()
            try:
                os.environ.update({
                    "GITHUB_EVENT_PATH": str(event_path),
                    "GITHUB_REPOSITORY": "coferlandia/demo",
                    "GITHUB_TOKEN": "token",
                    "GITHUB_OUTPUT": str(temp / "out.txt"),
                    "RUNNER_TEMP": str(temp),
                })
                with self.assertRaisesRegex(SystemExit, "no prior authorized valid"):
                    exec(self._script(), {"fake_urlopen": fake_urlopen})
            finally:
                os.environ.clear(); os.environ.update(old)


if __name__ == "__main__":
    unittest.main()
