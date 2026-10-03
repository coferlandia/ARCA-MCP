---
name: coferlandia-release-publisher
description: >
  Use when an exact Git commit must become, dry-run, verify, or resolve as a formal product release:
  annotated version tag plus GitHub Release, including historical commits, prereleases, hotfix lines,
  release notes, artifacts, provenance, or tag/release inconsistency checks. Not for deployment,
  production rollback, service restart, or ordinary internal version-file changes.
license: Apache-2.0
compatibility: >
  Requires Python 3.11+, git, GitHub token authorization for the target repository, and publication
  permissions for `publish`. Read-only planning/verification needs only corresponding read access.
  Optional signed tags require a usable local Git signing configuration. The CLI is non-interactive
  and may run inside a repository-declared GitHub Actions publication workflow.
metadata:
  author: coferlandia
  version: "1.1.4"
  category: ops
  status: active
  tested: "2026-09-12 - activation boundaries, SemVer/policy logic, Git identity, GitHub REST adapter, draft-resume idempotency, duplicate release identity rejection, asset digest fallback verification, CLI contracts, optional GitHub-native publication transport policy, and configurable runner metadata covered by repository CI tests."
---

## Context

This skill owns exactly **Commit -> Release**. A release is a formal, externally observable identity
for one exact existing Git commit. The ordinary Coferlandia authority model is:

```text
exact commit SHA
  -> annotated Git tag (normally vMAJOR.MINOR.PATCH)
  -> GitHub Release referencing that existing tag
  -> optional release artifacts/provenance
```

The target may be `HEAD` or an older valid commit. Never modify the target commit or create a
synthetic commit merely to declare it released.

Semantic judgment belongs to the agent; deterministic identity, lineage, validation, publication,
and verification belong to `scripts/coferlandia-release.py`. Read `references/release-model.md`
before choosing a target/previous release, `references/policy-contract.md` when local policy exists
or is needed, and `references/consistency-state-machine.md` before recovering a partial/inconsistent
publication.

The CLI is deliberately non-interactive. A repository may expose it through a policy-declared
GitHub Actions transport for `chat-release`; that workflow transports already resolved release
identity into this publisher and does not become a second owner of tag/Release mechanics.

## Activation and authority

Use this skill for requests such as:

- create/publish a release from a specific commit or `HEAD`;
- dry-run a prospective release;
- release an older commit intentionally;
- create a SemVer prerelease or hotfix on an allowed maintenance line;
- verify a published release or resolve `version -> tag -> exact SHA` for automation;
- diagnose tag/GitHub Release/provenance inconsistency.

Do not activate merely because a PR merged, a commit exists, or an internal version file changed.
Do not deploy after publication unless a separate deployment authority is explicitly invoked.

Creating a tag or GitHub Release is a publication side effect. `inspect`, `plan`, `verify`, and
`resolve` are read-only with respect to remote publication. Run `publish` only when the current
request/control authority explicitly authorizes publishing that release.

## Repository-local publication owner

Before applying the generic workflow, inspect repository instructions and repository-local skills,
especially `.agents/skills/*release*/SKILL.md`.

Delegate and stop this generic workflow only when a stronger local contract **explicitly owns
Commit -> published Release**, including the final tag/GitHub Release operation. A preparation-only
or pre-merge release/versioning gate may compose before this publisher and does not suppress it.
Never execute two competing publication protocols.

The repository-local `coferlandia-release-maintainer` in `coferlandia-skills` is an example of a
preparation/final-delivery gate: it explicitly does not create tags or GitHub Releases, so it is not
an alternative generic publication owner.

## Workflow

### 1. Inspect factual release state

Run from the target repository:

```bash
python <installed-skill>/scripts/coferlandia-release.py inspect --target <revision>
```

Use `--repository owner/name` when the GitHub repository cannot be inferred from `origin`, and
`--policy <path>` when the repository supplies an explicit machine policy.

The deterministic inspection must establish:

- exact target SHA and allowed release refs;
- target reachability without checkout/reset;
- published GitHub Releases and annotated tags on the target lineage;
- nearest valid previous release ancestor;
- `previous..target` commits and changed paths;
- configured GitHub check evidence;
- immutable-release capability when observable/required;
- existing tag/release state and ambiguities.

Stop on unresolved ancestry, release-line, version-scheme, or identity ambiguity. Do not guess from
publication dates alone.

### 2. Classify semantic impact from evidence

Study the actual behavior/compatibility delta between the previous release and target. Use commits,
PRs, Issues, migrations, relevant documentation, tests, and public/operator contracts as evidence.
Do not infer PATCH/MINOR/MAJOR blindly from filenames, commit prefixes, or Conventional Commits.

Generic SemVer meaning:

- `PATCH`: compatible correction;
- `MINOR`: additive compatible capability;
- `MAJOR`: incompatible contract or migration requiring consumer adaptation.

The CLI validates version mechanics; the agent owns the semantic classification. A requested
version may be larger than the minimum impact, but may not understate it.

For the first release, do not invent maturity or automatically choose `1.0.0`. Recommend an
explicit initial SemVer from known maturity/local policy; unresolved maturity remains a control
choice.

If published release history already uses a non-SemVer scheme, do not silently introduce SemVer.
Follow a stronger local publication contract or report that the generic v1 scheme is unsupported.

### 3. Write meaningful release title and notes

Summarize changes for consumers/operators, not as a copied commit log. Preserve useful PR/Issue
references when evidence supports them. Include only applicable sections among:

```text
Summary
Features
Fixes
Breaking changes
Migration notes
Operational notes
Known issues
```

Do not invent CI, migration, compatibility, artifact, or known-issue claims.

### 4. Produce a deterministic dry-run plan

Run `plan` with the semantic decisions:

```bash
python <installed-skill>/scripts/coferlandia-release.py plan \
  --target <revision> \
  --impact <patch|minor|major> \
  --version <X.Y.Z[-prerelease]> \
  --title "<title>" \
  --notes-file <release-notes.md> \
  --output .agent/release-publisher/release-plan.json
```

Add `--prerelease` when the SemVer has a prerelease identifier. Add `--artifact <path>` only for
already-produced release artifacts. The generic publisher does not run arbitrary build/deployment
commands.

Review the JSON plan before publication. It contains target/previous release, impact, tag, notes,
artifacts/digests, validation evidence, observed consistency state, policy fingerprint, stale-plan
fingerprint, and the exact ordered publication operations.

Dry-run may refresh/read refs and GitHub state; it creates no remote tag, Release, or asset.

### 5. Publish exactly the reviewed plan

When publication is authorized:

```bash
python <installed-skill>/scripts/coferlandia-release.py publish \
  --input .agent/release-publisher/release-plan.json
```

This command is non-interactive and is the same publication primitive used by an approved
GitHub-native workflow transport. The transport must supply an exact reviewed plan/identity and
must not reimplement tag or GitHub Release operations itself.

The CLI re-reads authoritative state before the first remote mutation. It fails if the plan is stale,
the target is no longer eligible, required checks are missing/red, required immutable releases
cannot be proven enabled, or the version/tag identity became incompatible.

Publication order is deliberately fail-closed:

```text
revalidate
-> create annotated tag at exact SHA
-> push exact tag without force
-> verify remote annotated tag peels to exact SHA
-> create GitHub Release as draft referencing existing tag
-> upload/verify declared assets and optional provenance
-> publish draft
-> re-read and verify final release
```

### 6. Verify and resolve

Use:

```bash
python <installed-skill>/scripts/coferlandia-release.py verify --tag v1.7.0
python <installed-skill>/scripts/coferlandia-release.py resolve --tag v1.7.0
```

`verify` reports coherence. `resolve` returns the normalized machine contract for downstream
systems such as VM Admin/CI/CD. It fails closed rather than returning an ambiguous version-to-SHA
mapping.

## Prereleases, hotfixes, and historical commits

SemVer prereleases such as `1.7.0-rc.1` are supported and must be published with GitHub
`prerelease=true`. A stable promotion may intentionally reuse the same commit only when the new
version relationship is valid and its tag is a distinct immutable identity.

The default release ref is the GitHub repository's default branch, never hardcoded `main`.
Maintenance/hotfix lines are declared through `release_refs` in local policy. A target must be
reachable from at least one allowed release ref, and its previous release must be an ancestor on the
same historical line.

Working-tree dirtiness is not a generic blocker because the release identity is an existing commit
SHA. Declared artifact files are separately hashed and revalidated before upload.

## Idempotency and inconsistency

A second execution over a fully matching published release succeeds as `already_consistent`.
Correct partial states (tag only or matching draft) may resume. Read
`references/consistency-state-machine.md` for exact transitions.

Never automatically:

- move/rewrite a published tag;
- use `git tag -f` or force-push a tag;
- delete/recreate a published GitHub Release to hide drift;
- replace a conflicting published asset;
- claim validation or immutability evidence that was not observed.

Any tag/release/SHA/provenance conflict is `INCONSISTENT` and requires explicit investigation, not
silent repair.

## Release metadata versus artifacts versus deployment

Keep these concepts separate:

- **release metadata**: version, tag, SHA, timestamps, notes, validation/provenance;
- **release artifacts**: optional immutable outputs belonging to the release (binary/package/SBOM,
  checksums, image digest reference, manifest, etc.);
- **deployment artifacts/actions**: host-specific material or operations used to install/run a
  release.

The generic publisher may attach declared release artifacts. It does not access servers, select a
VM, run Docker Compose, restart services, execute production migrations, deploy, roll back
production, or decide which release is currently deployed. Do not assume Silku, SecretarIA,
Oracle Cloud, Coferlandia VM Admin, Docker, or any infrastructure format.

## Output Location

Transient plans and generated provenance go under:

```text
.agent/release-publisher/
```

They are not release authority and never need to exist in the target commit. Published authority is
the annotated tag plus GitHub Release; optional provenance is a Release asset.

## Expected Output

Human result:

```text
Release: v1.7.0
Commit: 913dbe7...
Previous: v1.6.3
Tag: v1.7.0 / annotated
GitHub Release: published
Release notes: published
Artifacts: verified
Consistency: PASS
```

`resolve` emits JSON containing at least repository, version, tag, exact commit, creation/publication
timestamps, title, release notes, prerelease flag, previous release, artifacts/provenance, and
`consistency: pass`. Unknown/unobservable properties remain `null`/explicitly unknown; never invent
them.

## Scripts Available

- `scripts/coferlandia-release.py` — deterministic JSON CLI with `doctor`, `inspect`, `plan`,
  `publish`, `verify`, `resolve`, `version`, and `capabilities`.

## Gotchas

- **Treating the newest release by date as the previous release:** wrong. Previous release is the
  nearest valid annotated-release ancestor of the target on the permitted line.
- **Letting GitHub create the tag implicitly:** wrong. Create and verify the annotated Git tag first;
  the GitHub Release references that existing identity.
- **Assuming local `main`:** wrong. Default branch comes from GitHub; maintenance refs come from
  policy.
- **Using a local preparation skill as a competing publisher:** wrong. Delegate only when it owns
  final Commit -> published Release.
- **Using release publication as deployment:** prohibited. Publication ends at verified release
  state; deployment is a separate system/skill.
