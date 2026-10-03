# Archivist Boundary

Use this reference whenever `project-documentation-archivist` has initialized the repository or the
proposed documentation touches durable project knowledge.

## Ownership

| Surface | Primary owner | Purpose |
|---|---|---|
| `README.md` | Archivist | Confirmed present state and universal repository entrypoint |
| `AGENTS.md` | Archivist | Minimum safe agent orientation and constraints |
| `DECISIONS.md` | Archivist | Durable rationale and trade-offs |
| `RUNBOOK.md` | Archivist | Repeatable operations and recovery procedures |
| `.agent/catalog/**` | Archivist | Source and processing traceability |
| `docs/**` | Evangelist | Progressive developer understanding and contributor orientation |
| GitHub Issues/Projects | GitHub workflow | Planned, active, blocked, and completed work |
| Git history and merged code | Repository | Implementation reality |

## Read boundary

Evangelist should read the canonical files as privileged evidence, then verify implementation-sensitive
claims against current code and configuration. An Archivist file may lag implementation; a closed Issue
may describe intent rather than final behavior.

## Write boundary

Evangelist writes analysis under `.agent/project-evangelist/` and approved developer documentation under
`docs/**`.

Do not modify `README.md`, `AGENTS.md`, `DECISIONS.md`, or `RUNBOOK.md` by default. When a developer
reading path needs one of those sources, link to it. When the canonical source is incomplete or wrong,
record an Archivist handoff instead of silently fixing it from the Evangelist workflow.

## Link instead of duplicate

Link when the target source already owns:

- detailed architecture rationale;
- operational or recovery procedures;
- agent-critical constraints;
- confirmed present-state inventory;
- active or historical work state.

A short contextual summary is allowed, but it must not become an independently maintained copy.

## Contradictions

When authoritative-looking sources disagree:

1. Record each source and its revision or path.
2. Inspect current implementation evidence.
3. Separate confirmed current behavior from unresolved rationale or procedure.
4. Do not silently choose between contradictory durable sources.
5. Add the unresolved item to the proposal and create an Archivist handoff when durable correction is
   required.

## GitHub work-state boundary

Do not create `TODO.md`, `HISTORY.md`, local open-question backlogs, or copied Issue/Project status pages.
Developer docs may explain the contribution workflow and link to the current GitHub work surfaces, but
GitHub remains the operational source.

## Handoff format

```text
Archivist handoff
- Target: README.md | AGENTS.md | DECISIONS.md | RUNBOOK.md
- Current statement: {summary}
- Conflicting evidence: {paths, Issue/PR/commit references}
- Required durable correction: {description}
- Developer-doc impact: {blocked page or reading path}
```
