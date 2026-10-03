# Archivist integration

Read this reference when `project-documentation-archivist` artifacts are present or Product Knowledge analysis reveals a durable project-knowledge gap.

## When Archivist is present

Relevant high-confidence evidence may include:

```text
README.md
AGENTS.md
DECISIONS.md
RUNBOOK.md
.agent/catalog/SOURCE_INDEX.md
.agent/catalog/PROCESSING_RUNS.md
```

Rules:

- read these artifacts as evidence; Product Knowledge Sync does not own them;
- never add Product Knowledge bookkeeping markers/frontmatter to Archivist files;
- never rewrite durable knowledge solely to satisfy Product Knowledge report mechanics;
- do not mirror GitHub operational work into Archivist history/TODO files;
- when durable knowledge is missing or contradictory, emit a structured `durable-knowledge-gap` handoff;
- if explicitly composed with Archivist, let Archivist decide and update its own canonical artifacts.

## When Archivist is absent

Proceed from repository/GitHub and configured-surface evidence. Do not initialize Archivist, emulate its catalog, or block solely because it is absent.

## Bidirectional boundary

Product Knowledge Sync may reveal durable gaps. Archivist may contain durable facts useful to Product Knowledge Sync. Neither continuously triggers, synchronizes, or rewrites the other; reconciliation occurs at explicit lifecycle points.
