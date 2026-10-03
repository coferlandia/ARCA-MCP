from __future__ import annotations
import argparse
import json
import shutil
import sys
from pathlib import Path
from typing import Any

from . import CLI_VERSION
from .git_service import GitService
from .github_service import GitHubService
from .model import ReleaseError, load_plan, write_json
from .operations import build_plan, inspect_release, publish_release, resolve_release, verify_release
from .policy import load_policy

def emit(value: Any, code: int = 0) -> int:
    print(json.dumps(value, indent=2, ensure_ascii=False, sort_keys=True))
    return code

def _root(value: Path | None) -> Path:
    return (value or Path.cwd()).resolve()

def _repository(arguments: argparse.Namespace, git: GitService) -> str:
    if arguments.repository:
        return arguments.repository
    detected = git.github_repository()
    if not detected:
        raise ReleaseError("could not infer GitHub repository from origin; pass --repository owner/name")
    return detected

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Deterministic Commit -> Release operations for Coferlandia repositories.")
    parser.add_argument("--repo", type=Path, help="target Git repository root; defaults to current directory")
    parser.add_argument("--repository", help="GitHub repository in owner/name form; otherwise inferred from origin")
    parser.add_argument("--policy", type=Path, help="release policy JSON; otherwise .coferlandia/release/policy.json when present")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("version")
    commands.add_parser("capabilities")
    commands.add_parser("doctor")
    inspect = commands.add_parser("inspect")
    inspect.add_argument("--target", default="HEAD")
    inspect.add_argument("--previous-tag")
    plan = commands.add_parser("plan")
    plan.add_argument("--target", default="HEAD")
    plan.add_argument("--previous-tag")
    plan.add_argument("--impact", choices=("patch", "minor", "major"), required=True)
    plan.add_argument("--version", required=True)
    plan.add_argument("--title", required=True)
    notes = plan.add_mutually_exclusive_group(required=True)
    notes.add_argument("--notes")
    notes.add_argument("--notes-file", type=Path)
    plan.add_argument("--prerelease", action="store_true")
    plan.add_argument("--artifact", action="append", default=[], type=Path)
    plan.add_argument("--provenance", choices=("optional", "required", "disabled"))
    plan.add_argument("--output", type=Path)
    publish = commands.add_parser("publish")
    publish.add_argument("--input", required=True, type=Path)
    verify = commands.add_parser("verify")
    verify.add_argument("--tag", required=True)
    resolve = commands.add_parser("resolve")
    resolve.add_argument("--tag", required=True)
    return parser

def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        if args.command == "version":
            return emit({"command": "version", "version": CLI_VERSION})
        if args.command == "capabilities":
            return emit({"command": "capabilities", "capabilities": ["doctor", "inspect", "plan", "publish", "verify", "resolve"]})
        root = _root(args.repo)
        git = GitService(root)
        repository = _repository(args, git)
        policy = load_policy(root, args.policy)
        github = GitHubService()
        if args.command == "doctor":
            info = github.repository_info(repository)
            return emit({"command": "doctor", "ok": bool(shutil.which("git")) and bool(shutil.which("gh")), "repo_root": str(root), "repository": repository, "default_branch": info.get("default_branch"), "git_available": bool(shutil.which("git")), "gh_available": bool(shutil.which("gh")), "policy": policy})
        if args.command == "inspect":
            result = inspect_release(root, repository, args.target, policy, previous_tag=args.previous_tag, git=git, github=github)
            return emit({"command": "inspect", **result}, 0 if result["eligible"] else 1)
        if args.command == "plan":
            inspection = inspect_release(root, repository, args.target, policy, previous_tag=args.previous_tag, git=git, github=github)
            notes_text = args.notes if args.notes is not None else args.notes_file.read_text(encoding="utf-8")
            plan_value = build_plan(root, repository, inspection, policy, impact=args.impact, version=args.version, title=args.title, release_notes=notes_text, prerelease=args.prerelease, artifact_paths=args.artifact, provenance=args.provenance, git=git, github=github)
            payload = plan_value.to_dict()
            if args.output:
                write_json(args.output, payload)
            return emit({"command": "plan", "dry_run": True, "plan": payload, "output": str(args.output) if args.output else None})
        if args.command == "publish":
            result = publish_release(root, load_plan(args.input), git=git, github=github)
            return emit({"command": "publish", **result})
        if args.command == "verify":
            result = verify_release(root, repository, args.tag, policy, git=git, github=github)
            return emit({"command": "verify", **result}, 0 if result["consistency"] == "pass" else 1)
        if args.command == "resolve":
            return emit({"command": "resolve", "release": resolve_release(root, repository, args.tag, policy, git=git, github=github)})
    except (ReleaseError, ValueError, OSError, json.JSONDecodeError) as exc:
        print(json.dumps({"command": getattr(args, "command", None), "ok": False, "error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1
    parser.error("unsupported command")
    return 2
