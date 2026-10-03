from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any, Callable
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen

from .model import ReleaseError

Opener = Callable[..., Any]


class GitHubService:
    def __init__(
        self,
        opener: Opener | None = None,
        token: str | None = None,
        api_base: str = "https://api.github.com",
        uploads_base: str = "https://uploads.github.com",
    ) -> None:
        self.opener = opener or urlopen
        self.token = token or os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
        self.api_base = api_base.rstrip("/")
        self.uploads_base = uploads_base.rstrip("/")

    @staticmethod
    def _split(repository: str) -> tuple[str, str]:
        parts = repository.split("/", 1)
        if len(parts) != 2 or not all(parts):
            raise ReleaseError(f"invalid GitHub repository: {repository}")
        return parts[0], parts[1]

    def _headers(self, *, accept: str = "application/vnd.github+json", content_type: str | None = None) -> dict[str, str]:
        headers = {
            "Accept": accept,
            "User-Agent": "coferlandia-release-publisher",
            "X-GitHub-Api-Version": "2022-11-28",
        }
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        if content_type:
            headers["Content-Type"] = content_type
        return headers

    def _request_bytes(
        self,
        method: str,
        path: str,
        *,
        payload: dict[str, Any] | None = None,
        raw: bytes | None = None,
        accept: str = "application/vnd.github+json",
        content_type: str | None = None,
        allow_not_found: bool = False,
        uploads: bool = False,
    ) -> bytes | None:
        if payload is not None and raw is not None:
            raise ValueError("payload and raw are mutually exclusive")
        base = self.uploads_base if uploads else self.api_base
        url = f"{base}/{path.lstrip('/')}"
        data = raw
        resolved_content_type = content_type
        if payload is not None:
            data = json.dumps(payload, separators=(",", ":")).encode("utf-8")
            resolved_content_type = "application/json"
        request = Request(
            url,
            data=data,
            method=method,
            headers=self._headers(accept=accept, content_type=resolved_content_type),
        )
        try:
            response = self.opener(request, timeout=30)
            try:
                return response.read()
            finally:
                close = getattr(response, "close", None)
                if callable(close):
                    close()
        except HTTPError as exc:
            if allow_not_found and exc.code == 404:
                return None
            body = exc.read().decode("utf-8", errors="replace").strip()
            raise ReleaseError(body or f"GitHub API {method} {path} failed with HTTP {exc.code}") from exc
        except URLError as exc:
            raise ReleaseError(f"GitHub API {method} {path} failed: {exc.reason}") from exc

    def _request_json(
        self,
        method: str,
        path: str,
        *,
        payload: dict[str, Any] | None = None,
        allow_not_found: bool = False,
        uploads: bool = False,
    ) -> Any:
        raw = self._request_bytes(
            method,
            path,
            payload=payload,
            allow_not_found=allow_not_found,
            uploads=uploads,
        )
        if raw is None:
            return None
        try:
            return json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise RuntimeError(f"GitHub API returned invalid JSON for {method} {path}") from exc

    def repository_info(self, repository: str) -> dict[str, Any]:
        owner, repo = self._split(repository)
        data = self._request_json("GET", f"repos/{owner}/{repo}")
        default = data.get("default_branch") or (data.get("defaultBranchRef") or {}).get("name")
        full = data.get("full_name") or data.get("nameWithOwner") or repository
        return {"repository": full, "default_branch": default}

    @staticmethod
    def _normalize_asset(asset: dict[str, Any]) -> dict[str, Any]:
        digest = asset.get("digest")
        if isinstance(digest, str) and digest.startswith("sha256:"):
            digest = digest.removeprefix("sha256:")
        return {
            "id": asset.get("id"),
            "name": asset.get("name"),
            "size": asset.get("size"),
            "sha256": digest,
            "url": asset.get("browser_download_url"),
        }

    @classmethod
    def _normalize_release(cls, data: dict[str, Any]) -> dict[str, Any]:
        return {
            "id": data.get("id"),
            "tag": data.get("tag_name") or data.get("tag"),
            "title": data.get("name") or data.get("title") or "",
            "body": data.get("body") or "",
            "draft": bool(data.get("draft")),
            "prerelease": bool(data.get("prerelease")),
            "immutable": data.get("immutable"),
            "created_at": data.get("created_at"),
            "published_at": data.get("published_at"),
            "html_url": data.get("html_url"),
            "assets": [cls._normalize_asset(item) for item in data.get("assets", [])],
        }

    def list_releases(self, repository: str) -> list[dict[str, Any]]:
        owner, repo = self._split(repository)
        releases: list[dict[str, Any]] = []
        page = 1
        while True:
            data = self._request_json("GET", f"repos/{owner}/{repo}/releases?{urlencode({'per_page': 100, 'page': page})}")
            if not isinstance(data, list):
                raise ReleaseError("GitHub releases response must be a list")
            releases.extend(self._normalize_release(item) for item in data)
            if len(data) < 100:
                break
            page += 1
        return releases

    def release_by_tag(self, repository: str, tag: str) -> dict[str, Any] | None:
        owner, repo = self._split(repository)
        data = self._request_json(
            "GET",
            f"repos/{owner}/{repo}/releases/tags/{quote(tag, safe='')}",
            allow_not_found=True,
        )
        primary = self._normalize_release(data) if data else None
        # The direct endpoint omits drafts. Always inspect the collection as well so
        # recovery fails closed if another release object already uses the same tag.
        matches = [release for release in self.list_releases(repository) if release.get("tag") == tag]
        if primary is not None:
            conflicts = [release for release in matches if release.get("id") != primary.get("id")]
            if conflicts:
                raise ReleaseError(f"multiple GitHub Releases use tag {tag}")
            return primary
        if len(matches) > 1:
            raise ReleaseError(f"multiple GitHub Releases use tag {tag}")
        return matches[0] if matches else None

    def release_by_id(self, repository: str, release_id: int) -> dict[str, Any]:
        owner, repo = self._split(repository)
        return self._normalize_release(self._request_json("GET", f"repos/{owner}/{repo}/releases/{release_id}"))

    def checks_for_commit(self, repository: str, sha: str) -> list[dict[str, Any]]:
        owner, repo = self._split(repository)
        data = self._request_json("GET", f"repos/{owner}/{repo}/commits/{sha}/check-runs?per_page=100")
        return [
            {
                "name": item.get("name"),
                "status": item.get("status"),
                "conclusion": item.get("conclusion"),
                "url": item.get("html_url") or item.get("details_url"),
            }
            for item in data.get("check_runs", [])
        ]

    def immutable_releases_status(self, repository: str) -> dict[str, Any]:
        owner, repo = self._split(repository)
        data = self._request_json("GET", f"repos/{owner}/{repo}/immutable-releases", allow_not_found=True)
        if data is None:
            return {"enabled": False, "enforced_by_owner": False}
        return {"enabled": bool(data.get("enabled")), "enforced_by_owner": bool(data.get("enforced_by_owner"))}

    def create_draft_release(self, repository: str, tag: str, title: str, body: str, prerelease: bool) -> dict[str, Any]:
        owner, repo = self._split(repository)
        data = self._request_json(
            "POST",
            f"repos/{owner}/{repo}/releases",
            payload={
                "tag_name": tag,
                "name": title,
                "body": body,
                "draft": True,
                "prerelease": bool(prerelease),
            },
        )
        return self._normalize_release(data)

    def upload_asset(self, repository: str, release_id: int, path: Path, name: str | None = None) -> dict[str, Any]:
        owner, repo = self._split(repository)
        asset_name = quote(name or path.name, safe="")
        raw = self._request_bytes(
            "POST",
            f"repos/{owner}/{repo}/releases/{release_id}/assets?name={asset_name}",
            raw=path.read_bytes(),
            content_type="application/octet-stream",
            uploads=True,
        )
        if raw is None:
            raise ReleaseError("GitHub asset upload returned no response")
        try:
            data = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise RuntimeError("GitHub asset upload returned invalid JSON") from exc
        return self._normalize_asset(data)

    def publish_release(self, repository: str, release_id: int) -> dict[str, Any]:
        owner, repo = self._split(repository)
        data = self._request_json(
            "PATCH",
            f"repos/{owner}/{repo}/releases/{release_id}",
            payload={"draft": False},
        )
        return self._normalize_release(data)

    def download_asset_bytes(self, repository: str, asset_id: int) -> bytes:
        owner, repo = self._split(repository)
        raw = self._request_bytes(
            "GET",
            f"repos/{owner}/{repo}/releases/assets/{asset_id}",
            accept="application/octet-stream",
        )
        if raw is None:
            raise ReleaseError("GitHub asset download returned no response")
        return raw

    def download_text_asset(self, repository: str, asset_id: int) -> str:
        return self.download_asset_bytes(repository, asset_id).decode("utf-8")
