# AGENTS.md

## Critical Instructions for Agents

- Read this file before modifying the project.
- Review `README.md`, `DECISIONS.md`, and `RUNBOOK.md` before structural or high-risk changes.
- Inspect the relevant GitHub Issue/PR and related work before implementing or debugging tracked work.
- Do not change public contracts, schemas, migrations, integrations, or critical configuration without checking related documentation and decisions first.
- Run confirmed validation commands before considering a change complete.
- Material unresolved work belongs in GitHub Issues, not a local TODO file.

## Project Essentials

### Architecture

- Main architecture pattern:
- Core modules or services:
- Important boundaries:

### Main Conventions

- Naming:
- Folder organization:
- Testing pattern:
- Configuration rule:

### Sensitive Areas

- Public contracts:
- Database or migrations:
- Security or authentication:
- Third-party integrations:

### Validation Commands

- Install:
- Build:
- Test:
- Lint:
- Typecheck:
- Run:
- Other:

Mark unconfirmed commands as pending rather than inventing them.

## Documentation Index

### Start Here

- `README.md`: confirmed current project state.
- `AGENTS.md`: minimum operational guidance for agents.

### Durable Knowledge

- `DECISIONS.md`: architectural and technical rationale.
- `RUNBOOK.md`: operations, troubleshooting, and routine procedures.

### Work Tracking

- GitHub Issues: planned, active, blocked, and completed work.
- GitHub Project: operational workflow and prioritization.

### Archivist Traceability

- `.agent/catalog/SOURCE_INDEX.md`: processed local/GitHub source index.
- `.agent/catalog/PROCESSING_RUNS.md`: Archivist run log.

## Maintenance Notes

- Keep this file brief, operational, and agent-oriented.
- Preserve semantic content when reorganizing.
- Move deep detail into README, DECISIONS, or RUNBOOK.
- Do not recreate TODO.md or HISTORY.md for Coferlandia work tracking.
