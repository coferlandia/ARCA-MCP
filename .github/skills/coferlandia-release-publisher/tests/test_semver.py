from __future__ import annotations

import sys
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_publisher.semver import SemVer, bump_version, validate_requested_version


class SemVerTests(unittest.TestCase):
    def test_parse_and_compare_stable_and_prerelease(self) -> None:
        self.assertEqual(str(SemVer.parse("1.7.0")), "1.7.0")
        self.assertLess(SemVer.parse("1.7.0-rc.1"), SemVer.parse("1.7.0"))
        self.assertLess(SemVer.parse("1.7.0-rc.1"), SemVer.parse("1.7.0-rc.2"))

    def test_bump_stable_versions(self) -> None:
        self.assertEqual(bump_version("1.4.2", "patch"), "1.4.3")
        self.assertEqual(bump_version("1.4.2", "minor"), "1.5.0")
        self.assertEqual(bump_version("1.4.2", "major"), "2.0.0")

    def test_rejects_invalid_semver(self) -> None:
        for value in ("v1.2.3", "2026.09", "1.2", "1.02.3"):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    SemVer.parse(value)

    def test_requested_version_cannot_understate_impact(self) -> None:
        validate_requested_version("1.6.3", "1.7.0", "minor")
        with self.assertRaises(ValueError):
            validate_requested_version("1.6.3", "1.6.4", "minor")
        with self.assertRaises(ValueError):
            validate_requested_version("1.6.3", "1.7.0", "major")

    def test_prerelease_line_can_advance_and_promote_without_extra_core_bump(self) -> None:
        validate_requested_version("1.7.0-rc.1", "1.7.0-rc.2", "minor")
        validate_requested_version("1.7.0-rc.1", "1.7.0", "minor")
        with self.assertRaises(ValueError):
            validate_requested_version("1.7.0-rc.2", "1.7.0-rc.1", "minor")

    def test_new_minor_prerelease_from_stable_is_valid(self) -> None:
        validate_requested_version("1.6.3", "1.7.0-rc.1", "minor")

    def test_first_release_requires_explicit_version(self) -> None:
        with self.assertRaises(ValueError):
            bump_version(None, "minor")


if __name__ == "__main__":
    unittest.main()
