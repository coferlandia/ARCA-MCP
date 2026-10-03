"""Contract tests for product-knowledge-sync."""
from __future__ import annotations

import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
SKILL_DIR = ROOT / "skills/content/product-knowledge-sync"
SKILL = SKILL_DIR / "SKILL.md"


class ProductKnowledgeContractTests(unittest.TestCase):
    def skill_text(self) -> str:
        return SKILL.read_text(encoding="utf-8")

    def test_required_public_files_exist(self) -> None:
        required = {
            "SKILL.md",
            "CHANGELOG.md",
            "scripts/product-knowledge-sync-cli.py",
            "references/profile-contract.md",
            "references/impact-contract.md",
            "references/audit-workflow.md",
            "references/verification-gate.md",
            "references/evidence-model.md",
            "references/surface-semantics.md",
            "references/archivist-integration.md",
            "references/cross-skill-boundaries.md",
            "tests/cases.json",
            "tests/test_contract.py",
            "tests/test_cli.py",
            "tests/test_activation.py",
            "tests/test_cross_skill.py",
        }
        for relative in required:
            self.assertTrue((SKILL_DIR / relative).is_file(), relative)

    def test_frontmatter_identity(self) -> None:
        text = self.skill_text()
        self.assertRegex(text, r"(?m)^name: product-knowledge-sync$")
        self.assertRegex(text, r"(?m)^  category: content$")
        self.assertRegex(text, r'(?m)^  version: "1\.0"$')
        self.assertRegex(text, r"(?m)^  status: active$")
        self.assertRegex(text, r'(?m)^  tested: ".+"$')

    def test_three_modes_and_invariants_are_explicit(self) -> None:
        text = self.skill_text()
        for mode in ("`impact`", "`audit`", "`verify`"):
            self.assertIn(mode, text)
        self.assertIn("shadow product", text.lower())
        self.assertIn("semantic discovery is agentic", text.lower())
        self.assertIn("deterministic enforcement", text.lower())
        self.assertIn("continuous bidirectional synchronization", text.lower())

    def test_optional_archivist_and_evangelist_boundaries(self) -> None:
        text = self.skill_text()
        self.assertIn("project-documentation-archivist", text)
        self.assertIn("never initializes", text.lower())
        self.assertIn("project-evangelist", text)
        self.assertIn("handoff", text.lower())
        archivist = (SKILL_DIR / "references/archivist-integration.md").read_text(encoding="utf-8")
        self.assertIn("When Archivist is absent", archivist)
        self.assertIn("Do not initialize Archivist", archivist)

    def test_output_and_release_opt_in_are_explicit(self) -> None:
        text = self.skill_text()
        self.assertIn(".agent/product-knowledge/<run-id>/", text)
        self.assertIn(".coferlandia/product-knowledge/profile.json", text)
        self.assertIn("Installing this skill alone never adds a gate", text)

    def test_skill_maintenance_discipline_is_explicit(self) -> None:
        text = self.skill_text()
        self.assertIn("superpowers:writing-skills", text)
        self.assertIn("RED baseline", text)
        self.assertIn("smallest GREEN", text)
        self.assertIn("verification-before-completion", text)

    def test_skill_stays_within_hard_line_cap(self) -> None:
        self.assertLessEqual(len(self.skill_text().splitlines()), 500)

    def test_cases_cover_activation_and_pressure(self) -> None:
        cases = json.loads((SKILL_DIR / "tests/cases.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(cases["positive"]), 5)
        self.assertGreaterEqual(len(cases["negative"]), 5)
        self.assertGreaterEqual(len(cases["pressure"]), 6)
        all_text = json.dumps(cases).lower()
        for phrase in (
            "release notes",
            "catalog",
            "external",
            "archivist",
            "no product-knowledge profile",
            "different candidate",
        ):
            self.assertIn(phrase, all_text)

    def test_public_examples_are_generic(self) -> None:
        combined = "\n".join(
            path.read_text(encoding="utf-8")
            for path in SKILL_DIR.rglob("*")
            if path.is_file()
            and path.suffix in {".md", ".json", ".py"}
            and path.name != "test_contract.py"
        ).lower()
        private_markers = (
            "secre" + "taria",
            "cadencia" + ".com.ar",
            "diego" + "cofre",
            "api_" + "key=",
            "pass" + "word=",
        )
        for private_marker in private_markers:
            self.assertNotIn(private_marker, combined)

    def test_changelog_matches_version(self) -> None:
        changelog = (SKILL_DIR / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertRegex(changelog, r"(?m)^## 1\.0 — 2026-10-01$")


if __name__ == "__main__":
    unittest.main()
