from __future__ import annotations
import hashlib
import json
import tempfile
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

class ReleaseError(RuntimeError):
    pass

def canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))

def fingerprint(value: Any) -> str:
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()

@dataclass(frozen=True)
class ReleaseArtifact:
    path: str
    name: str
    sha256: str
    size: int

    @classmethod
    def from_path(cls, path: Path) -> "ReleaseArtifact":
        if not path.is_file():
            raise ReleaseError(f"release artifact does not exist: {path}")
        data = path.read_bytes()
        return cls(str(path.resolve()), path.name, hashlib.sha256(data).hexdigest(), len(data))

@dataclass(frozen=True)
class ValidationEvidence:
    name: str
    status: str
    conclusion: str | None = None
    url: str | None = None

@dataclass(frozen=True)
class ReleaseIdentity:
    repository: str
    version: str
    tag: str
    commit: str

@dataclass
class ReleasePlan:
    schema_version: int
    repository: str
    target_commit: str
    previous_release: dict[str, Any] | None
    impact: str
    version: str
    tag: str
    title: str
    release_notes: str
    prerelease: bool
    artifacts: list[dict[str, Any]] = field(default_factory=list)
    provenance: str = "optional"
    policy: dict[str, Any] = field(default_factory=dict)
    policy_fingerprint: str = ""
    inspection_fingerprint: str = ""
    observed_state: str = "NEW"
    validation: list[dict[str, Any]] = field(default_factory=list)
    operations: list[str] = field(default_factory=list)
    plan_fingerprint: str = ""

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)

    def integrity_payload(self) -> dict[str, Any]:
        payload = self.to_dict()
        payload.pop("plan_fingerprint", None)
        return payload

    def compute_fingerprint(self) -> str:
        return fingerprint(self.integrity_payload())

    def seal(self) -> "ReleasePlan":
        self.plan_fingerprint = self.compute_fingerprint()
        return self

    def assert_integrity(self) -> None:
        if not self.plan_fingerprint:
            raise ReleaseError("release plan is not sealed; regenerate it with the plan command")
        if self.plan_fingerprint != self.compute_fingerprint():
            raise ReleaseError("release plan integrity check failed; plan was modified after dry-run")

    @classmethod
    def from_dict(cls, value: dict[str, Any]) -> "ReleasePlan":
        if value.get("schema_version") != 1:
            raise ReleaseError("unsupported release-plan schema_version")
        required = {"repository", "target_commit", "impact", "version", "tag", "title", "release_notes", "plan_fingerprint"}
        missing = sorted(required - set(value))
        if missing:
            raise ReleaseError("release plan missing fields: " + ", ".join(missing))
        fields = {name for name in cls.__dataclass_fields__}
        plan = cls(**{key: item for key, item in value.items() if key in fields})
        plan.assert_integrity()
        return plan

def load_plan(path: Path) -> ReleasePlan:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ReleaseError(f"invalid release plan JSON: {exc}") from exc
    if not isinstance(data, dict):
        raise ReleaseError("release plan must be a JSON object")
    return ReleasePlan.from_dict(data)

def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, indent=2, ensure_ascii=False, sort_keys=True) + "\n"
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent, delete=False) as handle:
        handle.write(text)
        temp = Path(handle.name)
    temp.replace(path)
