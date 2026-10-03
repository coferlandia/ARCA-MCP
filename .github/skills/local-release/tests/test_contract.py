import unittest
from pathlib import Path


SKILL_ROOT = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[4]


class LocalReleaseContractTests(unittest.TestCase):
    def test_local_release_owns_local_release_surface(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("name: local-release", text)
        self.assertIn("READY_FOR_RELEASE", text)
        self.assertIn("Qualification strategy: LOCAL", text)
        self.assertIn("coferlandia-release-publisher", text)
        self.assertIn("exact release candidate", text)
        self.assertIn("release manifest", text)
        self.assertIn("RELEASE_REPAIR_REQUIRED", text)
        self.assertIn("must not deploy", text)

    def test_local_release_never_falls_back_to_github_native_qualification(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("never falls back to `chat-release`", text)
        self.assertIn("must not invoke GitHub-native Qualification", text)
        self.assertIn("does not convert the Qualification strategy to GITHUB_NATIVE", text)

    def test_local_release_initializes_or_reuses_release_candidate(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("## Release candidate initialization", text)
        self.assertIn("search for an existing open release PR", text)
        self.assertIn("create the repository-approved release PR", text)
        self.assertIn("ambiguous/conflicting active release", text)
        self.assertIn("Build or currentize the repository-defined release manifest", text)
        self.assertIn("Materialized/frozen candidate", text)
        self.assertIn("Live-source candidate", text)
        self.assertIn("source-ref advancement after initialization is ordinary concurrent development", text)
        self.assertIn("source snapshot SHA", text)

    def test_frozen_candidate_does_not_follow_mutable_source_ref(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("Source snapshot SHA:", text)
        self.assertIn("Candidate ref:", text)
        self.assertIn("mutable source ref is allowed to advance independently", text)
        self.assertIn("Do not silently move a frozen candidate", text)
        self.assertIn("source-ref advancement alone is not stale", text)

    def test_local_release_requires_profile_and_aggregate_review_authority(self):
        text = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("## Aggregate release review", text)
        self.assertIn("combined candidate", text)
        self.assertIn("Require `.coferlandia/ci/profile.json`", text)
        self.assertIn("compute the current profile fingerprint", text)
        self.assertIn("Review Critical: 0", text)
        self.assertIn("Review Important: 0", text)
        self.assertIn("required-review change makes the handoff stale", text)
        self.assertIn("Candidate/base/work-surface/profile/manifest/review drift", text)

    def test_skill_is_listed_in_public_index(self):
        index = (ROOT / "skills" / "INDEX.md").read_text(encoding="utf-8")
        self.assertIn("[local-release](./engineering/local-release/)", index)


if __name__ == "__main__":
    unittest.main()
