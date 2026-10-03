from __future__ import annotations

from pathlib import Path

from .errors import ValidationError
from .markdown import parse_frontmatter, word_count

KNOWLEDGE_STATUS = {"confirmed", "inferred", "unknown", "stale", "conflicting", "not-applicable"}
COMPONENT_STATUS = {"candidate", "incubating", "stable", "deprecated", "retired"}
APPLICATION_STATUS = {"proposed", "active", "replaced", "removed"}
APPLICATION_RESULT = {"successful", "partial", "failed", "inconclusive"}
FITNESS = {"high", "medium", "low", "unknown"}
ADAPTATION = {"none", "configuration", "extension", "fork"}
EFFORT = {"xs", "s", "m", "l", "xl"}
STABILITY = {"proven", "promising", "unstable", "unknown"}
COST = {"low", "medium", "high", "unknown"}
EVIDENCE = {"anecdotal", "tested", "production-observed", "measured"}
RECOMMENDATION = {"recommended", "conditional", "not-recommended"}
TREATMENT = {"act-now", "plan-soon", "monitor", "accept", "no-action"}
TREND = {"improving", "stable", "worsening"}

REPORT_LIMITS = {
    "architect-addendum": 800,
    "project-record": 1000,
    "assessment-brief": 1500,
    "release-delta": 700,
    "application-result": 700,
    "extraction-summary": 1000,
}

REQUIRED_SECTIONS = {
    "project": ["Purpose and criticality", "System boundary", "Critical flows", "Quality attributes", "Active material risks", "Architectural runway", "Related records"],
    "component": ["Purpose", "Public contract", "Compatibility", "Limitations and contraindications", "Applications", "Cross-project lessons"],
    "component-application": ["Problem addressed", "Integration approach", "Adaptations and deviations", "Validation", "Operational results", "Limitations", "Reusable lesson", "Current recommendation", "Evidence"],
    "architecture-finding": ["Evidence", "Current consequence", "Future consequence", "Reason to act now", "Reason not to act now"],
    "architecture-decision": ["Context", "Decision", "Consequences", "Evidence"],
    "engagement": ["Scope examined", "Material changes", "Decisions", "Components", "Risks", "Quality impact", "Lessons", "Next actions", "Evidence"],
    "component-extraction": ["Objective", "Source behavior", "Component boundary", "Behavior to preserve", "Project-specific exclusions", "Public contract", "Tests", "Provenance and license", "Acceptance criteria"],
    "architecture-event": ["Event", "Evidence"],
}


def validate_record(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    fm = parse_frontmatter(text)
    for key in ("id", "type", "title"):
        if not fm.get(key):
            raise ValidationError(f"{path}: missing required frontmatter field '{key}'")
    entity_type = str(fm["type"])
    if entity_type not in REQUIRED_SECTIONS:
        raise ValidationError(f"{path}: unsupported architecture entity type '{entity_type}'")
    for section in REQUIRED_SECTIONS[entity_type]:
        if f"## {section}" not in text:
            raise ValidationError(f"{path}: missing required section '## {section}'")
    if entity_type == "project" and fm.get("knowledge_status") not in KNOWLEDGE_STATUS:
        raise ValidationError(f"{path}: invalid knowledge_status")
    if entity_type == "component":
        status = fm.get("status")
        evidence = fm.get("evidence_strength")
        if status not in COMPONENT_STATUS:
            raise ValidationError(f"{path}: invalid component status")
        if evidence not in EVIDENCE:
            raise ValidationError(f"{path}: invalid component evidence_strength")
        if status == "stable":
            required = ("validated_implementation", "automated_tests", "integration_documentation", "compatibility_documented", "provenance", "maintenance_policy")
            missing = [key for key in required if not fm.get(key)]
            if missing or evidence == "anecdotal":
                raise ValidationError(f"{path}: stable component lacks required evidence: {', '.join(missing) or 'evidence_strength'}")
    if entity_type == "component-application":
        for relationship in ("project", "component"):
            if not fm.get(relationship):
                raise ValidationError(f"{path}: missing required relationship '{relationship}'")
        enums = {
            "status": APPLICATION_STATUS,
            "result": APPLICATION_RESULT,
            "fitness": FITNESS,
            "adaptation_level": ADAPTATION,
            "integration_effort": EFFORT,
            "operational_stability": STABILITY,
            "maintenance_cost": COST,
            "evidence_strength": EVIDENCE,
            "reuse_recommendation": RECOMMENDATION,
        }
        for key, allowed in enums.items():
            if fm.get(key) not in allowed:
                raise ValidationError(f"{path}: invalid {key}")
    if entity_type == "architecture-finding":
        if fm.get("treatment") not in TREATMENT or fm.get("trend") not in TREND:
            raise ValidationError(f"{path}: invalid finding treatment or trend")
        for key in ("likelihood", "impact"):
            if not isinstance(fm.get(key), int) or not 1 <= fm[key] <= 5:
                raise ValidationError(f"{path}: {key} must be 1..5")
    return []


def validate_report(path: Path, kind: str, strict: bool = False) -> list[str]:
    if kind not in REPORT_LIMITS:
        raise ValidationError(f"unsupported report kind: {kind}")
    count = word_count(path.read_text(encoding="utf-8"))
    limit = REPORT_LIMITS[kind]
    if count <= limit:
        return []
    message = f"{path}: {count} words exceeds {kind} limit {limit}"
    if strict:
        raise ValidationError(message)
    return [message]
