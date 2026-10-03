from __future__ import annotations

from pathlib import Path
from typing import Any

from .contracts import validate_record
from .errors import ConflictError, ValidationError
from .ids import stable_id, slugify
from .markdown import MANAGED_END, MANAGED_START, markdown_files, parse_frontmatter, render_frontmatter, update_managed, wikilinks
from .paths import atomic_write, confined

DIRECTORIES = ["dashboards", "projects", "components", "applications", "extractions", "events", "patterns", "policies", "schemas"]
DASHBOARDS = ["PROJECTS", "COMPONENTS", "DECISIONS", "RISKS", "APPLICATIONS", "EXTRACTIONS", "TIMELINE"]
MANAGED_ENTITY_PREFIXES = ("PROJECT-", "COMP-", "ADR-", "ARCH-", "APP-", "ENG-", "EXTRACT-", "EVENT-")


def home_skeleton() -> dict[str, str]:
    home = "# Architecture Home\n\nPortable cross-project architecture memory.\n\n## Dashboards\n\n" + "\n".join(f"- [[dashboards/{name}]]" for name in DASHBOARDS) + "\n"
    result = {"HOME.md": home, ".gitignore": ".obsidian/\n"}
    for name in DASHBOARDS:
        result[f"dashboards/{name}.md"] = f"# {name.title()}\n\n{MANAGED_START}\n_No records._\n{MANAGED_END}\n"
    return result


def init_home(home: Path, dry_run: bool = False) -> list[str]:
    planned: list[str] = []
    for directory in DIRECTORIES:
        path = confined(home, directory)
        planned.append(str(path))
        if not dry_run:
            path.mkdir(parents=True, exist_ok=True)
    for relative, content in home_skeleton().items():
        path = confined(home, relative)
        planned.append(str(path))
        if not dry_run and not path.exists():
            atomic_write(path, content)
    return planned


def project_template(slug: str, title: str) -> tuple[str, str]:
    slug = slugify(slug)
    fm = {
        "id": stable_id("project", slug), "type": "project", "title": title, "slug": slug,
        "status": "active", "lifecycle_stage": "unknown", "criticality": "unknown",
        "knowledge_status": "unknown", "architecture_confidence": "low", "last_assessed": "unknown",
    }
    sections = [
        "Purpose and criticality", "System boundary", "Critical flows", "Deployment and operations",
        "Data boundaries", "Quality attributes", "Critical quality scenarios", "Active material risks",
        "Accepted constraints", "Components", "Architectural runway", "Knowledge completeness", "Related records",
    ]
    body = f"# {title}\n\n" + "\n\n".join(f"## {section}" for section in sections) + "\n"
    return f"projects/{slug}/PROJECT-{slug}.md", render_frontmatter(fm) + body


def component_template(slug: str, title: str, artifact_kind: str, status: str = "candidate") -> tuple[str, str]:
    slug = slugify(slug)
    if status not in {"candidate", "incubating"}:
        raise ValidationError("new components may only start as candidate or incubating")
    fm = {
        "id": stable_id("component", slug), "type": "component", "title": title, "slug": slug,
        "artifact_kind": artifact_kind, "status": status, "version": "0.1.0", "evidence_strength": "anecdotal",
    }
    sections = [
        "Purpose", "Problem solved", "Public contract", "Supported environments", "Configuration",
        "Dependencies", "Quality contribution", "Compatibility", "Limitations and contraindications",
        "Provenance and license", "Applications", "Upgrade and deprecation policy", "Cross-project lessons",
    ]
    body = f"# {title}\n\n" + "\n\n".join(f"## {section}" for section in sections) + "\n"
    return f"components/{slug}/COMP-{slug}.md", render_frontmatter(fm) + body


def create_unique(home: Path, relative: str, content: str, dry_run: bool = False) -> dict[str, Any]:
    path = confined(home, relative)
    if path.exists():
        if path.read_text(encoding="utf-8") == content:
            return {"path": str(path), "created": False, "idempotent": True}
        raise ConflictError(f"record already exists with different content: {path}")
    if not dry_run:
        path.parent.mkdir(parents=True, exist_ok=True)
        atomic_write(path, content)
    return {"path": str(path), "created": not dry_run, "dry_run": dry_run}


def records(home: Path) -> list[tuple[Path, dict[str, Any]]]:
    found: list[tuple[Path, dict[str, Any]]] = []
    seen: dict[str, Path] = {}
    for path in markdown_files(home):
        try:
            fm = parse_frontmatter(path.read_text(encoding="utf-8"))
        except ValidationError as exc:
            if path.stem.startswith(MANAGED_ENTITY_PREFIXES):
                raise ValidationError(f"{path}: invalid managed entity: {exc}") from exc
            continue
        entity_id = fm.get("id")
        if not entity_id:
            if path.stem.startswith(MANAGED_ENTITY_PREFIXES):
                raise ValidationError(f"{path}: managed entity is missing id")
            continue
        if entity_id in seen:
            raise ValidationError(f"duplicate id {entity_id}: {seen[entity_id]} and {path}")
        seen[str(entity_id)] = path
        found.append((path, fm))
    return found


def _render_indexes(home: Path) -> dict[Path, str]:
    grouped: dict[str, list[str]] = {name: [] for name in DASHBOARDS}
    mapping = {
        "project": "PROJECTS", "component": "COMPONENTS", "architecture-decision": "DECISIONS",
        "architecture-finding": "RISKS", "component-application": "APPLICATIONS",
        "component-extraction": "EXTRACTIONS", "engagement": "TIMELINE", "architecture-event": "TIMELINE",
    }
    for path, fm in records(home):
        dashboard = mapping.get(str(fm.get("type")))
        if dashboard:
            rel = path.relative_to(home).with_suffix("")
            grouped[dashboard].append(f"- [[{rel.as_posix()}|{fm.get('title', fm.get('id'))}]] — `{fm.get('id')}`")
    rendered: dict[Path, str] = {}
    for dashboard, lines in grouped.items():
        path = confined(home, f"dashboards/{dashboard}.md")
        existing = path.read_text(encoding="utf-8") if path.exists() else f"# {dashboard.title()}\n"
        rendered[path] = update_managed(existing, "\n".join(sorted(lines)) or "_No records._")
    return rendered


def rebuild_indexes(home: Path, dry_run: bool = False) -> list[str]:
    changed: list[str] = []
    for path, updated in _render_indexes(home).items():
        existing = path.read_text(encoding="utf-8") if path.exists() else ""
        if updated != existing:
            changed.append(str(path))
            if not dry_run:
                atomic_write(path, updated)
    return changed


def validate_indexes(home: Path) -> list[str]:
    stale = rebuild_indexes(home, dry_run=True)
    if stale:
        raise ValidationError("architecture indexes are stale: " + ", ".join(stale))
    return []


def project_endpoint(home: Path, project_slug: str) -> Path:
    slug = slugify(project_slug)
    path = confined(home, f"projects/{slug}/PROJECT-{slug}.md")
    if not path.is_file():
        raise ValidationError(f"project not registered: {path}")
    return path


def application_endpoints(home: Path, project_slug: str, component_slug: str) -> tuple[Path, Path]:
    project = project_endpoint(home, project_slug)
    slug = slugify(component_slug)
    component = confined(home, f"components/{slug}/COMP-{slug}.md")
    if not component.is_file():
        raise ValidationError(f"application endpoint not registered: {component}")
    return project, component


def link_application(home: Path, application_id: str, project_slug: str, component_slug: str, dry_run: bool = False) -> list[str]:
    link = f"- [[{application_id}]]"
    changed: list[str] = []
    for path in application_endpoints(home, project_slug, component_slug):
        existing = path.read_text(encoding="utf-8")
        managed: list[str] = []
        if MANAGED_START in existing and MANAGED_END in existing:
            block = existing.split(MANAGED_START, 1)[1].split(MANAGED_END, 1)[0]
            managed = [line.strip() for line in block.splitlines() if line.strip().startswith("- [[")]
        updated = update_managed(existing, "\n".join(sorted(set([*managed, link]))))
        if updated != existing:
            changed.append(str(path))
            if not dry_run:
                atomic_write(path, updated)
    return changed


def validate_links(home: Path) -> list[str]:
    id_to_path = {str(fm["id"]): path for path, fm in records(home)}
    stem_to_path = {path.stem: path for path in markdown_files(home)}
    broken: list[str] = []
    for path in markdown_files(home):
        for target in wikilinks(path.read_text(encoding="utf-8")):
            short = target.split("/")[-1]
            if target.startswith(MANAGED_ENTITY_PREFIXES) and target not in id_to_path and short not in stem_to_path:
                broken.append(f"{path.relative_to(home)} -> {target}")
    if broken:
        raise ValidationError("broken managed wikilinks: " + "; ".join(broken))
    return []


def validate_home(home: Path) -> list[str]:
    for required in ("HOME.md", "dashboards/PROJECTS.md", "projects", "components", "applications"):
        if not confined(home, required).exists():
            raise ValidationError(f"architecture home missing: {required}")
    for path, _ in records(home):
        validate_record(path)
    validate_links(home)
    validate_indexes(home)
    return []
