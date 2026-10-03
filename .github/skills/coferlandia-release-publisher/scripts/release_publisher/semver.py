from __future__ import annotations
import re
from dataclasses import dataclass
from functools import total_ordering

_RE = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
    r"(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)

@total_ordering
@dataclass(frozen=True)
class SemVer:
    major: int
    minor: int
    patch: int
    prerelease: tuple[str, ...] = ()
    build: tuple[str, ...] = ()

    @classmethod
    def parse(cls, value: str) -> "SemVer":
        match = _RE.fullmatch(value)
        if not match:
            raise ValueError(f"invalid semantic version: {value}")
        pre = tuple(match.group(4).split(".")) if match.group(4) else ()
        for item in pre:
            if item.isdigit() and len(item) > 1 and item.startswith("0"):
                raise ValueError(f"invalid numeric prerelease identifier: {item}")
        build = tuple(match.group(5).split(".")) if match.group(5) else ()
        return cls(int(match.group(1)), int(match.group(2)), int(match.group(3)), pre, build)

    def __str__(self) -> str:
        result = f"{self.major}.{self.minor}.{self.patch}"
        if self.prerelease:
            result += "-" + ".".join(self.prerelease)
        if self.build:
            result += "+" + ".".join(self.build)
        return result

    @property
    def core(self) -> tuple[int, int, int]:
        return (self.major, self.minor, self.patch)

    def _compare_pre(self, other: "SemVer") -> int:
        if not self.prerelease and not other.prerelease:
            return 0
        if not self.prerelease:
            return 1
        if not other.prerelease:
            return -1
        for left, right in zip(self.prerelease, other.prerelease):
            if left == right:
                continue
            left_numeric, right_numeric = left.isdigit(), right.isdigit()
            if left_numeric and right_numeric:
                return -1 if int(left) < int(right) else 1
            if left_numeric != right_numeric:
                return -1 if left_numeric else 1
            return -1 if left < right else 1
        return (len(self.prerelease) > len(other.prerelease)) - (len(self.prerelease) < len(other.prerelease))

    def __lt__(self, other: object) -> bool:
        if not isinstance(other, SemVer):
            return NotImplemented
        if self.core != other.core:
            return self.core < other.core
        return self._compare_pre(other) < 0

    def __eq__(self, other: object) -> bool:
        return isinstance(other, SemVer) and self.core == other.core and self.prerelease == other.prerelease

def bump_version(previous: str | None, impact: str) -> str:
    if previous is None:
        raise ValueError("first release requires an explicit version")
    current = SemVer.parse(previous)
    if impact == "patch":
        result = (current.major, current.minor, current.patch + 1)
    elif impact == "minor":
        result = (current.major, current.minor + 1, 0)
    elif impact == "major":
        result = (current.major + 1, 0, 0)
    else:
        raise ValueError(f"unsupported semantic impact: {impact}")
    return ".".join(str(value) for value in result)

def validate_requested_version(previous: str | None, requested: str, impact: str) -> None:
    candidate = SemVer.parse(requested)
    if previous is None:
        return
    old = SemVer.parse(previous)
    if candidate <= old:
        raise ValueError(f"requested version {requested} must be newer than {previous}")

    # Once a prerelease line already encodes the next public core version, advancing
    # within that same core (rc.1 -> rc.2 or rc.1 -> stable) is a promotion, not a
    # fresh PATCH/MINOR/MAJOR bump from the prerelease identifier.
    if old.prerelease and candidate.core == old.core:
        return

    minimum = SemVer.parse(bump_version(previous, impact))
    if candidate.core < minimum.core:
        raise ValueError(f"requested version {requested} understates {impact} impact; minimum core is {minimum}")
