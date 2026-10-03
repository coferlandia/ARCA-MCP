# Impact contract

Read this reference for one bounded product change.

## Minimum report

```json
{
  "schema_version": 1,
  "mode": "impact",
  "subject": {"type": "issue", "reference": "#123"},
  "candidate": "optional-exact-candidate-binding",
  "user_facing": true,
  "rationale": "Why the change is or is not user-facing",
  "behaviors": [
    {"id": "stable-local-id", "summary": "Behavior changed", "evidence": ["reference"]}
  ],
  "surfaces": [
    {"id": "end-user-help", "disposition": "updated", "references": ["docs/help/feature.md"], "reason": null}
  ],
  "handoffs": []
}
```

`candidate` is optional for impact analysis but mandatory when the report participates in candidate-bound `verify`.

## Dispositions

- `updated`: the surface was changed and has evidence/reference where available.
- `verified-no-change`: semantic review found no update necessary; `reason` is required.
- `not-applicable`: the behavior does not apply to this surface; `reason` is required.
- `manual-review-required`: the surface remains visible but requires an owner/human/manual decision.
- `blocked`: required work cannot currently be completed.

Every configured surface must have exactly one entry for a user-facing impact. A non-user-facing report may carry zero surface entries but must explain the decision.

## Handoffs

Each handoff contains `owner`, `type`, `summary`, and `blocking`. Handoffs do not grant authority to mutate the destination. Examples include `durable-knowledge-gap`, `developer-doc-gap`, and `commercial-review`.

## Semantic rule

The CLI validates structure and declared coverage only. It does not decide whether the rationale, behavior list, or documentation prose is semantically correct.
