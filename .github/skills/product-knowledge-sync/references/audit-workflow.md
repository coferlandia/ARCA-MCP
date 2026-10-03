# Audit workflow

Read this reference for historical/current documentation drift discovery across a release range, milestone, product area, or repository.

## Sequence

1. Lock the audit subject/range and collect implementation/release/Issue/PR evidence.
2. Reconstruct actual current or range-specific behavior before reading documentation conclusions.
3. Evaluate every configured/run surface independently.
4. For each material behavior, inspect the semantic dimensions that actually apply: purpose, setup/configuration, usage, roles/permissions, restrictions/preconditions, adjacent interactions, failure behavior, troubleshooting/recovery, time/location/data semantics, and migration/deprecation.
5. Classify each surface with evidence and reason.
6. Emit owner handoffs for gaps outside Product Knowledge Sync authorship authority.
7. Validate the report mechanically with the CLI.

## Audit statuses

- `covered`
- `partial`
- `missing`
- `stale`
- `contradictory`
- `not-applicable`
- `manual-review-required`

Presence is not coverage. A page containing the feature name can still be `partial`, `stale`, or `contradictory`.

## Suggested report shape

```json
{
  "schema_version": 1,
  "mode": "audit",
  "subject": {"type": "range", "reference": "v1.4.0..v1.5.0"},
  "candidate": "optional-exact-candidate-binding",
  "surfaces": [
    {
      "id": "end-user-help",
      "status": "partial",
      "references": ["docs/help/feature.md"],
      "reason": "Usage is current but recovery behavior is absent"
    }
  ],
  "handoffs": []
}
```

`candidate` becomes mandatory when the report participates in `verify`.
