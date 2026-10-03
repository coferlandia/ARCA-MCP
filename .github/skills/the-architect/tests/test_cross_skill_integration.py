"""Cross-skill regression coverage for Architecture Gate ownership and handoffs."""
from __future__ import annotations

import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]


class CrossSkillIntegrationTests(unittest.TestCase):
    def text(self, relative: str) -> str:
        return (ROOT / relative).read_text(encoding="utf-8")

    def test_owning_skills_load_their_architecture_references(self) -> None:
        expectations = {
            "skills/ops/coferlandia-project-manager/SKILL.md": (
                "### Architecture Gate",
                "references/architecture-gate.md",
            ),
            "skills/engineering/software-development/SKILL.md": (
                "## Architecture Gate",
                "references/architecture-gate.md",
            ),
            "skills/ops/project-orchestrator/SKILL.md": (
                "## Architecture Gate",
                "references/architecture-gate.md",
            ),
            "skills/content/project-documentation-archivist/SKILL.md": (
                "## The Architect boundary",
                "references/the-architect-boundary.md",
            ),
        }
        for path, phrases in expectations.items():
            text = self.text(path)
            for phrase in phrases:
                self.assertIn(phrase, text, path)

    def test_development_pressure_cases_cover_gate_matrix(self) -> None:
        cases = json.loads(
            self.text("skills/engineering/software-development/tests/cases.json")
        )
        ids = {item["id"] for item in cases["evaluations"]}
        self.assertTrue(
            {
                "architecture-gate-required-blocks-analyst",
                "architecture-gate-passed-allows-analyst",
                "architecture-gate-required-blocks-direct-coding",
                "architecture-gate-absent-or-not-required-compatible",
                "retouch-does-not-require-architecture-gate",
            }.issubset(ids)
        )

    def test_release_notes_and_versions_are_reconciled(self) -> None:
        notes = self.text("RELEASE-NOTES.md")
        self.assertIn("## v2.2.0 (2026-08-01)", notes)
        self.assertIn(
            "| the-architect | new | 1.0.0 | Adds cross-project architecture memory",
            notes,
        )
        self.assertIn(
            "| coferlandia-project-manager | 0.6.0 | 0.8.0 |", notes
        )
        self.assertIn("| software-development | 4.4 | 4.6 |", notes)
        self.assertIn("| project-orchestrator | 1.1 | 2.3 |", notes)
        self.assertIn(
            "| project-documentation-archivist | 3.0.0 | 3.1.0 |", notes
        )
        versions = {
            "skills/ops/coferlandia-project-manager/SKILL.md": 'version: "0.9.0"',
            "skills/engineering/software-development/SKILL.md": 'version: "4.7"',
            "skills/ops/project-orchestrator/SKILL.md": 'version: "2.4"',
            "skills/content/project-documentation-archivist/SKILL.md": 'version: "3.1.0"',
        }
        for path, version in versions.items():
            self.assertIn(version, self.text(path), path)


if __name__ == "__main__":
    unittest.main()
