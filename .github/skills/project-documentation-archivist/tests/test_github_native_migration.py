from __future__ import annotations

import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "github_migration.py"
VALIDATOR = Path(__file__).resolve().parents[1] / "scripts" / "validate_catalog.py"

spec = importlib.util.spec_from_file_location("github_migration", SCRIPT)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
import sys
sys.modules[spec.name] = module
spec.loader.exec_module(module)


class GitHubNativeMigrationTests(unittest.TestCase):
    def make_repo(self) -> tempfile.TemporaryDirectory:
        return tempfile.TemporaryDirectory()

    def init_repo(self, root: Path) -> None:
        subprocess.run(["git", "init", "-q", str(root)], check=True)
        subprocess.run(["git", "-C", str(root), "checkout", "-q", "-b", "main"], check=True)
        subprocess.run(["git", "-C", str(root), "config", "user.email", "test@example.com"], check=True)
        subprocess.run(["git", "-C", str(root), "config", "user.name", "Test"], check=True)
        (root / ".gitkeep").write_text("", encoding="utf-8")
        subprocess.run(["git", "-C", str(root), "add", ".gitkeep"], check=True)
        subprocess.run(["git", "-C", str(root), "commit", "-q", "-m", "init"], check=True)

    def test_inventory_ids_are_stable_and_decisions_default_to_review(self) -> None:
        with self.make_repo() as td:
            root = Path(td)
            self.init_repo(root)
            (root / "TODO.md").write_text("# TODO\n\n- [ ] TASK-1 Add health check\n", encoding="utf-8")
            (root / "HISTORY.md").write_text("# History\n\n## 2026-07-01\n- Added API endpoint\n", encoding="utf-8")
            first = module.inventory_payload(root)
            second = module.inventory_payload(root)
            self.assertEqual([x["legacy_id"] for x in first["items"]], [x["legacy_id"] for x in second["items"]])
            decisions = module.default_decisions(first)
            self.assertTrue(decisions["items"])
            self.assertTrue(all(x["disposition"] == "NEEDS_REVIEW" for x in decisions["items"]))

    def test_duplicate_legacy_text_gets_distinct_stable_ids(self) -> None:
        with self.make_repo() as td:
            root = Path(td)
            self.init_repo(root)
            (root / "TODO.md").write_text("# TODO\n\n## A\n- [ ] Same work\n\n## B\n- [ ] Same work\n", encoding="utf-8")
            first = module.inventory_payload(root)
            second = module.inventory_payload(root)
            ids1 = [item["legacy_id"] for item in first["items"]]
            ids2 = [item["legacy_id"] for item in second["items"]]
            self.assertEqual(ids1, ids2)
            self.assertEqual(len(ids1), len(set(ids1)))

    def test_decision_validation_rejects_missing_mapping_and_create_title(self) -> None:
        inventory = {"items": [{"legacy_id": "legacy-todo-1"}, {"legacy_id": "legacy-history-2"}]}
        decisions = {
            "items": [
                {"legacy_id": "legacy-todo-1", "disposition": "EXISTING_ISSUE", "target": None},
                {"legacy_id": "legacy-history-2", "disposition": "CREATE_CLOSED_HISTORICAL_ISSUE", "issue": {"title": ""}},
            ]
        }
        errors = module.validate_decisions(inventory, decisions)
        self.assertTrue(any("requires target" in item for item in errors))
        self.assertTrue(any("requires issue.title" in item for item in errors))

    def test_knowledge_only_requires_feed_and_audit_note(self) -> None:
        inventory = {"items": [{"legacy_id": "legacy-todo-1"}]}
        decisions = {"items": [{"legacy_id": "legacy-todo-1", "disposition": "KNOWLEDGE_ONLY", "feeds": ["DECISIONS.md"], "notes": ""}]}
        errors = module.validate_decisions(inventory, decisions)
        self.assertTrue(any("requires notes" in item for item in errors))

    def test_marker_lookup_fails_closed_when_github_search_fails(self) -> None:
        original_run = module.run
        try:
            module.run = lambda *args, **kwargs: module.CommandResult(1, "", "network failure")
            with self.assertRaises(RuntimeError):
                module.find_marker_issue(Path("."), "acme/repo", "legacy-todo-1")
        finally:
            module.run = original_run

    def test_created_issue_body_never_contains_raw_legacy_source(self) -> None:
        calls = []
        original_run = module.run
        try:
            def fake_run(cmd, *, cwd, check=False):
                calls.append(cmd)
                if cmd[:3] == ["gh", "issue", "create"]:
                    return module.CommandResult(0, "https://github.com/acme/repo/issues/17", "")
                return module.CommandResult(0, "", "")
            module.run = fake_run
            legacy = {"legacy_source": "TODO.md", "raw": "password=do-not-publish", "original_date": None}
            decision = {"legacy_id": "legacy-todo-1", "disposition": "CREATE_OPEN_ISSUE", "issue": {"title": "Safe issue", "body": "Curated context", "labels": []}}
            module.create_issue(Path("."), "acme/repo", legacy, decision)
        finally:
            module.run = original_run
        create = next(cmd for cmd in calls if cmd[:3] == ["gh", "issue", "create"] )
        body = create[create.index("--body") + 1]
        self.assertIn("Curated context", body)
        self.assertIn("coferlandia-migration-id", body)
        self.assertNotIn("password=do-not-publish", body)

    def test_apply_journal_prevents_duplicate_after_partial_project_failure(self) -> None:
        with self.make_repo() as td:
            root = Path(td)
            self.init_repo(root)
            migration = root / ".agent/migrations"
            migration.mkdir(parents=True)
            inventory = {
                "schema_version": 1,
                "generated_at": "2026-07-27T00:00:00Z",
                "items": [{"legacy_id": "legacy-todo-1", "legacy_source": "TODO.md", "kind": "todo", "raw": "work"}],
            }
            decisions = {
                "schema_version": 1,
                "items": [{
                    "legacy_id": "legacy-todo-1",
                    "disposition": "CREATE_OPEN_ISSUE",
                    "target": None,
                    "issue": {"title": "Migrate me", "body": "Curated", "labels": []},
                    "feeds": [],
                    "notes": "",
                }],
            }
            module.write_json(migration / "github-native-inventory.json", inventory)
            module.write_json(migration / "github-native-decisions.json", decisions)
            args = type("Args", (), {
                "project_root": str(root),
                "decisions": ".agent/migrations/github-native-decisions.json",
                "inventory": None,
                "map": None,
                "project_owner": "acme",
                "project_number": 1,
                "apply": True,
            })()

            original_resolve_repo = module.resolve_repo
            original_create_issue = module.create_issue
            original_add = module.add_issue_to_project
            original_resolve_existing = module.resolve_existing_issue
            original_find_marker = module.find_marker_issue
            creates = []
            adds = []
            try:
                module.resolve_repo = lambda _root: {"nameWithOwner": "acme/repo"}
                module.find_marker_issue = lambda *_args, **_kwargs: None
                def fake_create(*_args, **_kwargs):
                    creates.append(1)
                    return {"number": 17, "url": "https://github.com/acme/repo/issues/17", "state": "OPEN"}
                module.create_issue = fake_create
                module.resolve_existing_issue = lambda *_args, **_kwargs: {"number": 17, "url": "https://github.com/acme/repo/issues/17", "state": "OPEN", "title": "Migrate me"}
                def fail_once(*_args, **_kwargs):
                    adds.append(1)
                    if len(adds) == 1:
                        raise RuntimeError("project mutation failed")
                module.add_issue_to_project = fail_once

                with self.assertRaises(RuntimeError):
                    module.cmd_apply(args)
                journal = module.load_json(migration / "github-native-map.json")
                self.assertFalse(journal["complete"])
                self.assertEqual(journal["results"][0]["github"]["number"], 17)

                self.assertEqual(module.cmd_apply(args), 0)
                final = module.load_json(migration / "github-native-map.json")
                self.assertTrue(final["complete"])
                self.assertEqual(len(creates), 1, "rerun must reuse the journaled Issue instead of creating a duplicate")
                self.assertEqual(len(adds), 2)
            finally:
                module.resolve_repo = original_resolve_repo
                module.create_issue = original_create_issue
                module.add_issue_to_project = original_add
                module.resolve_existing_issue = original_resolve_existing
                module.find_marker_issue = original_find_marker

    def test_write_apply_blocks_unresolved_decisions(self) -> None:
        with self.make_repo() as td:
            root = Path(td)
            self.init_repo(root)
            migration = root / ".agent/migrations"
            migration.mkdir(parents=True)
            inventory = {"schema_version": 1, "generated_at": "2026-07-27T00:00:00Z", "items": [{"legacy_id": "legacy-todo-1", "legacy_source": "TODO.md", "kind": "todo", "raw": "work"}]}
            decisions = {"schema_version": 1, "items": [{"legacy_id": "legacy-todo-1", "disposition": "NEEDS_REVIEW", "target": None, "issue": {"title": "", "body": "", "labels": []}, "feeds": [], "notes": ""}]}
            module.write_json(migration / "github-native-inventory.json", inventory)
            module.write_json(migration / "github-native-decisions.json", decisions)
            args = type("Args", (), {"project_root": str(root), "decisions": ".agent/migrations/github-native-decisions.json", "inventory": None, "map": None, "project_owner": None, "project_number": None, "apply": True})()
            original_resolve_repo = module.resolve_repo
            try:
                module.resolve_repo = lambda _root: (_ for _ in ()).throw(AssertionError("GitHub should not be touched"))
                self.assertEqual(module.cmd_apply(args), 1)
            finally:
                module.resolve_repo = original_resolve_repo

    def test_strict_validator_rejects_legacy_files_after_cutover(self) -> None:
        with self.make_repo() as td:
            root = Path(td)
            for name in ("README.md", "AGENTS.md", "DECISIONS.md", "RUNBOOK.md"):
                (root / name).write_text("# Test\n", encoding="utf-8")
            (root / "AGENTS.md").write_text(
                "# AGENTS\n\n## Critical Instructions for Agents\n\n## Project Essentials\n\n## Documentation Index\n\n## Maintenance Notes\n",
                encoding="utf-8",
            )
            catalog = root / ".agent/catalog"
            catalog.mkdir(parents=True)
            (catalog / "SOURCE_INDEX.md").write_text("# Source Index\n", encoding="utf-8")
            (catalog / "PROCESSING_RUNS.md").write_text("# Processing Runs\n", encoding="utf-8")
            (root / "TODO.md").write_text("# TODO\n", encoding="utf-8")
            result = subprocess.run(["python", str(VALIDATOR), "--project-root", str(root), "--require-github-native"], text=True, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Legacy operational artifact still exists", result.stdout)


if __name__ == "__main__":
    unittest.main()
