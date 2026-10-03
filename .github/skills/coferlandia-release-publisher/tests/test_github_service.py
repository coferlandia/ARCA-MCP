from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_publisher.github_service import GitHubService


class FakeResponse:
    def __init__(self, payload: object) -> None:
        self.payload = payload

    def read(self) -> bytes:
        if isinstance(self.payload, bytes):
            return self.payload
        if isinstance(self.payload, str):
            return self.payload.encode("utf-8")
        return json.dumps(self.payload).encode("utf-8")

    def close(self) -> None:
        pass


class FakeOpener:
    def __init__(self, payloads: list[object]) -> None:
        self.payloads = list(payloads)
        self.requests = []

    def __call__(self, request, timeout=30):
        self.requests.append(request)
        return FakeResponse(self.payloads.pop(0))


class GitHubServiceTests(unittest.TestCase):
    def test_reads_repository_and_release_as_json(self) -> None:
        release_payload = {"id": 7, "tag_name": "v1.2.0", "name": "Release", "draft": False, "prerelease": False, "assets": []}
        opener = FakeOpener([
            {"full_name": "coferlandia/demo", "default_branch": "main"},
            release_payload,
            [release_payload],
        ])
        service = GitHubService(opener=opener, token="")
        info = service.repository_info("coferlandia/demo")
        release = service.release_by_tag("coferlandia/demo", "v1.2.0")
        self.assertEqual(info["default_branch"], "main")
        self.assertEqual(release["tag"], "v1.2.0")
        self.assertFalse(release["draft"])
        self.assertTrue(all(request.full_url.startswith("https://api.github.com/") for request in opener.requests))

    def test_invalid_json_fails_explicitly(self) -> None:
        service = GitHubService(opener=FakeOpener(["not-json"]), token="")
        with self.assertRaises(RuntimeError):
            service.repository_info("coferlandia/demo")

    def test_create_release_is_draft_and_does_not_create_tag(self) -> None:
        opener = FakeOpener([{"id": 42, "tag_name": "v1.2.0", "draft": True, "assets": []}])
        service = GitHubService(opener=opener, token="")
        result = service.create_draft_release("coferlandia/demo", "v1.2.0", "Demo", "Notes", False)
        payload = json.loads(opener.requests[0].data.decode("utf-8"))
        self.assertTrue(result["draft"])
        self.assertEqual(opener.requests[0].method, "POST")
        self.assertTrue(payload["draft"])
        self.assertNotIn("target_commitish", payload)

    def test_service_exposes_no_destructive_release_delete(self) -> None:
        self.assertFalse(hasattr(GitHubService, "delete_release"))
        self.assertFalse(hasattr(GitHubService, "move_tag"))


if __name__ == "__main__":
    unittest.main()
