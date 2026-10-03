from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from .errors import ValidationError

FRONTMATTER_RE = re.compile(r"\A---\n(?P<body>.*?)\n---\n", re.S)
WIKILINK_RE = re.compile(r"\[\[([^\]|#]+)")
MANAGED_START = "<!-- the-architect:managed:start -->"
MANAGED_END = "<!-- the-architect:managed:end -->"


def quote(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    return json.dumps(str(value), ensure_ascii=False)


def render_frontmatter(values: dict[str, Any]) -> str:
    lines = ["---"]
    for key, value in values.items():
        if isinstance(value, list):
            lines.append(f"{key}:")
            lines.extend(f"  - {quote(item)}" for item in value)
        else:
            lines.append(f"{key}: {quote(value)}")
    lines.append("---")
    return "\n".join(lines) + "\n"


def parse_frontmatter(text: str) -> dict[str, Any]:
    normalized = text.replace("\r\n", "\n")
    match = FRONTMATTER_RE.match(normalized)
    if not match:
        raise ValidationError("missing YAML frontmatter")
    result: dict[str, Any] = {}
    active_list: str | None = None
    for raw in match.group("body").splitlines():
        if not raw.strip() or raw.lstrip().startswith("#"):
            continue
        if raw.startswith("  - ") and active_list:
            result[active_list].append(_scalar(raw[4:]))
            continue
        if ":" not in raw or raw.startswith(" "):
            raise ValidationError(f"unsupported frontmatter line: {raw}")
        key, value = raw.split(":", 1)
        key, value = key.strip(), value.strip()
        if key in result:
            raise ValidationError(f"duplicate frontmatter key: {key}")
        if not value:
            result[key] = []
            active_list = key
        else:
            result[key] = _scalar(value)
            active_list = None
    return result


def _scalar(value: str) -> Any:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] == '"':
        try:
            return json.loads(value)
        except json.JSONDecodeError as exc:
            raise ValidationError(f"invalid quoted frontmatter scalar: {value}") from exc
    if value == "true":
        return True
    if value == "false":
        return False
    if value.isdigit():
        return int(value)
    return value


def body_without_frontmatter(text: str) -> str:
    normalized = text.replace("\r\n", "\n")
    match = FRONTMATTER_RE.match(normalized)
    return normalized[match.end():] if match else normalized


def update_managed(text: str, generated: str) -> str:
    has_start = MANAGED_START in text
    has_end = MANAGED_END in text
    if has_start != has_end:
        raise ValidationError("managed section has an unmatched start/end marker")
    block = f"{MANAGED_START}\n{generated.rstrip()}\n{MANAGED_END}"
    if has_start:
        pattern = re.compile(re.escape(MANAGED_START) + r".*?" + re.escape(MANAGED_END), re.S)
        return pattern.sub(block, text)
    return text.rstrip() + "\n\n" + block + "\n"


def wikilinks(text: str) -> list[str]:
    return [item.strip() for item in WIKILINK_RE.findall(text)]


def word_count(text: str) -> int:
    return len(re.findall(r"\b\w+[\w'-]*\b", body_without_frontmatter(text), re.UNICODE))


def markdown_files(home: Path) -> list[Path]:
    return sorted(path for path in home.rglob("*.md") if ".git" not in path.parts and ".obsidian" not in path.parts)
