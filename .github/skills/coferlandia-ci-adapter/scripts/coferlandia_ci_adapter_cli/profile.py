from __future__ import annotations

import hashlib
import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any

PROFILE_PATH = Path(".coferlandia/ci/profile.json")
VERSION_RE = re.compile(r"^\d+\.\d+\.\d+$")
ENV_NAME_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
ALLOWED_TOP = {"schema_version", "profile_version", "repository", "documentation", "local", "github", "identity", "exceptions", "fingerprint"}
FORBIDDEN_KEY_PARTS = {"secret", "password", "passwd", "token", "credential", "private_key"}


def _forbidden_key(key: str) -> bool:
    normalized = key.lower().replace("-", "_")
    return any(part in normalized for part in FORBIDDEN_KEY_PARTS)


def _scan_forbidden(value: Any, path: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if _forbidden_key(str(key)):
                raise ValueError(f"forbidden secret-bearing key at {path}.{key}")
            _scan_forbidden(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _scan_forbidden(child, f"{path}[{index}]")


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _unique_ids(items: list[dict], label: str) -> None:
    ids = [str(item.get("id", "")) for item in items]
    _require(all(ids), f"{label} entries require non-empty id")
    _require(len(ids) == len(set(ids)), f"{label} ids must be unique")


def validate_profile(profile: dict, *, verify_fingerprint: bool = False) -> None:
    _require(isinstance(profile, dict), "profile must be an object")
    unknown = set(profile) - ALLOWED_TOP
    _require(not unknown, f"unknown top-level fields: {sorted(unknown)}")
    _scan_forbidden(profile)
    _require(profile.get("schema_version") == 1, "schema_version must be 1")
    _require(isinstance(profile.get("profile_version"), str) and VERSION_RE.match(profile["profile_version"]) is not None, "profile_version must be MAJOR.MINOR.PATCH")
    repository = profile.get("repository")
    _require(isinstance(repository, str) and repository.count("/") == 1 and all(repository.split("/")), "repository must be owner/name")
    docs = profile.get("documentation")
    _require(isinstance(docs, list) and all(isinstance(x, str) and x for x in docs), "documentation must be a string list")
    _require(len(docs) == len(set(docs)), "documentation entries must be unique")

    local = profile.get("local")
    _require(isinstance(local, dict), "local must be an object")
    _require(set(local) == {"working_directory", "qualification_commands", "required_services", "environment"}, "local fields are invalid")
    _require(isinstance(local["working_directory"], str) and local["working_directory"], "local.working_directory is required")
    commands = local["qualification_commands"]
    _require(isinstance(commands, list) and commands, "local.qualification_commands must be non-empty")
    _unique_ids(commands, "local.qualification_commands")
    for item in commands:
        _require(set(item) == {"id", "command", "purpose"}, "qualification command fields are invalid")
        _require(all(isinstance(item[key], str) and item[key] for key in ("id", "command", "purpose")), "qualification command values must be non-empty strings")
    services = local["required_services"]
    _require(isinstance(services, list) and all(isinstance(x, str) and x for x in services), "local.required_services must be a string list")
    environment = local["environment"]
    _require(isinstance(environment, list) and all(isinstance(x, str) and ENV_NAME_RE.fullmatch(x) for x in environment), "local.environment must contain variable names only, never NAME=value or secret values")
    _require(len(environment) == len(set(environment)), "local.environment entries must be unique")

    github = profile.get("github")
    _require(isinstance(github, dict) and set(github) == {"submission", "gates", "merge_group"}, "github fields are invalid")
    submission = github["submission"]
    _require(isinstance(submission, dict), "github.submission must be an object")
    _require(submission.get("mode") in {"existing-pr-events", "workflow-dispatch", "none"}, "invalid github.submission.mode")
    _require(set(submission) <= {"mode", "workflow"}, "unknown github.submission fields")
    if submission["mode"] == "workflow-dispatch":
        _require(isinstance(submission.get("workflow"), str) and submission["workflow"], "workflow-dispatch requires workflow")
    gates = github["gates"]
    _require(isinstance(gates, list) and gates, "github.gates must be non-empty")
    _unique_ids(gates, "github.gates")
    for gate in gates:
        _require(set(gate) <= {"id", "kind", "workflow", "name", "allowed_conclusions"}, "unknown gate fields")
        _require(gate.get("kind") in {"workflow", "check_run"}, "gate kind must be workflow or check_run")
        if gate["kind"] == "workflow":
            _require(isinstance(gate.get("workflow"), str) and gate["workflow"], "workflow gate requires workflow path/id")
        else:
            _require(isinstance(gate.get("name"), str) and gate["name"], "check_run gate requires name")
        allowed = gate.get("allowed_conclusions")
        _require(isinstance(allowed, list) and allowed and all(isinstance(x, str) and x for x in allowed), "allowed_conclusions must be non-empty")
    merge_group = github["merge_group"]
    _require(isinstance(merge_group, dict) and set(merge_group) == {"supported", "authoritative_when_present"}, "merge_group fields are invalid")
    _require(all(isinstance(merge_group[k], bool) for k in merge_group), "merge_group values must be boolean")
    if merge_group["authoritative_when_present"]:
        _require(merge_group["supported"], "authoritative_when_present requires supported=true")

    identity = profile.get("identity")
    _require(isinstance(identity, dict) and set(identity) == {"base_sensitive", "profile_sensitive"}, "identity fields are invalid")
    _require(all(isinstance(identity[k], bool) for k in identity), "identity values must be boolean")

    exceptions = profile.get("exceptions")
    _require(isinstance(exceptions, list), "exceptions must be a list")
    _unique_ids(exceptions, "exceptions") if exceptions else None
    for item in exceptions:
        _require(set(item) == {"id", "documentation", "authorization"}, "exception fields are invalid")
        _require(item["authorization"] in {"external", "repository-policy"}, "invalid exception authorization")
        _require(isinstance(item["documentation"], str) and item["documentation"], "exception documentation is required")

    if "fingerprint" in profile:
        _require(isinstance(profile["fingerprint"], str) and re.fullmatch(r"[a-f0-9]{64}", profile["fingerprint"]) is not None, "fingerprint must be sha256 hex")
        if verify_fingerprint:
            _require(profile["fingerprint"] == profile_fingerprint(profile), "profile fingerprint mismatch")


def canonical_profile(profile: dict) -> dict:
    clean = json.loads(json.dumps(profile))
    clean.pop("fingerprint", None)
    return clean


def profile_fingerprint(profile: dict) -> str:
    validate_profile(canonical_profile(profile))
    payload = json.dumps(canonical_profile(profile), sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def load_profile(path: Path, *, verify_fingerprint: bool = False) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    validate_profile(data, verify_fingerprint=verify_fingerprint)
    return data


def render_profile(source: dict) -> dict:
    validate_profile(canonical_profile(source))
    rendered = canonical_profile(source)
    rendered["fingerprint"] = profile_fingerprint(rendered)
    return rendered


def atomic_write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(value, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
        os.replace(tmp_name, path)
    except Exception:
        try:
            os.unlink(tmp_name)
        except OSError:
            pass
        raise
