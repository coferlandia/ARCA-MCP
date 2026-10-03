from __future__ import annotations
import re
import subprocess
from pathlib import Path
from typing import Any
from .model import ReleaseError

class GitService:
    def __init__(self, root: Path, remote: str = "origin") -> None:
        self.root = root.resolve()
        self.remote = remote

    def _run(self, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
        result = subprocess.run(["git", "-C", str(self.root), *args], text=True, capture_output=True, check=False)
        if check and result.returncode != 0:
            raise ReleaseError(result.stderr.strip() or f"git {' '.join(args)} failed")
        return result

    def resolve_commit(self, revision: str) -> str:
        result = self._run("rev-parse", "--verify", f"{revision}^{{commit}}", check=False)
        if result.returncode != 0:
            raise ReleaseError(f"revision does not resolve to a commit: {revision}")
        return result.stdout.strip()

    def is_ancestor(self, ancestor: str, descendant: str) -> bool:
        return self._run("merge-base", "--is-ancestor", ancestor, descendant, check=False).returncode == 0

    def is_reachable_from(self, commit: str, ref: str) -> bool:
        resolved = self._run("rev-parse", "--verify", ref, check=False)
        return resolved.returncode == 0 and self.is_ancestor(commit, resolved.stdout.strip())

    def distance(self, ancestor: str, descendant: str) -> int:
        result = self._run("rev-list", "--count", f"{ancestor}..{descendant}")
        return int(result.stdout.strip())

    def tag_info(self, tag: str) -> dict[str, Any] | None:
        typ = self._run("cat-file", "-t", f"refs/tags/{tag}", check=False)
        if typ.returncode != 0:
            return None
        kind = "annotated" if typ.stdout.strip() == "tag" else "lightweight"
        return {"tag": tag, "kind": kind, "commit": self.resolve_commit(f"refs/tags/{tag}")}

    def remote_tag_info(self, tag: str) -> dict[str, Any] | None:
        result = self._run("ls-remote", "--tags", self.remote, f"refs/tags/{tag}", f"refs/tags/{tag}^{{}}")
        rows = [line.split() for line in result.stdout.splitlines() if line.strip()]
        if not rows:
            return None
        peeled = next((sha for sha, ref in rows if ref.endswith("^{}")), None)
        direct = next((sha for sha, ref in rows if not ref.endswith("^{}")), None)
        if peeled:
            return {"tag": tag, "kind": "annotated", "commit": peeled, "object": direct}
        return {"tag": tag, "kind": "lightweight", "commit": direct}

    def list_tags(self) -> list[str]:
        result = self._run("for-each-ref", "--format=%(refname:strip=2)", "refs/tags")
        return [line for line in result.stdout.splitlines() if line.strip()]

    def tags_for_commit(self, commit: str) -> list[str]:
        result = self._run("tag", "--points-at", commit)
        return sorted(line for line in result.stdout.splitlines() if line.strip())

    def create_annotated_tag(self, tag: str, commit: str, message: str, sign: bool = False) -> None:
        existing = self.tag_info(tag)
        if existing:
            if existing["kind"] == "annotated" and existing["commit"] == commit:
                return
            raise ReleaseError(f"tag {tag} already exists with incompatible identity")
        args = ["tag", "-a"]
        if sign:
            args.append("-s")
        args.extend([tag, commit, "-m", message])
        self._run(*args)

    def push_tag(self, tag: str) -> None:
        self._run("push", self.remote, f"refs/tags/{tag}:refs/tags/{tag}")

    def refresh_tags(self) -> None:
        self._run("fetch", self.remote, "--tags", "--prune")

    def commits_between(self, previous: str | None, target: str) -> list[dict[str, str]]:
        spec = f"{previous}..{target}" if previous else target
        result = self._run("log", "--reverse", "--format=%H%x09%s", spec)
        rows = []
        for line in result.stdout.splitlines():
            sha, _, subject = line.partition("\t")
            if sha:
                rows.append({"sha": sha, "subject": subject})
        return rows

    def changed_paths(self, previous: str | None, target: str) -> list[dict[str, str]]:
        result = self._run("diff", "--name-status", previous, target) if previous else self._run("diff-tree", "--root", "--no-commit-id", "--name-status", "-r", target)
        rows = []
        for line in result.stdout.splitlines():
            parts = line.split("\t")
            if len(parts) >= 2:
                rows.append({"status": parts[0], "path": parts[-1]})
        return rows

    def remote_url(self) -> str:
        return self._run("remote", "get-url", self.remote).stdout.strip()

    def github_repository(self) -> str | None:
        url = self.remote_url()
        patterns = (
            r"^https?://github\.com/([^/]+/[^/]+?)(?:\.git)?$",
            r"^git@github\.com:([^/]+/[^/]+?)(?:\.git)?$",
            r"^ssh://git@github\.com/([^/]+/[^/]+?)(?:\.git)?$",
        )
        for pattern in patterns:
            match = re.match(pattern, url)
            if match:
                return match.group(1)
        return None
