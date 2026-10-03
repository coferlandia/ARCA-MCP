# Changelog — local-ci

## 1.2.0 — 2026-09-25

### Changed

- Requires the durable READY_FOR_CI candidate to already be synchronized with the authoritative development base when the repository profile is base-sensitive.
- Fails fast with `DEVELOPMENT_CURRENTIZATION_REQUIRED` before expensive LOCAL qualification when the base moved, returning branch mutation/currentization to Development instead of performing it inside Qualification.

## 1.1.0 — 2026-09-10

### Changed

- Makes the Agent Skill invocation surface itself select the `LOCAL` Qualification strategy, removing any need for a separate strategy router after the skill is active.
- Clarifies that a bounded controller such as `chat-coder` reaching `READY_FOR_CI` does not implicitly invoke Local CI.
- Keeps GitHub-native Qualification exclusively outside this skill and preserves fail-closed `LOCAL_QUALIFICATION_BLOCKED` behavior with no remote fallback.

## 1.0.0 — 2026-09-07

### Added

- Generic local Qualification strategy that consumes repository-owned CI profiles and durable READY_FOR_CI evidence.
- Candidate/profile-bound READY_FOR_MERGE output compatible with the Chat GitHub-native CI controller, with explicit no-fallback and no-merge boundaries.
