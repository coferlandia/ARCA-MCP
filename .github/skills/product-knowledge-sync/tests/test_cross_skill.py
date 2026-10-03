"""Cross-skill compatibility tests for Product Knowledge Sync."""
from __future__ import annotations

import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]


class ProductKnowledgeCrossSkillTests(unittest.TestCase):
    def text(self, relative: str) -> str:
        return (ROOT / relative).read_text(encoding="utf-8")

    def test_project_manager_has_optional_product_knowledge_handoff(self) -> None:
        text = self.text("skills/ops/coferlandia-project-manager/SKILL.md")
        self.assertIn('version: "0.9.0"', text)
        self.assertIn("## Product Knowledge Impact", text)
        self.assertIn("do not\ninvent a new release gate", text)
        self.assertIn("references/product-knowledge-impact.md", text)

    def test_software_development_preserves_and_reviews_explicit_handoff(self) -> None:
        text = self.text("skills/engineering/software-development/SKILL.md")
        self.assertIn('version: "4.7"', text)
        self.assertIn("## Product Knowledge Impact", text)
        self.assertIn("Analyst preserves", text)
        self.assertIn("Code Reviewer checks the exact impact reference", text)
        self.assertIn("existing development behavior is unchanged", text)
        self.assertIn("references/product-knowledge-impact.md", text)

    def test_archivist_and_evangelist_keep_existing_ownership(self) -> None:
        archivist = self.text("skills/content/project-documentation-archivist/SKILL.md")
        evangelist = self.text("skills/content/project-evangelist/SKILL.md")
        self.assertIn("durable knowledge layer", archivist)
        self.assertIn("does **not** own project work state", archivist)
        self.assertIn("developer documentation", evangelist)
        self.assertIn("does not own durable project memory", evangelist)

    def test_generic_release_and_ci_adapter_contracts_are_not_product_specific(self) -> None:
        for relative in (
            "prompts/chat-release.md",
            "skills/engineering/local-release/SKILL.md",
            "skills/meta/coferlandia-ci-adapter/SKILL.md",
        ):
            text = self.text(relative).lower()
            self.assertNotIn("product-knowledge-sync", text, relative)

    def test_required_fixture_families_exist(self) -> None:
        fixture_root = ROOT / "skills/content/product-knowledge-sync/tests/fixtures"
        for name in (
            "minimal-project",
            "archivist-project",
            "repository-and-external-surfaces",
            "stale-release-evidence",
        ):
            self.assertTrue((fixture_root / name).is_dir(), name)


if __name__ == "__main__":
    unittest.main()
