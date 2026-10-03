from __future__ import annotations

import argparse
import json
import sys
from datetime import date
from pathlib import Path
from typing import Any

from . import __version__
from .contracts import validate_record, validate_report
from .errors import ArchitectError, ConfigurationError, ValidationError
from .ids import stable_id, slugify
from .markdown import render_frontmatter
from .output import emit, envelope
from .paths import DEFAULT_CONFIG, resolve_home
from .registry import (
    application_endpoints,
    component_template,
    create_unique,
    init_home,
    link_application,
    project_endpoint,
    project_template,
    rebuild_indexes,
    records,
    validate_home,
    validate_indexes,
    validate_links,
)

CAPABILITIES = {
    "home": ["init", "status", "validate"],
    "project": ["register", "show", "list"],
    "component": ["register", "show", "list", "search"],
    "engagement": ["create", "validate"],
    "decision": ["create", "validate"],
    "finding": ["create", "validate"],
    "application": ["create", "validate"],
    "extraction": ["create", "validate"],
    "event": ["create", "validate"],
    "index": ["rebuild", "validate"],
    "links": ["validate"],
    "report": ["validate"],
}

ENTITY_TYPES = {
    "engagement": ("engagement", "ENG", "projects/{project}/engagements/{id}.md"),
    "decision": ("architecture-decision", "ADR", "projects/{project}/decisions/{id}.md"),
    "finding": ("architecture-finding", "ARCH", "projects/{project}/findings/{id}.md"),
    "application": ("component-application", "APP", "applications/{id}.md"),
    "extraction": ("component-extraction", "EXTRACT", "extractions/{id}.md"),
    "event": ("architecture-event", "EVENT", "events/{id}.md"),
}


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Deterministic architecture-home mechanics for the-architect")
    p.add_argument("--config", default=str(DEFAULT_CONFIG))
    p.add_argument("--home", help="Override configured architecture-home path")
    p.add_argument("--json", action="store_true", help="Accepted for compatibility; output is always JSON")
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("version")
    sub.add_parser("self-check")
    sub.add_parser("capabilities")

    home = sub.add_parser("home").add_subparsers(dest="action", required=True)
    init = home.add_parser("init"); init.add_argument("--dry-run", action="store_true")
    home.add_parser("status"); home.add_parser("validate")

    project = sub.add_parser("project").add_subparsers(dest="action", required=True)
    reg = project.add_parser("register"); reg.add_argument("--slug", required=True); reg.add_argument("--title", required=True); reg.add_argument("--dry-run", action="store_true")
    show = project.add_parser("show"); show.add_argument("--slug", required=True)
    project.add_parser("list")

    component = sub.add_parser("component").add_subparsers(dest="action", required=True)
    reg = component.add_parser("register"); reg.add_argument("--slug", required=True); reg.add_argument("--title", required=True); reg.add_argument("--kind", default="library"); reg.add_argument("--status", default="candidate"); reg.add_argument("--dry-run", action="store_true")
    show = component.add_parser("show"); show.add_argument("--slug", required=True)
    component.add_parser("list")
    search = component.add_parser("search"); search.add_argument("query")

    for name in ENTITY_TYPES:
        group = sub.add_parser(name).add_subparsers(dest="action", required=True)
        create = group.add_parser("create")
        create.add_argument("--slug", required=True)
        create.add_argument("--title", required=True)
        create.add_argument("--project")
        create.add_argument("--component")
        create.add_argument("--no-material-change", action="store_true")
        create.add_argument("--dry-run", action="store_true")
        validate = group.add_parser("validate"); validate.add_argument("path")

    index = sub.add_parser("index").add_subparsers(dest="action", required=True)
    rebuild = index.add_parser("rebuild"); rebuild.add_argument("--dry-run", action="store_true")
    index.add_parser("validate")
    links = sub.add_parser("links").add_subparsers(dest="action", required=True); links.add_parser("validate")
    report = sub.add_parser("report").add_subparsers(dest="action", required=True)
    validate = report.add_parser("validate"); validate.add_argument("path"); validate.add_argument("--kind", required=True); validate.add_argument("--strict", action="store_true")
    return p


def _home(args: argparse.Namespace) -> Path:
    return resolve_home(Path(args.config).expanduser(), args.home)


def _entity_content(group: str, args: argparse.Namespace) -> tuple[str, str] | None:
    if args.no_material_change:
        return None
    if group in {"engagement", "decision", "finding", "extraction"} and not args.project:
        raise ValidationError(f"{group} create requires --project")
    if group == "application" and (not args.project or not args.component):
        raise ValidationError("application create requires --project and --component")
    entity_type, _, pattern = ENTITY_TYPES[group]
    entity_id = stable_id(group, args.slug)
    project = slugify(args.project) if args.project else "portfolio"
    relative = pattern.format(project=project, id=entity_id)
    fm: dict[str, Any] = {"id": entity_id, "type": entity_type, "title": args.title, "created": date.today().isoformat()}
    if args.project:
        fm["project"] = f"[[PROJECT-{project}]]"
    if args.component:
        fm["component"] = f"[[COMP-{slugify(args.component)}]]"
    sections = {
        "engagement": ["Scope examined", "Material changes", "Decisions", "Components", "Risks", "Quality impact", "Lessons", "Next actions", "Evidence"],
        "decision": ["Context", "Decision", "Consequences", "Evidence"],
        "finding": ["Evidence", "Current consequence", "Future consequence", "Reason to act now", "Reason not to act now"],
        "application": ["Problem addressed", "Integration approach", "Adaptations and deviations", "Validation", "Operational results", "Limitations", "Reusable lesson", "Current recommendation", "Evidence"],
        "extraction": ["Objective", "Source behavior", "Component boundary", "Behavior to preserve", "Project-specific exclusions", "Public contract", "Tests", "Provenance and license", "Acceptance criteria"],
        "event": ["Event", "Evidence"],
    }[group]
    if group == "finding":
        fm.update({"likelihood": 1, "impact": 1, "confidence": "low", "trend": "stable", "remediation_effort": "unknown", "architectural_leverage": "local", "treatment": "monitor"})
    if group == "application":
        fm.update({"status": "proposed", "result": "inconclusive", "fitness": "unknown", "adaptation_level": "none", "integration_effort": "xs", "operational_stability": "unknown", "maintenance_cost": "unknown", "evidence_strength": "anecdotal", "reuse_recommendation": "conditional", "component_version": "unknown"})
    body = f"# {args.title}\n\n" + "\n\n".join(f"## {section}" for section in sections) + "\n"
    return relative, render_frontmatter(fm) + body


def dispatch(args: argparse.Namespace) -> dict[str, Any]:
    command = args.command if not hasattr(args, "action") else f"{args.command} {args.action}"
    if args.command == "version":
        return envelope(command, data={"version": __version__})
    if args.command == "capabilities":
        return envelope(command, data=CAPABILITIES)
    if args.command == "self-check":
        config = Path(args.config).expanduser()
        data: dict[str, Any] = {"config": str(config), "config_exists": config.exists(), "git_operations": False}
        warnings: list[str] = []
        try:
            home = _home(args)
            data.update({"home": str(home), "home_exists": home.exists()})
        except ConfigurationError as exc:
            warnings.append(str(exc))
        return envelope(command, data=data, warnings=warnings)

    home = _home(args)
    if args.command == "home":
        if args.action == "init": return envelope(command, data={"paths": init_home(home, args.dry_run), "dry_run": args.dry_run})
        if args.action == "status": return envelope(command, data={"home": str(home), "exists": home.exists(), "records": len(records(home)) if home.exists() else 0})
        return envelope(command, data={"validated": True, "warnings": validate_home(home)})
    if args.command == "project":
        if args.action == "register":
            relative, content = project_template(args.slug, args.title)
            result = create_unique(home, relative, content, args.dry_run)
            if not args.dry_run: rebuild_indexes(home)
            return envelope(command, data=result)
        matches = [(path, fm) for path, fm in records(home) if fm.get("type") == "project"]
        if args.action == "show":
            target = stable_id("project", args.slug)
            for path, fm in matches:
                if fm.get("id") == target: return envelope(command, data={"path": str(path), "record": fm})
            raise ValidationError(f"unknown project: {target}")
        return envelope(command, data=[{"path": str(path), **fm} for path, fm in matches])
    if args.command == "component":
        if args.action == "register":
            relative, content = component_template(args.slug, args.title, args.kind, args.status)
            result = create_unique(home, relative, content, args.dry_run)
            if not args.dry_run: rebuild_indexes(home)
            return envelope(command, data=result)
        matches = [(path, fm) for path, fm in records(home) if fm.get("type") == "component"]
        if args.action == "show":
            target = stable_id("component", args.slug)
            for path, fm in matches:
                if fm.get("id") == target: return envelope(command, data={"path": str(path), "record": fm})
            raise ValidationError(f"unknown component: {target}")
        if args.action == "search":
            query = args.query.lower()
            matches = [(p, fm) for p, fm in matches if query in str(fm.get("title", "")).lower() or query in str(fm.get("id", "")).lower()]
        return envelope(command, data=[{"path": str(path), **fm} for path, fm in matches])
    if args.command in ENTITY_TYPES:
        if args.action == "validate": return envelope(command, data={"validated": str(Path(args.path)), "warnings": validate_record(Path(args.path))})
        prepared = _entity_content(args.command, args)
        if prepared is None:
            return envelope(command, data={"created": False, "reason": "No material architectural change."})
        if args.command in {"engagement", "decision", "finding", "extraction"}:
            project_endpoint(home, args.project)
        elif args.command == "event" and args.project:
            project_endpoint(home, args.project)
        if args.command == "application":
            application_endpoints(home, args.project, args.component)
        relative, content = prepared
        result = create_unique(home, relative, content, args.dry_run)
        relationship_changes: list[str] = []
        if args.command == "application":
            relationship_changes = link_application(home, stable_id("application", args.slug), args.project, args.component, args.dry_run)
        if not args.dry_run: rebuild_indexes(home)
        result["relationship_links"] = relationship_changes
        return envelope(command, data=result)
    if args.command == "index":
        if args.action == "rebuild": return envelope(command, data={"changed": rebuild_indexes(home, args.dry_run), "dry_run": args.dry_run})
        return envelope(command, data={"validated": True, "warnings": validate_indexes(home)})
    if args.command == "links": return envelope(command, data={"validated": True, "warnings": validate_links(home)})
    if args.command == "report": return envelope(command, data={"validated": str(Path(args.path)), "warnings": validate_report(Path(args.path), args.kind, args.strict)})
    raise ValidationError(f"unsupported command: {command}")


def main(argv: list[str] | None = None) -> int:
    normalized = list(sys.argv[1:] if argv is None else argv)
    normalized = [item for item in normalized if item != "--json"]
    args = parser().parse_args(normalized)
    command = args.command if not hasattr(args, "action") else f"{args.command} {args.action}"
    try:
        emit(dispatch(args))
        return 0
    except ArchitectError as exc:
        print(str(exc), file=sys.stderr)
        emit(envelope(command, ok=False, error=str(exc)))
        return 2
    except (OSError, ValueError) as exc:
        print(str(exc), file=sys.stderr)
        emit(envelope(command, ok=False, error=str(exc)))
        return 3
