---
name: local-release
description: >
  Use when one repository release must be initialized or resumed, reviewed, qualified through the LOCAL
  execution surface, integrated, and formally published when repository policy requires it. Invoking this
  skill selects LOCAL release qualification and never falls back to chat-release.
license: Apache-2.0
compatibility: >
  Requires read/write access to the target repository, git, Python 3.11+ for profile validation,
  every local runtime/service declared by the repository CI profile, and access required by the
  repository-approved release work-surface, integration, and publication mechanisms.
metadata:
  author: coferlandia
  version: "1.1.0"
  category: engineering
  status: active
  tested: "2026-09-21 - LOCAL release candidate initialization, optional frozen/materialized candidate identity, source-drift independence, aggregate review, READY_FOR_RELEASE, qualification boundaries, and publisher composition covered by contract tests."
---

## Context

`local-release` is the Agent Skills/local surface for one complete repository-approved release lifecycle whose Qualification strategy is LOCAL:

```text
explicit local-release invocation
-> resolve repository release policy/source/target
-> initialize or reuse release work surface
-> build/currentize release manifest
-> bind exact release candidate/base
-> aggregate release review
-> LOCAL release qualification
-> READY_FOR_RELEASE
-> repository-approved release integration
-> coferlandia-release-publisher when formal publication is required
-> COMPLETE
```

Invoking this skill selects `LOCAL` for release Qualification. It **never falls back to `chat-release`** and **must not invoke GitHub-native Qualification** as a substitute when local qualification fails or cannot run. Repository policy may still use GitHub for durable Issues/PRs/release integration/publication; that does not convert the Qualification strategy to GITHUB_NATIVE.

The target repository owns its release topology, source/target refs, release manifest, local qualification commands, integration method, publication requirements, reconciliation and deployment policy. This skill must not hardcode project branch names, workflow names, test commands, version numbers, runners or production topology.

This skill **must not deploy**. Deployment remains a separate repository-owned authority.

## Activation boundary

Activate when the user explicitly selects local release / `local-release`, or when an already-selected Agent Skills/local workflow delegates a repository release to this skill.

Do not activate merely because development reached `READY_FOR_MERGE`, because a release PR exists, or because a GitHub-native `chat-release` attempt is unavailable. Do not use this skill as an automatic fallback from `chat-release`.

Unless the controlling request is explicitly dry-run/planning-only, `local-release` owns repository-approved release work-surface initialization/reuse, LOCAL Qualification, release integration, and formal publication through the shared publisher when policy requires it. It never gains deployment authority.

## Release candidate initialization

Before review/Qualification, establish exactly one current release candidate through repository policy. Treat the release source ref as candidate discovery/input; after initialization, candidate identity is authoritative for the active release attempt.

1. Read current repository instructions and resolve authoritative release source/target refs, current source SHA, target/base SHA, release integration mechanism, manifest contract, reconciliation/ancestry/freeze policy, candidate identity mode, and publication policy.
2. Prove repository-declared pre-release reconciliation/ancestry/freeze conditions are satisfied and capture the exact source SHA as the source snapshot.
3. Resolve the repository-declared candidate identity mode:
   - **Materialized/frozen candidate:** initialize or reuse a stable candidate ref, branch, work-surface head, or equivalent identity at the selected source snapshot SHA. Later normal advancement of the source ref must not move or invalidate that candidate.
   - **Live-source candidate:** the source ref itself remains candidate authority; later source-head movement creates a new candidate and invalidates candidate-bound evidence.
4. If repository policy uses a GitHub pull request as the release work surface:
   - search for an existing open release PR matching the target and repository-declared active candidate identity;
   - for a materialized/frozen candidate, require the PR head to be the stable candidate identity rather than the mutable source ref;
   - reuse it only when it unambiguously represents the same release;
   - when none exists and policy permits controller creation, initialize the approved candidate identity first and then create the repository-approved release PR (Draft when the repository keeps release qualification/review pre-integration);
   - stop rather than creating a competing release if an ambiguous/conflicting active release exists.
5. If repository policy uses another work surface, initialize/reuse only that declared mechanism.
6. Build or currentize the repository-defined release manifest from authoritative source-snapshot-vs-target delta evidence, including the work identities and release-impact information required by repository policy.
7. Persist the manifest only in the repository-defined durable location and bind it to source snapshot SHA, exact candidate SHA, candidate ref/work-surface identity when applicable, and target/base identity.
8. Re-read the initialized work surface and candidate identity before review.

For a materialized/frozen candidate, source-ref advancement after initialization is ordinary concurrent development and does not stale review/Qualification/READY_FOR_RELEASE evidence. Candidate movement, explicit candidate refresh after repair, target/base movement, manifest/profile/gate drift, or review-state change still invalidates affected evidence.

For live-source mode, source-head movement remains candidate drift and requires candidate/manifest/review/Qualification refresh.

Release initialization/control-plane operations do not authorize product-code corrections.

## Aggregate release review

Before LOCAL qualification, perform or consume the repository-approved aggregate review of the complete release delta and current manifest. Review the combined candidate rather than trusting previously integrated work items independently.

Cover relevant cross-change interactions, integration/history correctness, public/API/data-contract compatibility, migrations, environment/configuration effects, manifest completeness, operational impact, and repository-declared release risks.

The exact-candidate release review must end with:

```text
Review Critical: 0
Review Important: 0
```

Release-owned metadata/manifest findings may be corrected within this skill followed by a fresh aggregate review. Product/development findings return `RELEASE_REPAIR_REQUIRED`; do not patch product code inside `local-release` unless repository policy explicitly defines that as the normal development path. A candidate/base/manifest identity change invalidates the prior review; ordinary source-ref advancement in frozen/materialized mode does not.

## Qualification preconditions

Before LOCAL qualification:

1. Prove the initialized source snapshot, candidate ref/work-surface identity when applicable, target ref, current candidate SHA and target/base SHA remain authoritative. In frozen/materialized mode, the mutable source ref is allowed to advance independently.
2. Prove the durable release manifest belongs to that exact candidate/base identity.
3. Prove any repository-required reconciliation/ancestry/freeze/active-release precondition remains satisfied.
4. Require `.coferlandia/ci/profile.json`, validate it against the central profile contract, and compute the current profile fingerprint.
5. Resolve the profile/repository-declared LOCAL release qualification commands and required local services/environment.
6. Require the current aggregate release review to remain `Review Critical: 0` and `Review Important: 0` for the exact candidate/base/manifest.
7. Confirm there is no conflicting release authority or stale work-surface state.

Missing, ambiguous or stale release identity, profile authority, review state, or local qualification contract blocks qualification.

## LOCAL qualification

1. Keep the exact release candidate unchanged while qualification runs.
2. Execute only repository-declared local release/FULL qualification commands, in declared order and working directory.
3. Record exact candidate SHA, qualified target/base SHA, profile fingerprint, release manifest identity, review state and command/result evidence.
4. Current failures are RED. Missing tools/services are blocked, not GREEN.
5. A material product correction is not made directly inside the release candidate unless repository policy explicitly defines that as its normal development path.
6. When correction must return through ordinary development, report:

```text
RELEASE_REPAIR_REQUIRED
Reason: <defect or violated contract>
Candidate SHA: <stale release candidate>
Required repair path: <repository-defined development path>
```

7. A product repair requires an explicit new candidate initialization/refresh from the corrected source state and invalidates old local release review/qualification evidence. Do not silently move a frozen candidate because the source ref advanced.
8. Re-read candidate/target/work-surface/profile/manifest/review identity after all local commands pass and require review state to remain Critical = 0 / Important = 0. Source-ref advancement alone is not drift in frozen/materialized mode.

## READY_FOR_RELEASE

Only after the exact release candidate is fully GREEN under the repository's LOCAL release contract and current aggregate release review remains clean, create/update durable evidence matching `_protocol/delivery/READY_FOR_RELEASE.md`:

```text
State: READY_FOR_RELEASE
Source ref: <source>
Source snapshot SHA: <source SHA selected when this candidate was initialized>
Candidate ref: <stable/materialized candidate ref or NONE for live-source mode>
Target ref: <target>
Release candidate SHA: <exact candidate>
Qualified base SHA: <exact target/base used>
Effective candidate: <repository-defined exact candidate>
Qualification strategy: LOCAL
CI profile fingerprint: <sha256>
Qualification evidence: <ordered local command/result evidence>
Included work: <durable release manifest/reference>
Review Critical: 0
Review Important: 0
Next stage: Release integration
```

Any later candidate SHA/ref, target/base, effective candidate, work-surface, profile, manifest or required-review change makes the handoff stale. In live-source mode a source-head change is a candidate change; in frozen/materialized mode source-ref advancement alone is not stale.

## Release integration

After `READY_FOR_RELEASE`, integrate only through the repository-approved release mechanism and re-read identity immediately before the consequential operation. Preserve repository-required history/genealogy and safeguards. Never force-push or weaken policy to complete a release.

If candidate/base/work-surface/profile/manifest/review state changed before integration, stop and return `RELEASE_REQUALIFICATION_REQUIRED` rather than integrating stale evidence. Do not compare a frozen candidate to the mutable source ref as an integration precondition unless repository policy explicitly requires that reconciliation check.

After integration, resolve the exact resulting target commit and verify repository-required release closeout/reconciliation that belongs to this release lifecycle.

## Formal publication

When repository policy requires a formal version/tag/GitHub Release, invoke `coferlandia-release-publisher` on the exact integrated commit. Do not duplicate SemVer classification, annotated-tag creation, GitHub Release publication, idempotent recovery or publication verification.

If publication requires an explicit semantic-version decision not already established by repository policy, preserve the publisher's authority boundary rather than inventing one.

## Failure boundaries

- Candidate initialization/work-surface conflict: `LOCAL_RELEASE_BLOCKED`.
- Missing/invalid CI profile or stale profile fingerprint: `LOCAL_RELEASE_BLOCKED` or `RELEASE_REQUALIFICATION_REQUIRED` depending on when drift occurred.
- Missing/unclean aggregate review state: `LOCAL_RELEASE_BLOCKED` before qualification or `RELEASE_REQUALIFICATION_REQUIRED` after evidence was emitted.
- Missing LOCAL runtime/service: `LOCAL_RELEASE_BLOCKED`; never fall back to `chat-release`.
- Product/test/review defect: `RELEASE_REPAIR_REQUIRED`; return through repository-defined development/correction flow.
- Candidate/base/work-surface/profile/manifest/review drift: `RELEASE_REQUALIFICATION_REQUIRED`.
- Integration policy unavailable or conflicting: `LOCAL_RELEASE_BLOCKED`.
- Formal publication failure: preserve the exact integrated commit and use publisher recovery/verification semantics; do not create a synthetic replacement commit.
- Deployment request: outside this skill; this skill must not deploy.

## Expected output

```text
Release workflow = COMPLETE | LOCAL_RELEASE_BLOCKED | RELEASE_REPAIR_REQUIRED | RELEASE_REQUALIFICATION_REQUIRED
Qualification strategy = LOCAL
Source ref = <ref>
Source snapshot SHA = <sha>
Candidate ref = <ref or NONE>
Target ref = <ref>
Release candidate SHA = <sha>
Qualified base SHA = <sha>
CI profile fingerprint = <sha256>
Release manifest = <durable reference>
Review Critical = 0
Review Important = 0
Qualification evidence = <local commands/results>
Integrated target SHA = <sha or PENDING>
Formal release = <release identity or NONE/PENDING per repository policy>
Deployment = NOT OWNED
```

## Output location

Use the repository's durable release evidence surfaces. `READY_FOR_RELEASE` follows the shared protocol marker. Test/build artifacts remain owned by repository commands. This skill creates no shadow release configuration.
