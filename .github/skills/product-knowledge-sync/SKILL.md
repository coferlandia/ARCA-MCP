---
name: product-knowledge-sync
description: >
  Use when product behavior, capabilities, workflows, or releases must be reconciled with end-user
  Help, admin/operator guidance, support knowledge, developer documentation, commercial material,
  or release notes; when documentation drift must be audited; or when a release needs explicit
  product-knowledge impact verification. Do not use for generic copywriting, developer onboarding
  alone, Archivist-only durable knowledge cleanup, or ordinary code review without product-doc impact.
license: Apache-2.0
compatibility: >
  Requires Python 3.11+ and read access to the target repository and configured knowledge surfaces.
  GitHub and project-documentation-archivist are optional evidence sources. The deterministic CLI
  uses only the Python standard library and never mutates Git, GitHub, or documentation.
metadata:
  author: coferlandia
  version: "1.0"
  category: content
  status: active
  tested: "2026-10-01 - activation, contract, profile/report validation, fingerprint, verification, stale-evidence, and boundary tests."
---

## Context

`product-knowledge-sync` detects and prevents drift between implemented product behavior and the
surfaces that explain that behavior to users, operators, support, developers, and commercial teams.
It owns reconciliation and coverage state, not authorship authority for every destination.

The repository, tests, Issues/PRs/releases, existing canonical capability/configuration contracts,
and current documentation remain authoritative evidence. This skill never creates a shadow product
catalog or turns its profile/reports into runtime product truth.

Semantic discovery is agentic. Deterministic enforcement is mechanical:

- the agent decides whether behavior is user-facing, what changed, which surfaces are affected, and
  whether prose is semantically complete;
- the CLI validates profile/report structure, surface coverage, dispositions, repository paths,
  fingerprints, aggregation, and candidate/profile staleness;
- the CLI must never infer product meaning from filenames, labels, commit prefixes, or regexes.

## Prerequisites

1. Resolve the target project root and current product/release subject.
2. Read `.coferlandia/product-knowledge/profile.json` when present.
3. If no profile exists, `impact` and `audit` may still run with explicitly supplied run surfaces,
   but they do not establish a persistent release gate. `verify` may be diagnostic only unless the
   target repository has opted into Product Knowledge verification through repository policy.
4. Inspect repository/GitHub evidence needed to reconstruct actual behavior. Do not trust existing
   documentation as ground truth merely because it exists.
5. When Archivist canonical files exist, read `references/archivist-integration.md` before using
   them as evidence.

## Modes

### 1. `impact` — one bounded change

Use for one Issue, PR, feature, bug fix, refactor with user-visible consequences, or equivalent
bounded change.

1. Identify authoritative current/change evidence.
2. Decide `user_facing: true|false` and record a rationale.
3. Enumerate affected behaviors/capabilities with evidence.
4. Evaluate every configured/run surface independently.
5. Give each surface exactly one terminal disposition:
   `updated`, `verified-no-change`, `not-applicable`, `manual-review-required`, or `blocked`.
6. For `verified-no-change` and `not-applicable`, record a reason. For `updated`, record evidence
   references when available. Never use a feature-name mention as proof of semantic completeness.
7. Emit handoffs when another owner must act.
8. Validate the report with:

```bash
python skills/content/product-knowledge-sync/scripts/product-knowledge-sync-cli.py \
  impact validate --input <impact.json> --project-root <project-root> --json
```

Read `references/impact-contract.md` when creating or validating an impact report.

### 2. `audit` — historical/current drift discovery

Use for a release range, milestone, product area, or whole repository.

1. Reconstruct actual behavior from implementation, tests, releases, Issues/PRs, and durable evidence.
2. Compare that behavior to each knowledge surface.
3. Evaluate semantic dimensions where relevant: purpose, setup/configuration, normal usage,
   roles/permissions, restrictions/preconditions, adjacent-feature interactions, failure behavior,
   troubleshooting/recovery, material time/location/data semantics, and migration/deprecation notes.
4. Classify each configured surface as `covered`, `partial`, `missing`, `stale`, `contradictory`,
   `not-applicable`, or `manual-review-required` with evidence/reason.
5. Emit owner handoffs rather than rewriting surfaces outside this skill's authority.
6. Validate the report with the CLI.

Read `references/audit-workflow.md` and `references/surface-semantics.md` before a broad audit.

### 3. `verify` — pre-release anti-drift gate

Use before release qualification/publication or for an explicit diagnostic verification.

1. Assemble the exact impact/audit reports that apply to the candidate.
2. Require every included report used for release verification to bind to the exact candidate.
3. Run:

```bash
python skills/content/product-knowledge-sync/scripts/product-knowledge-sync-cli.py \
  verify --project-root <project-root> --candidate <identity> \
  --input <report.json> [--input <report.json> ...] --json
```

4. Treat exit `0` as deterministic PASS, `3` as semantic BLOCKED, `4` as stale/mismatched evidence,
   and `2` as invalid input/profile. Do not convert stale evidence into a semantic result.
5. Release-blocking authority exists only when the repository explicitly opts in through its
   Product Knowledge profile/qualification policy. Installing this skill alone never adds a gate.

Read `references/verification-gate.md` before release verification.

## Ownership and handoffs

- `project-documentation-archivist` owns durable project knowledge. Product Knowledge Sync may read
  its canonical files and emit `durable-knowledge-gap` handoffs, but never initializes or rewrites
  Archivist solely for bookkeeping.
- `project-evangelist` owns progressive developer-facing explanatory docs when explicitly invoked.
  Product Knowledge Sync owns configured coverage state and may hand developer-doc gaps to it.
- `software-development` may carry an explicit `Product Knowledge Impact` reference as implementation
  scope; this skill remains the owner of product-knowledge reconciliation semantics.
- commercial/external surfaces remain first-class even when the agent cannot edit them. Record
  `manual-review-required`; never silently omit them.
- release/CI controllers remain separate. Consumer repositories opt in by adding the deterministic
  verification command to their own repository-owned qualification contract.

Read `references/cross-skill-boundaries.md` when a workflow crosses those owners.

## Product Knowledge Impact handoff

A planning/development contract may carry:

```md
## Product Knowledge Impact

Status: required | not-required | already-resolved
Reference: <impact report/contract or none>
Expected surfaces: <surface IDs or defer-to-product-knowledge-sync>
```

Analyst preserves it, implementation treats declared required documentation work as in-scope, and
review verifies the declaration. Absence of this section and absence of an opted-in profile preserve
existing workflows.

## Output Location

Execution artifacts go under:

```text
.agent/product-knowledge/<run-id>/
```

Typical files are `impact.json`, `audit.json`, `verification.json`, and `summary.md`. They are
transient execution evidence, not canonical product knowledge.

### Output Exceptions

- `.coferlandia/product-knowledge/profile.json` — explicit repository configuration describing
  knowledge surfaces and release policy; it contains no current/effective product values.
- Actual documentation/help/commercial destinations remain owned by their existing repository or
  external workflows and are never implicitly rewritten by this skill.

## Expected Output

```text
Product Knowledge result
Mode: impact | audit | verify
Subject/candidate: <identity>
User-facing: true | false | not-applicable
Surfaces evaluated: <count>
Resolved/covered: <count>
Unresolved required surfaces: <count>
Manual review pending: <count>
Handoffs: <items | none>
Profile fingerprint: <sha256 | none>
Result: PASS | BLOCKED | STALE | VALID
Evidence: <paths/references>
```

## Skill maintenance

When changing this skill itself, use `superpowers:writing-skills` when available and treat the
instruction contract as test-driven process documentation. Run the natural-language activation and
pressure cases in `tests/cases.json` before relying on a change: establish the RED baseline or gap,
make the smallest GREEN instruction/contract/CLI correction, then REFACTOR only while the pressure,
contract, CLI, and cross-skill tests remain green. Add a new pressure case whenever review exposes a
new rationalization or boundary loophole; never encode private conversation or customer data in a
public fixture.

For deterministic CLI behavior, use `superpowers:test-driven-development` when available or the
same RED -> minimal GREEN -> refactor discipline. Before claiming any verification, package, or
release gate passed, use `superpowers:verification-before-completion` when available and require
fresh evidence for the exact candidate.

## Gotchas

- **Release notes are enough:** wrong. Evaluate each configured surface independently.
- **The capability name appears, so docs are covered:** wrong. Audit semantic completeness.
- **External docs cannot be edited, so ignore them:** wrong. Keep them visible and use manual review.
- **Archivist owns all documentation:** wrong. Archivist owns durable project knowledge, not every
  user/support/commercial surface.
- **No profile means fail every release:** wrong. No profile means no persistent Product Knowledge
  release gate unless repository policy explicitly says otherwise.
- **A recent timestamp proves freshness:** wrong. Candidate identity and profile fingerprint control
  staleness; timestamps are informational only.
- **The CLI can infer user-facing impact:** prohibited. Semantic claims must come from agentic evidence.
- **Continuous bidirectional synchronization:** prohibited. Use explicit impact/audit/verify lifecycle
  points and owner handoffs.

## Scripts Available

- **`scripts/product-knowledge-sync-cli.py`** — deterministic profile/report validation,
  fingerprinting, and verification aggregation. No interactive prompts or mutations.

## References

- Read `references/profile-contract.md` when creating/changing the project profile.
- Read `references/impact-contract.md` for single-change report fields and dispositions.
- Read `references/audit-workflow.md` for historical/current semantic drift audits.
- Read `references/verification-gate.md` for candidate-bound release verification.
- Read `references/evidence-model.md` when choosing evidence or resolving source conflicts.
- Read `references/surface-semantics.md` when evaluating semantic coverage vs mere presence.
- Read `references/archivist-integration.md` when Archivist artifacts are present or a durable gap appears.
- Read `references/cross-skill-boundaries.md` when handing work to Archivist, Evangelist,
  software-development, planning, commercial owners, or CI/release flows.
