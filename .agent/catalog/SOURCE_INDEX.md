# Source Index

## Local Sources

| Status | Source | Archived path | Type | Detected | SHA-256 | Last processed | Feeds | Notes |
|---|---|---|---|---|---|---|---|---|
| processed | `README.md` | — | current-state documentation | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, RUNBOOK | Existing project overview, setup, certificate guidance and public API usage. Source remained in place. Git revision recorded by run base. |
| processed | `docs/MCP_SERVER.md` | — | technical contract | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, DECISIONS, RUNBOOK | MCP architecture, scopes, idempotency, numbering, health and PDF behavior. |
| processed | `docs/MCP_IDEMPOTENCY.md` | — | technical contract | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, DECISIONS, RUNBOOK | Durable fiscal replay/reconciliation, series ownership and single-writer model. |
| processed | `docs/REPOSITORY_SECURITY.md` | — | security/operations documentation | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, DECISIONS, RUNBOOK | Public-repository deployment/security boundary. |
| processed | `.github/workflows/ci.yml` | — | CI configuration | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, RUNBOOK | Confirmed build/test/package-audit and tracked-secret guard commands. |
| processed | `.gitmodules` | — | repository configuration | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, DECISIONS | Confirms `.agents` is pinned to `obra/superpowers`. |
| processed | `.github/skills/project-documentation-archivist/SKILL.md` | — | agent operational contract | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | AGENTS, DECISIONS, catalog | Archivist v3.1.0 contract; GitHub-native durable-knowledge boundary. |
| processed | `.github/skills/project-documentation-archivist/references/workflow.md` | — | agent workflow reference | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | catalog | Defines initialization/inventory/distribution/validation phases. |
| processed | `.github/skills/project-documentation-archivist/references/catalog-files.md` | — | agent workflow reference | 2026-10-03 | not-computed | 2026-10-03T05:18:00Z | DECISIONS, catalog | Defines canonical file ownership and forbids local work-state duplication. |

> Hash note: this initialization was executed through the GitHub connector rather than a local filesystem checkout. Exact repository identity is frozen by the run base commit below, but local-file SHA-256 values could not be computed by that execution surface. A future local Archivist run should replace `not-computed` with SHA-256 values when revisiting these sources; this is traceability debt, not an unresolved product decision.

## GitHub Sources

| Status | Repository | Source type | Number / SHA | URL | Revision / updatedAt | Last processed | Feeds | Notes |
|---|---|---|---|---|---|---|---|---|
| processed | `coferlandia/ARCA-MCP` | Issue | #31 | https://github.com/coferlandia/ARCA-MCP/issues/31 | 2026-10-03T05:16:36Z | 2026-10-03T05:18:00Z | catalog | Tracks this Archivist initialization; operational state stays in GitHub. |
| processed | `coferlandia/ARCA-MCP` | Issue | #28 | https://github.com/coferlandia/ARCA-MCP/issues/28 | 2026-10-03T04:42:11Z | 2026-10-03T05:18:00Z | catalog | Open PDF template-reference work retained only as GitHub work state; not mirrored into canonical backlog docs. |
| processed | `coferlandia/ARCA-MCP` | PR | #30 | https://github.com/coferlandia/ARCA-MCP/pull/30 | 2026-10-03T05:10:32Z | 2026-10-03T05:18:00Z | AGENTS, DECISIONS | Merged Superpowers submodule installation evidence. |
| processed | `coferlandia/ARCA-MCP` | commit | `29c409248d18723c35a956f8b129d49b263cb5d7` | https://github.com/coferlandia/ARCA-MCP/commit/29c409248d18723c35a956f8b129d49b263cb5d7 | immutable commit | 2026-10-03T05:18:00Z | AGENTS, catalog | Run base; Coferlandia skills installation commit. |
