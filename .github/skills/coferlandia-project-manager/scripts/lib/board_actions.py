#!/usr/bin/env python3
"""Compatibility surface for former PM board-driven actions.

Phase 1 removes the PM-owned TODO/status state machine. Operational status now belongs to
GitHub Issues/Projects. These commands remain so existing wrappers fail safely and provide
useful GitHub-native guidance instead of silently reading TODO.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from reporting import cmd_task_report

GITHUB_NATIVE_STATUSES = {"backlog", "in-progress", "review", "blocked", "done"}


def _issue(args: argparse.Namespace) -> dict:
    report_args = argparse.Namespace(
        projects_file=args.projects_file,
        task=args.task,
        default_branch="main",
        stale_days=30,
        apply=False,
        format="json",
    )
    return cmd_task_report(report_args)


def cmd_validate_task_transition(args: argparse.Namespace) -> dict:
    issue = _issue(args)
    if issue.get("status") == "error":
        return {"status": "error", "authorized": False, "blocking_reason": issue.get("error")}
    target = args.target_status.strip().lower()
    if target not in GITHUB_NATIVE_STATUSES:
        return {
            "status": "error",
            "authorized": False,
            "blocking_reason": (
                f"'{args.target_status}' belongs to the retired PM-local state machine. "
                "Use GitHub Project status and the normalized categories backlog, in-progress, review, blocked, or done."
            ),
        }
    return {
        "status": "ok",
        "project_slug": issue["project"],
        "repository": issue["repository"],
        "issue_number": issue["issue"]["number"],
        "target_status": target,
        "authorized": False,
        "blocking_reason": "PM no longer mutates operational task state. Change the GitHub Project/Issue state through GitHub-native tooling.",
        "required_approval": "yes for write-capable GitHub mutation",
        "suggested_next_action": "update the GitHub Project item or Issue through the approved GitHub workflow",
        "source_of_truth": "github",
    }


def cmd_generate_execution_brief(args: argparse.Namespace) -> dict:
    issue = _issue(args)
    if issue.get("status") == "error":
        return issue
    value = issue["issue"]
    return {
        "status": "ok",
        "deprecated_command": True,
        "project_slug": issue["project"],
        "repository": issue["repository"],
        "issue_number": value["number"],
        "title": value["title"],
        "issue_url": value["url"],
        "required_next_skill": None,
        "suggested_next_action": "Use the GitHub Issue as the operational work item. Project architecture/planning remains a PM responsibility; implementation routing belongs to the selected delivery workflow.",
        "required_approval": "follow the selected delivery workflow",
        "executes_work": False,
        "mode": "compatibility-only",
        "source_of_truth": "github",
    }


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="GitHub-native compatibility tooling for former PM board actions.")
    sub = parser.add_subparsers(dest="command", required=True)
    validate = sub.add_parser("validate-task-transition")
    validate.add_argument("--projects-file", required=True, type=Path)
    validate.add_argument("--task", required=True, help="Issue reference, preferably project-slug#number")
    validate.add_argument("--target-status", required=True)
    brief = sub.add_parser("generate-execution-brief")
    brief.add_argument("--projects-file", required=True, type=Path)
    brief.add_argument("--task", required=True, help="Issue reference, preferably project-slug#number")
    brief.add_argument("--dry-run", action="store_true")
    brief.add_argument("--current-status-override")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    payload = cmd_validate_task_transition(args) if args.command == "validate-task-transition" else cmd_generate_execution_brief(args)
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0 if payload.get("status") != "error" else 1


if __name__ == "__main__":
    sys.exit(main())
