# Catalog Files

Archivist stores durable knowledge only. GitHub owns operational work state and historical work records.

## README.md

Present confirmed project state: purpose, status, architecture, components, setup, configuration, main flows, relevant structure, confirmed limitations, and links to extended documentation.

Do not turn README into a diary or backlog.

## AGENTS.md

Minimum reliable entrypoint for an agent before changing the project. Preserve critical instructions, architecture boundaries, conventions, sensitive areas, confirmed validation commands, and a concise documentation/work-tracking index.

Do not duplicate GitHub Issues or deep history here.

## DECISIONS.md

Durable project rationale. A meaningful decision should capture context, chosen option, alternatives, reasons, consequences/trade-offs, and source references such as related Issues/PRs/commits.

Plain implementation events do not belong here.

## RUNBOOK.md

Repeatable operational knowledge: local startup, deployment, health checks, logs, troubleshooting, maintenance, backups/restoration, emergency procedures, and operational risks. Never store secret values.

## .agent/catalog/SOURCE_INDEX.md

Traceability for local and remote sources.

Each record must identify:

- source type;
- source identity;
- revision/hash or GitHub `updatedAt`;
- processing status;
- last processed time;
- canonical files fed;
- concise notes.

Local and GitHub sources may use different table sections or a structured representation, but remote sources must not be forced into fake filesystem paths.

## .agent/catalog/PROCESSING_RUNS.md

Append-only run log containing date/time, mode, Git base, sources processed, GitHub mutations when any, files updated, temporary uncertainties, validation evidence, factual summary, and suggested commit.

## GitHub Issues / Projects

These are not Archivist files, but they are part of the project-memory contract.

Use GitHub Issues for:

- pending work;
- bugs;
- roadmap work;
- validation work;
- documentation work requiring action;
- material unresolved questions;
- completed work history when represented by the Issue lifecycle.

Use GitHub Projects for operational workflow state, prioritization, iteration, and portfolio projection.

## Legacy files

`TODO.md`, `HISTORY.md`, and `.agent/catalog/OPEN_QUESTIONS.md` are migration inputs only. Do not create them for newly initialized GitHub-native projects.
