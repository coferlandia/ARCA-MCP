# Architecture knowledge model

Canonical entities:

- `PROJECT-<slug>` — concise mutable current architecture.
- `ADR-<slug>` — immutable decision; successors link with `supersedes`/`superseded_by`.
- `ARCH-<slug>` — material finding/risk/debt.
- `COMP-<slug>` — reusable component definition and cross-project recommendation.
- `APP-<slug>` — canonical project↔component application, adaptations and result.
- `ENG-<slug>` / `EVENT-<slug>` — immutable architectural delta.
- `EXTRACT-<slug>` — executable extraction design.

Use flat YAML properties and stable wikilinks. Project/component notes link to an Application Record;
they never reproduce its result. GitHub Issues/PRs/Git own exact delivery evidence.

Managed indexes use stable markers. Human prose outside markers is preserved.
