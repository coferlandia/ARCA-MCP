from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import sys
SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from the_architect_cli.contracts import validate_record, validate_report  # noqa: E402
from the_architect_cli.errors import ValidationError  # noqa: E402
from the_architect_cli.markdown import parse_frontmatter, render_frontmatter, update_managed  # noqa: E402
from the_architect_cli.registry import component_template, project_template  # noqa: E402


class ContractTests(unittest.TestCase):
    def write(self, root: Path, name: str, content: str) -> Path:
        path = root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        return path

    def test_project_and_component_templates_validate(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            p_rel, p_text = project_template("sample", "Sample")
            c_rel, c_text = component_template("outbox", "Outbox", "library")
            self.assertEqual([], validate_record(self.write(root, p_rel, p_text)))
            self.assertEqual([], validate_record(self.write(root, c_rel, c_text)))

    def test_frontmatter_quotes_arbitrary_titles_and_rejects_duplicate_keys(self) -> None:
        rendered = render_frontmatter({"id": "PROJECT-1", "type": "project", "title": "true: #1"})
        self.assertEqual("true: #1", parse_frontmatter(rendered)["title"])
        with self.assertRaisesRegex(ValidationError, "duplicate frontmatter key"):
            parse_frontmatter("---\nid: ONE\nid: TWO\n---\n")

    def test_invalid_application_enum_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            text = render_frontmatter({
                "id": "APP-demo", "type": "component-application", "title": "Demo",
                "project": "[[PROJECT-demo]]", "component": "[[COMP-demo]]",
                "status": "active", "result": "excellent", "fitness": "high",
                "adaptation_level": "none", "integration_effort": "s",
                "operational_stability": "proven", "maintenance_cost": "low",
                "evidence_strength": "measured", "reuse_recommendation": "recommended",
            }) + "# Demo\n\n" + "\n\n".join(f"## {s}" for s in (
                "Problem addressed", "Integration approach", "Adaptations and deviations", "Validation",
                "Operational results", "Limitations", "Reusable lesson", "Current recommendation", "Evidence"))
            with self.assertRaisesRegex(ValidationError, "invalid result"):
                validate_record(self.write(Path(tmp), "app.md", text))

    def test_stable_component_requires_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, text = component_template("demo", "Demo", "library")
            text = text.replace('status: "candidate"', 'status: "stable"')
            with self.assertRaisesRegex(ValidationError, "stable component lacks required evidence"):
                validate_record(self.write(Path(tmp), "component.md", text))

    def test_component_rejects_unknown_evidence_strength(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            _, text = component_template("demo", "Demo", "library")
            text = text.replace('evidence_strength: "anecdotal"', 'evidence_strength: "magical"')
            with self.assertRaisesRegex(ValidationError, "invalid component evidence_strength"):
                validate_record(self.write(Path(tmp), "component.md", text))

    def test_unknown_entity_type_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            text = render_frontmatter({"id": "OTHER-demo", "type": "other", "title": "Other"}) + "# Other\n"
            with self.assertRaisesRegex(ValidationError, "unsupported architecture entity type"):
                validate_record(self.write(Path(tmp), "other.md", text))

    def test_finding_requires_valid_risk_fields(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            text = render_frontmatter({
                "id": "ARCH-demo", "type": "architecture-finding", "title": "Demo",
                "likelihood": 6, "impact": 1, "trend": "stable", "treatment": "monitor",
            }) + "# Demo\n\n" + "\n\n".join(f"## {s}" for s in (
                "Evidence", "Current consequence", "Future consequence", "Reason to act now", "Reason not to act now"))
            with self.assertRaisesRegex(ValidationError, "likelihood must be 1..5"):
                validate_record(self.write(Path(tmp), "finding.md", text))

    def test_report_limit_warns_and_strict_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self.write(Path(tmp), "report.md", "word " * 801)
            self.assertEqual(1, len(validate_report(path, "architect-addendum")))
            with self.assertRaises(ValidationError):
                validate_report(path, "architect-addendum", strict=True)

    def test_managed_update_preserves_human_authored_text(self) -> None:
        original = "# Dashboard\n\nHuman note.\n"
        first = update_managed(original, "- [[PROJECT-one]]")
        second = update_managed(first, "- [[PROJECT-two]]")
        self.assertIn("Human note.", second)
        self.assertNotIn("PROJECT-one", second)
        self.assertIn("PROJECT-two", second)

    def test_unmatched_managed_marker_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValidationError, "unmatched"):
            update_managed("# Dashboard\n\n<!-- the-architect:managed:start -->\n", "new")


if __name__ == "__main__":
    unittest.main()
