"""Contract tests for the project-evangelist skill."""
from __future__ import annotations

import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
SKILL_DIR = ROOT / "skills/content/project-evangelist"
SKILL = SKILL_DIR / "SKILL.md"


class ProjectEvangelistContractTests(unittest.TestCase):
    def text(self, relative: str) -> str:
        return (ROOT / relative).read_text(encoding="utf-8")

    def skill_text(self) -> str:
        return SKILL.read_text(encoding="utf-8")

    def test_required_files_exist(self) -> None:
        required = {
            "SKILL.md",
            "references/documentation-model.md",
            "references/archivist-boundary.md",
            "references/evidence-and-validation.md",
            "assets/documentation-proposal.template.md",
            "assets/docs-index.template.md",
            "tests/cases.json",
            "tests/test_contract.py",
        }
        for relative in required:
            self.assertTrue((SKILL_DIR / relative).is_file(), relative)

    def test_frontmatter_identity_and_active_test_evidence(self) -> None:
        text = self.skill_text()
        self.assertRegex(text, r"(?m)^name: project-evangelist$")
        self.assertRegex(text, r"(?m)^  category: content$")
        self.assertRegex(text, r'(?m)^  version: "1\.0"$')
        self.assertRegex(text, r"(?m)^  status: active$")
        tested = re.search(r'(?m)^  tested: "(.+)"$', text)
        self.assertIsNotNone(tested)
        self.assertIn("validate_skill.py", tested.group(1))
        self.assertIn("natural activation", tested.group(1))

    def test_triggering_and_scope_boundaries(self) -> None:
        text = self.skill_text()
        self.assertIn("Use when", text)
        self.assertIn("developer documentation", text.lower())
        self.assertIn("Technology at a Glance", text)
        self.assertIn("Architecture at a Glance", text)
        self.assertIn("MkDocs", text)
        self.assertIn("not configure", text)
        self.assertIn("approval", text.lower())

    def test_required_references_are_conditionally_loaded(self) -> None:
        text = self.skill_text()
        for path in (
            "references/documentation-model.md",
            "references/archivist-boundary.md",
            "references/evidence-and-validation.md",
        ):
            self.assertIn(path, text)
            pattern = rf"Read `{re.escape(path)}` when"
            self.assertRegex(text, pattern)

    def test_output_locations_and_archivist_boundary(self) -> None:
        text = self.skill_text()
        self.assertIn(".agent/project-evangelist/", text)
        self.assertIn("`docs/**`", text)
        self.assertIn("Output Exceptions", text)
        self.assertIn("project-documentation-archivist", text)
        self.assertIn("README.md", text)
        self.assertIn("DECISIONS.md", text)
        self.assertIn("RUNBOOK.md", text)
        self.assertIn("link", text.lower())
        self.assertIn("duplicate", text.lower())

    def test_writing_skills_tdd_contract_is_present(self) -> None:
        text = self.skill_text()
        self.assertIn("superpowers:writing-skills", text)
        self.assertIn("positive and negative", text.lower())
        self.assertIn("baseline", text.lower())

    def test_cases_have_positive_and_negative_pressure_prompts(self) -> None:
        cases = json.loads((SKILL_DIR / "tests/cases.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(cases["positive"]), 4)
        self.assertGreaterEqual(len(cases["negative"]), 5)
        positive = " ".join(cases["positive"]).lower()
        negative = " ".join(cases["negative"]).lower()
        for term in ("stack", "architecture", "repository map", "contributor"):
            self.assertIn(term, positive)
        for term in ("archivist", "todo.md", "mkdocs", "webhook", "paragraph"):
            self.assertIn(term, negative)

    def test_templates_expose_required_sections(self) -> None:
        proposal = self.text("skills/content/project-evangelist/assets/documentation-proposal.template.md")
        for heading in (
            "Project Understanding",
            "Developer Audiences",
            "Reading Paths",
            "Existing Documentation Assessment",
            "Proposed Documentation Structure",
            "Evidence",
            "Contradictions and Unknowns",
            "Approval",
        ):
            self.assertIn(heading, proposal)
        index = self.text("skills/content/project-evangelist/assets/docs-index.template.md")
        for heading in (
            "What This Project Does",
            "Technology at a Glance",
            "Architecture at a Glance",
            "Main Capabilities",
            "Start Here",
            "Project Map",
            "Known Limitations",
            "Further Reading",
        ):
            self.assertIn(heading, index)
        self.assertIn("Remove unsupported sections", index)

    def test_index_and_release_surface_are_updated(self) -> None:
        index = self.text("skills/INDEX.md")
        self.assertIn("[project-evangelist](./content/project-evangelist/)", index)
        plugin = json.loads(self.text(".claude-plugin/plugin.json"))
        version = tuple(int(part) for part in plugin["version"].split("."))
        self.assertGreaterEqual(version, (2, 1, 0))
        notes = self.text("RELEASE-NOTES.md")
        self.assertIn("Project Evangelist", notes)
        self.assertIn("project-evangelist", notes)
        self.assertNotIn("No repo-wide plugin version bump is included", notes)


if __name__ == "__main__":
    unittest.main()
