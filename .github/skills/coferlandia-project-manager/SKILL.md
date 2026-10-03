---
name: coferlandia-project-manager
description: >
  Use ONLY when the user explicitly wants Coferlandia portfolio/project management across
  an explicit projects.json registry, GitHub Issues/Projects reporting, project architecture,
  Archivist knowledge-health coordination, or Obsidian portfolio projection. Explicit invocation only.
license: Apache-2.0
compatibility: >
  Requires Bash, git, and Python 3.11+. GitHub-backed planning/reporting requires GitHub CLI
  (`gh`) authenticated for the managed repository. GitHub Projects operations additionally
  require a token with the `project` scope. Project architecture/planning can fall back to
  local `.agent/work-items/` contracts when GitHub is unavailable or local tracking is selected.
metadata:
  author: coferlandia
  version: "0.9.0"
  category: ops
  status: active
  tested: "2026-10-01 - Architecture Gate selection plus optional Product Knowledge Impact planning handoff covered by cross-skill tests."
---

## Context

This skill is the portfolio and project-architecture layer for Coferlandia.

It does not own a second task database. In GitHub mode, operational work state and active work
contracts belong to GitHub Issues and GitHub Projects. In local-fallback planning, initiative
contracts live under `.agent/work-items/` without recreating a PM-owned task database. The
repository contains implementation reality. Archivist owns durable knowledge. Obsidian is an
optional generated projection.

## PM responsibilities

### 1. Project architecture

Transform a conversation, rough idea, external document, bug cluster, or product requirement
into a coherent development initiative.

Relevant Superpowers remain intentionally available for design work, especially:

- `superpowers:brainstorming`
- `superpowers:writing-plans`
- `superpowers:preserving-productive-tensions` when applicable
- `superpowers:writing-skills` for skill projects when applicable

Execution-oriented Superpowers are not PM runtime dependencies merely for task tracking.
Development/execution remains delegated to the selected delivery workflow or development role.

### Epic Planner

Treat **Epic Planner** as a project-architecture capability of this skill, not as a separate
implementation role. Epic Planner owns initiative-level **WHAT and WHY**:

- problem statement and desired outcome;
- product requirements and global scope;
- public/compatibility constraints;
- non-goals;
- acceptance criteria;
- master implementation direction;
- selected execution strategy.

Epic Planner must not perform repository-wide Analyst decomposition or force low-level
implementation decisions that require technical analysis of the current codebase.

When the user supplies enough intent to create an initiative, use the applicable design
Superpowers above. For skill-development initiatives, use `superpowers:writing-skills` when it
is available in addition to the planning discipline.

### Workflow selection

Before handing a development initiative to execution, resolve the workflow dimensions that are
not already evident from the user's request. Do not run a fixed questionnaire and do not repeat
decisions the user already made.

Resolve only the dimensions that remain materially ambiguous:

- whether a master Epic/plan must be produced or is already supplied;
- whether the plan should go through `analyst` decomposition or directly to a capable
  `coding-agent`;
- whether operational tracking uses GitHub or local fallback;
- whether execution uses `project-orchestrator` or standalone development roles.

Two execution strategies are first-class:

1. **Direct capable-agent execution** — a sufficiently executable master plan is handed directly
   to a capable `coding-agent`; Analyst decomposition is not mandatory.
2. **Analyst-decomposed execution** — an `analyst` absorbs broad repository/system context and
   converts the master contract into Atomic + Self-contained + Low-context tasks for basic or
   narrowly scoped coding agents.

Record the resolved choice once so downstream roles do not repeatedly ask for it:

```md
## Execution Strategy

Tracking: GitHub | local fallback
Decomposition: Analyst | none
Execution: Project Orchestrator | standalone development roles
Worker profile: Basic coding agents | capable coding agent
Review: Per-task + final Epic review | final independent review
Integration: Single PR / squash merge | explicitly selected alternative
```

The PM chooses or confirms the workflow. The orchestrator executes the selected strategy and
must not silently redesign it.

### Architecture Gate

Before publishing an initiative, decide whether it materially needs `the-architect` Architecture
Preflight. Select the gate for new or cross-cutting subsystems, shared/public contracts,
persistence or migrations, security/trust boundaries, reliability/concurrency/transactions/eventing,
deployment topology, reusable-component selection or extraction, major modernization, or material
performance/scalability constraints.

Do not require the gate for Retouch Mode or ordinary localized work. Epic Planner records the gate
but does not perform the assessment. When selected, use:

```md
## Architecture Gate

Mode: the-architect
Status: required
Assessment reference: none
Addendum updated: none
Blocker: Architecture Preflight pending
```

Read `references/architecture-gate.md` whenever gate selection may apply. The Architect updates the
managed addendum and changes the status to `passed` or `blocked` before Analyst or direct execution.

### Product Knowledge Impact

When an initiative changes user-facing product behavior, or explicitly requests reconciliation
across Help/support/developer/commercial/release knowledge, planning may add:

```md
## Product Knowledge Impact

Status: required | not-required | already-resolved
Reference: <impact contract/report or none>
Expected surfaces: <known surface IDs or defer-to-product-knowledge-sync>
```

PM surfaces the need and reference; it does not perform the Product Knowledge audit or become the
author of every documentation destination. Use `required` when the initiative clearly changes
user-facing behavior and a configured profile/explicit reconciliation request applies. Use
`not-required` for non-user-facing work with no such impact, and `already-resolved` only when an
exact current impact contract/report is supplied. `Expected surfaces` may defer to
`product-knowledge-sync`; do not create a shadow surface catalog.

If no Product Knowledge profile exists and no explicit reconciliation request is present, do not
invent a new release gate. Downstream Analyst/development preserves this section/reference when
present. Read `references/product-knowledge-impact.md` whenever Product Knowledge scope may apply.

### Planning storage policy

Prefer GitHub when it is available and the selected workflow uses GitHub tracking.

In GitHub mode:

- create or update the Epic Issue as the authoritative initiative contract;
- use the configured GitHub Project as an operational projection for workflow/prioritization;
- preserve later decisions, deviations, reviews, and evidence chronologically;
- use native Epic/sub-issue relationships when available and the repository's explicit fallback
  linkage convention otherwise.

When GitHub is unavailable or local tracking was explicitly selected:

- continue project architecture/planning locally instead of failing solely because `gh` is absent;
- write the equivalent Epic/master contract under `.agent/work-items/<epic>/` following the
  repository artifact convention;
- do not recreate `TODO.md`, `HISTORY.md`, or a PM-owned operational task database;
- keep `projects.json` limited to portfolio membership/integration metadata.

Epic Planner writes exactly one complete representation per invocation. It does not manually mirror or
continuously synchronize GitHub and `.agent/work-items/`.

When the resolved strategy is `Tracking: GitHub` but the active Planner lacks usable GitHub write
capability, it may emit the complete standard local Epic contract and hand it to
`project-orchestrator`. Before execution, the orchestrator performs one-time Initial Contract
Materialization to create the missing GitHub counterpart. When Planner writes the Epic directly to
GitHub, the orchestrator performs the inverse one-time materialization into local execution files.
After that boundary, the local files are the frozen contract snapshot for the run; later contract
changes are not propagated automatically in either direction.

### 2. Portfolio management

The PM home owns:

- `.coferlandia/project-manager/config.json`
- `.coferlandia/project-manager/projects.json`
- `.coferlandia/project-manager/reports/`
- `.coferlandia/project-manager/state.json`
- optional local Obsidian portfolio projection

`projects.json` is authoritative only for **portfolio membership and integration metadata**.
It must not duplicate Issue status or Project field values.

A project entry may contain:

```json
{
  "slug": "secretaria",
  "path": "C:/dev/secretaria",
  "repository": "diegocofre/secretaria",
  "github_project": {
    "owner": "diegocofre",
    "number": 4
  },
  "status": "active"
}
```

`repository` and `github_project` are optional during migration; repository identity can be
resolved from the local Git remote when possible.

## Source-of-truth boundaries

### GitHub mode

- GitHub Issues: active initiative/task contracts and requested/pending/active/blocked/completed work.
- GitHub Projects: workflow state, prioritization, iteration, and project-board fields.
- Git repository: implementation reality.
- Archivist: README/AGENTS/DECISIONS/RUNBOOK and knowledge traceability.
- PM `projects.json`: portfolio membership and GitHub Project coordinates only.
- Obsidian: human-readable projection only.

### Local-fallback planning

- `.agent/work-items/<epic>/`: active initiative/task work contracts for that explicitly local workflow.
- Git repository: implementation reality.
- Archivist: durable project knowledge.
- PM `projects.json`: portfolio membership/integration metadata only.

Never recreate TODO.md/HISTORY.md as PM state.

## Prerequisites

Always required:

- Bash
- git
- Python 3.11+

Required only for GitHub-backed operations:

- `gh`
- authenticated access to managed repositories
- `project` token scope when GitHub Project data/mutation is requested

Useful checks:

```bash
gh auth status
gh auth refresh -s project   # when Project access is missing
```

A missing GitHub prerequisite blocks the requested GitHub operation; it does not by itself block
local project architecture/planning when the selected workflow permits local fallback.

## Onboarding

1. Run `scripts/pm-onboard.sh --apply --json`.
2. Register projects with `scripts/pm-manage-projects.sh add <path>`.
3. Prefer storing the resolved GitHub repository and, when known, GitHub Project owner/number.
4. Run `scripts/pm-portfolio-report.sh --json`.
5. Migrate any repository still using Archivist v2 TODO/HISTORY before treating GitHub state as authoritative.

## GitHub-native reporting

The reporting layer should answer from GitHub and Git state:

- open Issues;
- Issues represented in the configured GitHub Project;
- blocked work using native `blockedBy`/`blocking` relationships when available;
- current Project status/priority fields when available;
- recently closed Issues;
- linked PR/implementation context where exposed;
- repository dirty/ahead/behind/staleness state;
- Archivist durable-knowledge health.

A repository that has not completed the GitHub-native migration may expose a migration warning,
but its TODO/HISTORY files must not be treated as authoritative portfolio state.

## Project status

Prefer configured GitHub Project `Status` values. Do not force all repositories into the old
18-state PM state machine.

For portfolio aggregation, normalize only broad categories when possible:

- `backlog`
- `in-progress`
- `review`
- `blocked`
- `done`
- `unknown`

Preserve the original Project status text in detailed output.

## Archivist integration

The PM reads:

- `README.md` for confirmed current state;
- `DECISIONS.md` for rationale;
- `RUNBOOK.md` for operations;
- `AGENTS.md` for agent-critical constraints;
- Archivist catalog health for processing status.

The PM reads GitHub directly for active/completed work in GitHub mode. It does not ask Archivist
for a TODO or HISTORY mirror.

## Obsidian projection

Obsidian pages are generated from GitHub/portfolio state.

Generated project sections may contain:

- repository and GitHub Project links;
- counts by status;
- Issues in progress/review/blocked;
- recently completed Issues;
- project priority/iteration when present;
- Archivist health;
- Git status.

Generated sections are disposable/rebuildable. Preserve human notes outside managed sections.
Generated per-Issue notes are allowed only as mechanically replaceable projections. They must
never carry independent workflow state that can diverge from GitHub; preserve human notes outside
managed sections.

## Git policy

The PM may inspect repositories and GitHub state. It does not automatically implement code,
create development branches, merge, or push. Delivery workflows own their own Git lifecycle.

## Migration boundary

Phase 1 requires every managed repository to migrate legacy operational files through
`project-documentation-archivist` before the repository is considered GitHub-native.

Expected migrated Archivist files are:

```text
README.md
AGENTS.md
DECISIONS.md
RUNBOOK.md
.agent/catalog/SOURCE_INDEX.md
.agent/catalog/PROCESSING_RUNS.md
```

`TODO.md`, `HISTORY.md`, and legacy `OPEN_QUESTIONS.md` are migration inputs only.

## Reporting commands

Existing read/report entry points remain stable:

- `pm-portfolio-report.sh`
- `pm-project-report.sh`
- `pm-task-report.sh` (task now means GitHub Issue)
- `pm-health-check.sh`
- `pm-clean-worktrees.sh`

`pm-task-report.sh --task` prefers `project-slug#number`; plain `#number` is accepted only when it
resolves uniquely across the managed portfolio, and GitHub Issue URLs are also accepted.

## Safety

- GitHub and Git reads must fail visibly when authentication/repository resolution fails.
- Do not silently fall back to TODO/HISTORY for authoritative status.
- Writes to Obsidian/runtime projections remain approval-gated and dry-run-first where existing wrappers provide that behavior.
- Never claim a GitHub Project field was read when the project is not configured or access failed.
- Preserve manual user notes in Obsidian.
- Do not ask workflow-selection questions whose answer is already explicit in the initiating request.
- Do not let Project fields redefine an Epic/task contract.
- Do not invent Product Knowledge surface catalogs or release gates; only record the optional impact handoff when applicable.

## Expected output

```text
PM coordination output:
- portfolio membership
- GitHub repository / Project state
- active, blocked, review, and recently completed Issues
- Git/Archivist health
- project architecture or next design action when requested
- selected Execution Strategy when development delivery is being coordinated
- Product Knowledge Impact status/reference when applicable
- migration warnings when a project has not completed GitHub-native cutover
```
