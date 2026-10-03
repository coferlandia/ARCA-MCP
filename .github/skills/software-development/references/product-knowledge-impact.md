# Product Knowledge Impact consumption

Read this reference when a work contract contains `## Product Knowledge Impact` or references a `product-knowledge-sync` impact report.

```md
## Product Knowledge Impact

Status: required | not-required | already-resolved
Reference: <impact contract/report or none>
Expected surfaces: <surface IDs or defer-to-product-knowledge-sync>
```

Rules:

- Absence of this section and absence of an opted-in repository Product Knowledge profile preserve existing development behavior.
- Analyst preserves the status/reference and assigns declared required documentation work to the execution task(s) that change the affected behavior; it does not re-decide Product Knowledge semantics.
- Developer/coding-agent treats `Status: required` plus its referenced impact contract as approved implementation/documentation scope. Resolve only the surfaces assigned to the task and return scope/semantic contradictions to the control authority/Product Knowledge owner.
- Reviewer verifies the implementation and in-scope documentation against the exact Product Knowledge Impact reference, including required-surface dispositions. It must not mark missing documentation complete merely because release notes or a capability name exist.
- `product-knowledge-sync` owns reconciliation/coverage semantics. Software Development does not create a shadow product catalog, invent release gates, or mutate external/manual surfaces outside its authority.
