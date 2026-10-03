# Changelog — coferlandia-ci-adapter

## 1.4.0 — 2026-09-26

### Added

- Adds an optional `workflow-dispatch-exact-head` fallback to remote Development validation for open Draft PRs whose normal pull-request event path cannot produce fresh exact-head evidence.
- Binds fallback dispatch to a repository-declared trusted control ref, verifies the current Draft PR head before candidate checkout, and keeps candidate code read-only.

### Boundaries

- Existing PR-event submission remains preferred; fallback dispatch executes the same fingerprinted Development commands/gate and never becomes Qualification, merge, publication or deployment authority.
- Existing v1 contracts without a fallback remain valid and render unchanged.

### Compatibility

- Compatible additive extension of `DEVELOPMENT_VALIDATION v1`; repositories opt in by adding `github.submission.fallback`.


## 1.3.2 — 2026-09-13

### Changed

- Tightens remote Development adaptation guidance so a repository's canonical FULL/Qualification suite is not selected merely because it is the easiest existing command to execute remotely.
- Prefers repository-owned affected/scoped Development routing and requires unresolved scope to fail closed instead of silently escalating to FULL validation.

### Compatibility

- No Development schema or renderer behavior changes. Existing contracts remain valid; this patch strengthens adapter guidance and examples only.

## 1.3.1 — 2026-09-12

### Added

- Adds an authorized text-only retry command for GitHub-native publication recovery. Retry resolves the latest prior syntactically valid request authored by a current repository admin/maintainer on the same PR and replays it through the normal publisher path.

### Safety

- Retry comments cannot supply or override release payload fields; both the retry actor and the replayed request author must have release authority, malformed/unauthorized candidate requests are skipped, and merged-PR binding, exact target SHA validation, isolated control-plane execution and fail-closed publisher semantics remain unchanged.

## 1.3.0 — 2026-09-12

### Added

- Adds optional ordered repository-owned `github.setup` actions to remote Development validation so project-specific toolchains can be provisioned deterministically before Development commands.
- Validates static external action references and bounded flat scalar inputs, renders setup deterministically, and binds setup changes into the Development fingerprint.
- Adds Python/Node setup examples plus regression coverage for backwards-compatible contracts without setup and rejection of local/dynamic actions or ambiguous inputs.

### Boundaries

- Setup remains Development-only and never grants Qualification, merge, publication or deployment authority.
- Generic runners remain stack-agnostic; project runtime versions and approved setup actions remain repository-owned facts.

### Compatibility

- Existing `DEVELOPMENT_VALIDATION v1` contracts that omit `github.setup` remain valid without migration.

## 1.2.2 — 2026-09-12

### Fixed

- Separates the exact publication control-plane checkout from the immutable release-target checkout, so a patched publisher can safely resume a historical partial release without moving its tag or changing the target commit.

### Compatibility

- Existing `publication.github` policy remains unchanged; generated workflows still bind publication to the merged release PR SHA and use the repository-declared runner labels.

## 1.2.1 — 2026-09-12

### Changed

- Removes the generated GitHub-native publication workflow dependency on a preinstalled GitHub CLI by using Python standard-library GitHub REST calls while preserving runner, authority and exact-SHA controls.

## 1.2.0 — 2026-09-12

### Added

- Adds a separate repository-owned remote Development validation contract at `.coferlandia/development/validation.json`.
- Adds deterministic validation, fingerprinting and materialization of Draft-PR GitHub Actions Development workflows bound to the exact pull-request head SHA.
- Adds repository-owned runner labels, shell, working directory, commands, required services and environment-name declarations for Development validation.
- Adds bounded Chat Coder bootstrap support: when local Development checks cannot execute and no remote Development surface exists, Chat Coder may delegate only the missing Development adaptation to this skill.

### Boundaries

- `.coferlandia/ci/profile.json` remains Qualification-only.
- Remote Development validation is source-candidate evidence only and never emits `READY_FOR_MERGE`, performs Qualification, publishes, deploys or merges.
- Generated workflows do not inject secret values and grant candidate code only `contents: read` GitHub permission.

### Compatibility

- Existing repositories using only the CI profile or release publication adaptation remain compatible, including repository-configurable publication runners introduced in 1.1.1.

## 1.1.1 — 2026-09-12

### Changed

- Makes the GitHub-native publication runner repository-configurable while preserving ubuntu-latest as the compatible default and keeping publication separate from Qualification.

## 1.1.0 — 2026-09-12

### Added

- Optional GitHub-native release publication adaptation kept separate from `.coferlandia/ci/profile.json`.
- Deterministic rendering of `.coferlandia/release/policy.json` `publication.github` plus a Chat-compatible `issue_comment` publication workflow that delegates exact Commit -> Release mechanics to `coferlandia-release-publisher`.
- Publication-policy validation, safe repository-relative publisher paths, durable versioned request markers, exact release identity transport, actor authorization, merged-PR checks, and no deployment or LOCAL fallback.

### Compatibility

- Existing repositories using only the CI profile contract remain unchanged; publication adaptation is explicit and opt-in.

## 1.0.0 — 2026-09-07

### Added

- Repository-study workflow that produces one minimal CI profile shared by Chat GitHub-native CI and the generic Local CI skill.
- Deterministic profile validation, secret-field rejection, canonical SHA-256 fingerprinting, drift checking, dry-run and atomic rendering.
