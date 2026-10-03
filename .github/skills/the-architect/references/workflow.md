# Architect workflows

## Preflight sequence

1. Resolve the current work contract and Architecture Gate.
2. Inspect project evidence read-only.
3. Load only relevant architecture-home records.
4. Evaluate quality constraints, reuse evidence, trade-offs, compatibility, and risk.
5. Apply Materiality Gate.
6. Update canonical records only where new durable evidence exists.
7. Replace only the managed Architect Addendum block.
8. Validate the gate and links.

## Assessment sequence

Use current project record as baseline. Record deltas, not a repeated survey. A first reliable
assessment may establish a baseline snapshot; later snapshots are exceptional.

## Release closeout

Update application records before summaries because the application record owns project↔component
results. Create one engagement/event only when the delta is material.

## No-material-change

Do not create placeholder ADRs, findings, events, or snapshots. Record evidence inspected and update
`last_assessed` only when appropriate.
