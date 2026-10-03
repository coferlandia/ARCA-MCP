# Cross-skill boundaries

Read this reference whenever Product Knowledge Sync hands work to planning, development, documentation owners, CI, or release controllers.

## Project Manager

Planning may carry an optional section:

```md
## Product Knowledge Impact

Status: required | not-required | already-resolved
Reference: <impact contract/report or none>
Expected surfaces: <surface IDs or defer-to-product-knowledge-sync>
```

PM identifies that reconciliation is needed; Product Knowledge Sync owns the reconciliation semantics.

## Software Development

Analyst preserves an explicit Product Knowledge Impact reference in executable tasks. Developer/coding-agent treats documentation work explicitly declared `required` by that contract as implementation scope. Reviewer checks the candidate against the declared impact contract. Missing/new product-documentation scope is escalated rather than silently invented.

Absence of a profile and impact contract preserves existing non-user-facing development behavior.

## Project Evangelist

Product Knowledge Sync owns configured detection/coverage state for developer-doc surfaces. Project Evangelist owns progressive developer-facing explanation when explicitly invoked. Handoffs carry exact behavior, evidence, and gap; Product Knowledge Sync does not duplicate repository maps/onboarding architecture.

## Archivist

Archivist owns durable in-project knowledge. Product Knowledge Sync reads it optionally and emits durable-gap handoffs; no implicit initialization or mutation.

## Commercial/external owners

External surfaces remain explicit. Lack of write access becomes `manual-review-required`, not omission. A human/external owner resolves the surface outside this skill's authority.

## CI and release

Product Knowledge Sync does not own Qualification or publication. A repository that opts in exposes `verify` as a repository-owned Qualification command. Generic Chat/local release controllers and CI Adapter need no Product Knowledge-specific behavior for v1; they execute repository-declared contracts.
