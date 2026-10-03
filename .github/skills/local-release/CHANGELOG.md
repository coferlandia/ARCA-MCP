# Changelog — local-release

## 1.1.0 — 2026-09-21

### Changed

- Adds repository-declared frozen/materialized release candidates so the mutable source ref may continue advancing after release initialization without invalidating the active candidate.
- Keeps live-source repositories backward compatible: when no distinct candidate identity is declared, source-head movement still creates a new candidate and requires fresh review/Qualification.
- Separates ordinary source advancement from intentional candidate refresh after product repair while preserving fail-closed target/base, manifest, profile, gate, and review drift handling.

## 1.0.0 — 2026-09-11

### Added

- Adds a LOCAL release-candidate qualification surface with `READY_FOR_RELEASE` evidence and explicit separation from GitHub-native release qualification.
- Reuses `coferlandia-release-publisher` for formal version/tag/GitHub Release publication instead of duplicating Commit-to-Release mechanics.
