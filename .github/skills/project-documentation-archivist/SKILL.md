---
name: project-documentation-archivist
description: >
  Use when organizing project documentation, distilling durable project knowledge,
  maintaining AGENTS.md, README.md, DECISIONS.md, or RUNBOOK.md, processing documentation
  sources, migrating legacy TODO.md/HISTORY.md project memory to GitHub Issues, or
  synchronizing durable knowledge from GitHub Issues, PRs, commits, and local sources.
license: Apache-2.0
compatibility: >
  Requires read/write access to the target project, Git when the project is versioned,
  Python 3.11+, and GitHub CLI (`gh`) with authenticated repository access for GitHub
  ingestion or migration operations.
metadata:
  author: dc-sistemas
  version: "3.1.0"
  category: content
  status: active
  tested: "2026-07-31 - The Architect cross-project memory boundary covered by cross-skill ownership tests."
---

## Context

This skill is the durable knowledge layer for a project.

It does **not** own project work state or project history as duplicate Markdown databases.
For GitHub-hosted repositories, operational work and its lifecycle belong to GitHub Issues,
GitHub Projects, pull requests, and Git history.

Archivist reads those sources, together with local documentation, and distills only the
knowledge that must survive outside an individual issue or implementation discussion.

## The Architect boundary

`the-architect` owns cross-project architecture memory, reusable-component definitions, component
application results, and architectural engagement history in its dedicated architecture home.
Archivist continues to own durable documentation inside the target repository: README, AGENTS,
DECISIONS, RUNBOOK, and catalog traceability.

Architect findings may be consumed as evidence, but neither role mirrors the other's knowledge base.
GitHub Issues and Projects remain operational work state. Read
`references/the-architect-boundary.md` when processing Architect-produced evidence or deciding where
architecture knowledge belongs.

## Source-of-truth boundaries

- **GitHub Issues + GitHub Projects**: planned, active, blocked, and completed work.
- **Git / repository state**: implementation reality.
- **Archivist canonical files**: durable project knowledge.
- **`.agent/catalog/*`**: internal traceability for Archivist processing.

Never recreate GitHub work state in a repository-local TODO or chronological HISTORY file.

## Canonical files

| File | Role |
|---|---|
| `README.md` | confirmed present state |
| `AGENTS.md` | minimum safe agent entrypoint |
| `DECISIONS.md` | durable rationale and trade-offs |
| `RUNBOOK.md` | durable operational procedures |
| `.agent/catalog/SOURCE_INDEX.md` | local and remote source traceability |
| `.agent/catalog/PROCESSING_RUNS.md` | processing-session log |

`TODO.md` and `HISTORY.md` are legacy migration inputs only. They are not canonical outputs.
`.agent/catalog/OPEN_QUESTIONS.md` is also legacy. Material unresolved work or decisions that
need action must be represented by a GitHub Issue; temporary uncertainty may be recorded in
the current processing run.

Read `references/catalog-files.md` before updating canonical files.

## Prerequisites

1. Work inside the intended project root.
2. Inspect Git status before modifying repository files.
3. For GitHub operations, verify `gh auth status` and resolve the repository with
   `gh repo view --json nameWithOwner,url,hasIssuesEnabled,hasProjectsEnabled`.
4. Do not move, rename, archive, or rewrite files owned by another workflow such as CCPM.
   They may be read as evidence.
5. Preserve semantic content already present in `AGENTS.md`.

## Normal knowledge-distillation workflow

1. Read `references/workflow.md`.
2. Inventory local documentation sources and relevant GitHub sources.
3. Update `.agent/catalog/SOURCE_INDEX.md` incrementally.
4. Read sources deeply enough to distinguish operational events from durable knowledge.
5. Route only durable knowledge:
   - present facts -> `README.md`
   - agent-critical constraints -> `AGENTS.md`
   - rationale / trade-offs -> `DECISIONS.md`
   - repeatable operations -> `RUNBOOK.md`
6. When a source reveals actionable future work or a material unresolved question, create
   or link a GitHub Issue rather than adding a local backlog entry.
7. Mark/archive only local sources that Archivist owns and is allowed to process.
8. Never add Archivist frontmatter to GitHub entities or workflow-owned files.
9. Validate with:
   `python skills/content/project-documentation-archivist/scripts/validate_catalog.py --project-root .`
10. Finish with the sources inspected, durable files updated, GitHub Issues created/linked,
    validation evidence, and a suggested commit message.

## GitHub source processing

Treat GitHub as first-class evidence:

- Issues and issue comments describe requested work, discussion, blockers, and outcomes.
- PRs and reviews provide implementation and review evidence.
- Commits provide implementation evidence.
- GitHub Projects provide operational workflow state but are not copied into project docs.

`SOURCE_INDEX.md` must retain enough identity and revision data to decide whether a GitHub
source changed since the last processing run. A practical remote key is repository + entity
type + number, together with GitHub `updatedAt`.

If a previously processed Issue/PR changes materially, reevaluate its knowledge impact.
Do not append duplicate decision/runbook content merely because a source was revisited.

## Material unresolved questions

A material uncertainty that requires a human or future work is operational state and should
become a GitHub Issue. Preserve source references and the exact uncertainty in the Issue.

Temporary processing uncertainty that does not require future project work may be recorded
only in `.agent/catalog/PROCESSING_RUNS.md`.

Never silently choose between contradictory authoritative sources.

## Legacy GitHub-native migration mode

Use this mode for repositories that still contain `TODO.md` and/or `HISTORY.md`.

The migration is deliberately split between semantic classification and deterministic GitHub
mechanics:

1. Run preflight:
   `python scripts/github_migration.py preflight --project-root .`
2. Inventory legacy entries:
   `python scripts/github_migration.py inventory --project-root .`
3. Read every inventory item and classify it according to
   `references/github-migration.md`. Archivist performs this semantic step; the script must
   not guess architectural meaning from keywords.
4. Write the decisions file generated from the inventory template.
5. Validate decisions:
   `python scripts/github_migration.py validate-decisions --project-root . --decisions <file>`
6. Preview deterministic GitHub mutations:
   `python scripts/github_migration.py apply --project-root . --decisions <file>`
7. After explicit write authorization, run the same command with `--apply`.
8. Run Archivist knowledge distillation again against the migrated GitHub sources and the
   legacy documents so knowledge-only content reaches README/AGENTS/DECISIONS/RUNBOOK.
9. Validate cutover:
   `python scripts/github_migration.py validate-cutover --project-root . --decisions <file>`
10. Only after validation succeeds, remove `TODO.md` and `HISTORY.md` from the project.
11. Run `validate_catalog.py --require-github-native` and commit the migration.

Migration commands are dry-run by default. Re-running them must not create duplicate Issues.

## Migration classifications

Allowed dispositions are:

- `EXISTING_ISSUE`
- `EXISTING_PR`
- `EXISTING_GIT_EVIDENCE`
- `CREATE_OPEN_ISSUE`
- `CREATE_CLOSED_HISTORICAL_ISSUE`
- `KNOWLEDGE_ONLY`
- `OBSOLETE`
- `DUPLICATE`
- `NEEDS_REVIEW`

`NEEDS_REVIEW` blocks cutover.

Do not manufacture a GitHub Issue for prose that is only a durable decision, current-state
fact, operational procedure, or agent instruction. Distill that content into the appropriate
canonical file instead.

## AGENTS.md curation rules

1. Read the full existing file before editing it.
2. Preserve semantic content; never erase uncertain material silently.
3. Keep a short `Critical Instructions for Agents` section near the top.
4. Maintain `Project Essentials`, `Documentation Index`, and `Maintenance Notes`.
5. Link to README, DECISIONS, RUNBOOK, and relevant GitHub work surfaces rather than TODO/HISTORY.
6. Confirm build/test/lint/run commands before presenting them as valid.
7. Put deep operational procedures in RUNBOOK and deep rationale in DECISIONS.

## Gotchas

- Do not treat Issue closure as proof that repository documentation is correct; inspect the
  merged implementation and current repository when the distinction matters.
- Do not mirror entire Issues or PR conversations into Markdown.
- Do not create a second task-state system in `.agent/`.
- Do not create one historical Issue per commit during migration.
- Do not delete legacy TODO/HISTORY until every item has a reviewed disposition and cutover
  validation passes.
- Do not archive or modify CCPM-owned `.claude/prds/**` or `.claude/epics/**` sources.
- Do not require CCPM for GitHub ingestion. CCPM is an optional producer of GitHub work state.
- Do not publish legacy source bodies to GitHub automatically. Migration inventories may contain legacy source text for local semantic review; treat them as potentially sensitive and review before committing/sharing them.
- Do not store secrets in RUNBOOK.

## Output location

- Standard repository files remain at project root.
- Archivist internal artifacts go under `.agent/catalog/`.
- Local processed-source archives go under `.agent/archive/YYYY/`.
- Migration evidence goes under `.agent/migrations/` by default.

## Scripts

### `scripts/validate_catalog.py`

Validates the GitHub-native Archivist catalog. Use `--require-github-native` after migration
to reject remaining legacy TODO/HISTORY/OPEN_QUESTIONS artifacts.

### `scripts/github_migration.py`

Deterministic helper for per-project migration from TODO/HISTORY to GitHub Issues and for
cutover validation. It never performs GitHub writes unless `--apply` is supplied.

## References

- `references/workflow.md` - normal knowledge-distillation workflow.
- `references/catalog-files.md` - canonical artifact boundaries.
- `references/frontmatter.md` - local source traceability rules.
- `references/github-migration.md` - per-project TODO/HISTORY migration procedure.
- `references/open-questions.md` - legacy OPEN_QUESTIONS migration guidance.
- `references/validation.md` - validation and cutover rules.
