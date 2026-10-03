# Expected Proposal Shape

Recommended:

1. `local-test-runner`
   - Source: `README.md`, `AGENTS.md`, current repository state
   - Reason: current, repeatable, safe local verification procedure

2. `provider-rendition-reconciliation`
   - Source: `RUNBOOK.md`, `DECISIONS.md`, `AGENTS.md`, relevant GitHub delivery evidence when available
   - Reason: current and project-specific, but generated skill must include approval gates before any production-affecting follow-up

Needs clarification:

1. `ingestion-log-triage`
   - Source: `RUNBOOK.md` plus an unresolved GitHub Issue or other current evidence
   - Reason: checksum step is ambiguous between script and admin UI

Rejected / stale:

1. `old-docker-compose-deploy`
   - Source: `docs/legacy/old-deploy.md`, `DECISIONS.md`, closed/merged GitHub evidence when available
   - Reason: contradicted by newer decisions and references a removed compose file

Approval rule:

- No `.agents/skills/` directory should be written before explicit approval.
