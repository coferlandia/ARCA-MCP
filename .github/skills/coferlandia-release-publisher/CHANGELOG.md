# Changelog — coferlandia-release-publisher

## 1.1.4 — 2026-09-13

### Fixed

- Fails closed when more than one GitHub Release object resolves to the same tag, including the case where the direct release-by-tag endpoint returns a published release while the collection also contains a conflicting draft.
- Verifies release asset and provenance digests by downloading asset bytes when GitHub omits digest metadata, avoiding dependence on a specific REST response shape.

### Compatibility

- Exact tag, commit, title, notes, prerelease and provenance identity semantics are unchanged. Matching partial releases remain idempotently resumable.

## 1.1.3 — 2026-09-12

### Fixed

- Resolves matching draft GitHub Releases by tag through the paginated release collection when GitHub's release-by-tag endpoint omits drafts, allowing publication to resume safely from an existing annotated tag plus draft without recreating either identity.

### Compatibility

- Publication remains fail-closed and idempotent: matching partial state resumes, while conflicting tag/release identity is still rejected.

## 1.1.2 — 2026-09-12

### Changed

- Replaces GitHub CLI subprocess calls with a Python standard-library GitHub REST transport using GITHUB_TOKEN or GH_TOKEN without changing fail-closed release semantics.

## 1.1.1 — 2026-09-12

### Changed

- Accepts and validates repository publication runner metadata without changing exact Commit-to-Release semantics.

## 1.1 — 2026-09-12

### Added

- Optional validation for repository-declared `publication.github` transport metadata (`issue-comment`, `workflow-dispatch`, or `none`) while preserving repository-owned sibling publication fields.
- Explicit non-interactive GitHub Actions compatibility so a repository workflow can transport already-resolved release identity into the deterministic publisher without duplicating tag/Release mechanics.

### Compatibility

- Existing release policies remain compatible when `publication.github` is absent.
- Commit -> annotated tag -> draft GitHub Release -> verification -> publication semantics are unchanged.

## 1.0 — 2026-09-05

### Added

- Adds a generic Commit -> Release workflow backed by a deterministic JSON CLI for SemVer planning, exact annotated-tag identity, GitHub Release publication, idempotent recovery, optional artifacts/provenance, consistency verification, and machine-readable release resolution without deployment coupling.
