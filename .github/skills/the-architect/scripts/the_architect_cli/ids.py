from __future__ import annotations

import re

PREFIXES = {
    "project": "PROJECT",
    "component": "COMP",
    "engagement": "ENG",
    "decision": "ADR",
    "finding": "ARCH",
    "application": "APP",
    "extraction": "EXTRACT",
    "event": "EVENT",
}


def slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.strip().lower()).strip("-")
    if not slug:
        raise ValueError("slug cannot be empty")
    return slug


def stable_id(kind: str, slug: str) -> str:
    try:
        prefix = PREFIXES[kind]
    except KeyError as exc:
        raise ValueError(f"unsupported entity kind: {kind}") from exc
    return f"{prefix}-{slugify(slug)}"
