# Verification gate

Read this reference when Product Knowledge evidence is used before release qualification/publication.

## Identity

Verification binds to all three identities:

1. exact caller-supplied candidate identity;
2. SHA-256 fingerprint of the current canonicalized profile;
3. SHA-256 fingerprints of the exact included impact/audit report payloads.

Timestamps are informational only. A current timestamp cannot make old candidate/profile evidence fresh.

## Results and exit codes

- `0`: PASS — input valid and no required surface remains unresolved.
- `2`: INVALID — malformed/unsupported profile or report contract.
- `3`: BLOCKED — structurally valid evidence contains unresolved required Product Knowledge work.
- `4`: STALE — candidate binding or supplied/stored profile fingerprint does not match current verification identity.
- `5`: ENVIRONMENT — evidence/path access failure distinct from invalid content.

A stale result must not be rewritten as BLOCKED or PASS.

## Required surface semantics

For `release_policy: required`, acceptable release dispositions are `updated`, `verified-no-change`, and `not-applicable`. `manual-review-required` and `blocked` are unresolved.

For audit evidence, a required surface is resolved only by `covered` or `not-applicable`; `partial`, `missing`, `stale`, `contradictory`, and `manual-review-required` remain unresolved.

`review` and `optional` surfaces stay visible in totals and manual-review counts but do not block unless repository policy adds an independent stronger rule.

## Authority

The CLI can produce deterministic verification for diagnostics anywhere. It becomes a release-blocking gate only when the target repository explicitly opts in through `.coferlandia/product-knowledge/profile.json` plus its repository-owned qualification policy. Installation alone grants no release authority.

## Output

The machine-readable result includes candidate, profile fingerprint, included report fingerprints, policy totals, blockers, manual-review count, and `PASS|BLOCKED` conclusion. Human summaries may project those fields but must not replace machine evidence.
