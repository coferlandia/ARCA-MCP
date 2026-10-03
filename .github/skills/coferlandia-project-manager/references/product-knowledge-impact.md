# Product Knowledge Impact planning hook

Read this reference when an initiative changes user-facing product behavior or explicitly requests product/help/support/commercial documentation reconciliation.

Planning may add:

```md
## Product Knowledge Impact

Status: required | not-required | already-resolved
Reference: <impact contract/report or none>
Expected surfaces: <known surface IDs or defer-to-product-knowledge-sync>
```

Rules:

- PM identifies whether reconciliation is relevant; it does not perform the semantic audit or become a documentation owner.
- Use `required` when the initiative clearly changes user-facing behavior and a Product Knowledge profile/explicit reconciliation request applies.
- Use `not-required` for non-user-facing work with no product-knowledge impact.
- Use `already-resolved` only when an exact current impact contract/report is supplied.
- `Expected surfaces` may defer to `product-knowledge-sync`; PM must not invent a shadow surface catalog.
- If no Product Knowledge profile exists and no explicit reconciliation request is present, do not impose a new release gate.
- Downstream Analyst/development preserves the section/reference; `product-knowledge-sync` remains owner of reconciliation semantics.
