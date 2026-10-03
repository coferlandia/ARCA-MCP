---
name: project-orchestrator
description: >
  Use ONLY when the user explicitly requests project-orchestrator or explicitly asks to execute an
  approved development contract through it. Supports a direct detailed plan or an Analyst-produced
  Epic/task graph. Explicit invocation only.
license: Apache-2.0
compatibility: >
  Requires Python 3.11+ and Git with worktree support. Execution providers require configured Codex
  and/or OpenCode CLIs. GitHub-backed Epic execution additionally requires authenticated GitHub CLI
  (`gh`); local `--spec` and `--manifest` execution do not.
metadata:
  author: coferlandia
  version: "2.4"
  category: ops
  status: active
  tested: "2026-09-03 - Exact-candidate integration gates, durable CI states, rerun selection, remote-base validation, and conditional squash merge covered across repository CI."
---

## Context

`project-orchestrator` is an explicit-invocation-only deterministic controller. It owns Git,
worktrees, commits, durable run state, provider retries, GitHub operational traceability, final integration,
and cleanup. Semantic models provide coding, completion verification, review, and in-scope fixes;
they never own controller state or Git lifecycle.

Reuse `software-development` role contracts rather than duplicating their semantic responsibilities.

The controller supports two first-class execution modes:

- **`direct-plan`** — one sufficiently detailed plan/specification is one undecomposed execution
  unit for a capable coding agent.
- **`task-execution`** — an Analyst-produced DAG of Atomic + Self-contained + Low-context tasks is
  executed serially in dependency order, typically by narrower/basic coding agents.

The orchestrator executes the selected execution graph. It must not re-plan, repartition, or
silently change the Epic/task contracts.

## Architecture Gate

During source/contract validation, before creating the Epic branch or worktree, validate the optional
`## Architecture Gate`. Absent gates and `Mode: none` / `Status: not-required` remain compatible.
When `Mode: the-architect`, only `Status: passed` may continue; `required` or `blocked` returns a
deterministic contract-validation blocker before materialization or Git side effects.

The controller never invokes `the-architect` automatically. The complete Epic body, including the
managed Architect Addendum, remains inside `EPIC.md` through one-time Initial Contract
Materialization. Do not create `ARCHITECTURE.md` or add contract synchronization.

Read `references/architecture-gate.md` when resolving any source contract that contains the gate.

## Contract sources

The public CLI accepts exactly one source:

```text
--spec <path>      local detailed plan; normalized to one DIRECT-PLAN unit
--epic <reference> GitHub Epic Issue; materialized to local contracts before workers run
--manifest <path>  local v2 execution manifest/task graph
```

GitHub mode is controller-facing. Coding/review/fix agents receive bounded local files and do not
need `gh`, GitHub API credentials, Project access, Issue browsing, or the original planning chat.

Before creating the Epic branch/worktree, the controller performs **Initial Contract
Materialization** exactly once:

- GitHub-only Epic/analysis/task contracts become the standard local execution tree;
- complete local contracts whose resolved strategy is `Tracking: GitHub` become marked, linked
  GitHub Epic/task Issues plus the canonical marked analysis comment;
- when both representations already exist, only repository/Epic/task identity and parent linkage
  are validated; bodies are not compared or merged;
- local-fallback tracking never publishes Issues merely because a remote exists.

The local tree is:

```text
.agent/work-items/epic-<issue>/
├── EPIC.md
├── ANALYSIS.md          # Analyst mode
├── manifest.json
├── tasks/
│   └── TASK-<issue>.md
└── archive/
```

Stable contract markers make interrupted local-to-GitHub initialization retry-safe and prevent
Issue duplication. Source hashes, timestamps, and revisions remain passive provenance. After
initialization, the local tree is the frozen contract snapshot for that run: the controller does not
re-fetch, compare, refresh, merge, or propagate later contract-body changes. Issue comments,
Project status, commit linkage, the final PR, closure, and archival remain active operational
traceability and are not contract synchronization.

## Execution lifecycle

1. Resolve and validate the v2 manifest/Execution Strategy, then complete one-time Initial Contract Materialization.
2. Atomically acquire the repository-wide Epic claim before creating the Epic branch or worktree.
3. Create **one Epic branch and one implementation worktree for the whole run**.
4. Select the next dependency-ready execution unit, atomically acquire its task claim, and project
   the GitHub Epic/task to `In Progress` when a Project is configured.
5. Invoke `coding-agent` only after claim ownership is verified and only in the assigned Epic
   worktree with the bounded Epic/task contract.
6. Invoke completion verification and deterministic controller checks.
7. Stage only allowed product changes; GitHub projection files under `.agent/work-items/**` are not
   included in GitHub-mode candidate commits.
8. Create an additive task candidate commit. In GitHub mode its metadata references task Issue and
   parent Epic without closing keywords.
9. Create a detached review worktree from the exact immutable SHA and invoke a distinct
   `code-reviewer`.
10. If review requires changes, run `fix-agent` in the Epic implementation worktree, create an
   **additive review-fix commit**, and review the new immutable HEAD again. Never amend a reviewed
   commit.
11. A passing task becomes `ready_for_merge`; it stays on the Epic branch, remains claimed and
   remains `In Progress`, and is not merged to `main`.
12. Repeat for all dependency-ready tasks.
13. Run one holistic Epic review over the final integrated branch HEAD. Holistic corrections are
   also additive and require a fresh holistic review.
14. Stop at `EPIC_READY_FOR_INTEGRATION` when the final SHA is approved.
15. In GitHub mode, record traceability, push the Epic branch, and open **one final PR**. The PR owns
   `Closes` references for delivered tasks/Epic. Stop at `PR_OPEN_AWAITING_MERGE_APPROVAL`.
16. Merge only through a separate explicit `integrate <run-id>` action. GitHub mode uses the final
   PR squash integration; local fallback performs a verified local squash and never pretends a PR
   exists.
17. After verified delivery to `main`, mark tasks and Epic `done`, synchronize Project/Issue
   completion evidence, release task/Epic claims, move local task files from `tasks/` to `archive/`,
   and clean run-owned worktrees/branches safely.

## Commit and Issue traceability

For GitHub task execution the relationship must be reconstructible in both directions:

```text
Epic Issue
   ↕
Task/Sub-Issue
   ↕
Candidate + review-fix commits
   ↕
Final Epic PR
   ↕
Final squash/merge SHA on main
```

Task commit example:

```text
feat: implement task outcome

Issue: #27
Epic: #8
```

Review correction example:

```text
fix: preserve existing behavior

Issue: #27
Epic: #8
Review: round 1
```

Task commits never use `Closes`, `Fixes`, or `Resolves`. The final PR owns delivery closure.
Machine-generated Issue comments use stable markers so retry/resume does not duplicate evidence.

## Durable claims

The local claim store is the authoritative exclusion mechanism. It lives under the Git common
directory, outside all worktrees, and blocks duplicate Epic/task execution across runs sharing that
Git repository. GitHub Project statuses, labels, and comments are not locks.

Claims are acquired atomically from fully written records, are idempotent for the owning run, and
fail closed on malformed state. They survive process termination, provider waits, blocked states,
`resume`, `retry`, review/fix cycles, PR creation, and worktree cleanup. There is no automatic
expiration or claim stealing.

Claims are released only after verified delivery to `main`, explicit cancellation, or audited
administrative release. Read `references/claims.md` before changing claim identity, persistence,
recovery, release, or Project projection behavior.

## GitHub Projects

When `github_project` is configured, Project membership/status is an operational projection only.
It must never redefine the Epic/task body contract or replace the local claim. Resolve
Project/item/field/option IDs structurally through `gh project` commands. Requested Project
mutations that fail must fail visibly; do not claim a status change that was not applied.
Issue-based execution remains usable when no Project is configured.

The controller projects active Epic/task Issues to the configured `in_progress` status before
provider execution, keeps them there through review and pending merge approval, and projects them to
`done` only after verified integration. Cancellation restores the previous Project status only when
it has not been changed externally.

## Direct-plan compatibility

A user can still provide a detailed plan exactly as before:

```bash
python scripts/project-orchestrator-cli.py run --spec plan.md --json
```

The plan becomes one `DIRECT-PLAN` execution unit regardless of Markdown headings. Analyst
involvement is not mandatory. The same Epic-scoped claim/commit/review/holistic-review/integration
gates apply.

## Core commands

```text
doctor
init-config
validate-config
providers list|probe
run --spec|--epic|--manifest
status [run-id]
resume <run-id>
retry <run-id>
cancel <run-id>
integrate <run-id>
cleanup <run-id>
claims list
claims inspect <claim-key-or-digest>
claims release <claim-key-or-digest> --reason <text> --force
validate-result
```

Use `run ... --dry-run` for local `--spec`/`--manifest` planning. When their resolved tracking is GitHub, dry-run validates and reports that initialization is required without creating Issues or inventing Issue numbers. GitHub `--epic` materialization is itself a local write; materialize first and preview its manifest when a mutation-free preview is required.

## Provider and workspace rules

- The controller assigns one Epic implementation worktree and detached immutable review worktrees.
- Coding/fix agents use only the Epic implementation worktree.
- Reviewers use only their exact detached candidate worktree and remain read-only.
- Only the controller creates claims, Project projections, commits, branches/worktrees, remote
  operational traceability, PRs, integration, archival, and cleanup.
- Provider retry/fallback remains deterministic and durable. Temporary provider capacity failures
  are not semantic approval/blocker decisions and do not release claims.
- A blocked/cancelled run preserves implementation worktree, commits, and evidence by default;
  cancellation releases claims explicitly after recording the terminal state.

## Safety invariants

- Do not activate merely because a plan, Issue, phase, or project-management concept is mentioned;
  require explicit project-orchestrator invocation.
- Direct plans are not heuristically decomposed.
- Initial Contract Materialization occurs before the worktree is created and never becomes continuous synchronization.
- Later GitHub/local contract-body drift does not mutate the frozen execution snapshot automatically.
- A provider must never run without valid Epic and current-task claim ownership.
- Never treat Project status, Issue comments, or labels as an atomic/distributed lock.
- Claims coordinate one Git common directory; they do not claim distributed exclusion across
  independent clones or machines.
- Analyst task dependencies must form a valid deterministic DAG.
- Never amend after independent review starts.
- Never merge a task independently to `main` in task-execution mode.
- Every post-review change invalidates the prior approval and requires a fresh immutable review.
- Final integration is never implied by review approval or PR creation; `integrate` is explicit.
- Refuse integration if the final reviewed SHA changed, the base advanced, or tracked base changes
  make integration unsafe.
- Never include GitHub operational projection files in product candidate commits.
- Development subroles never push or manage claims/Projects; controller-owned GitHub execution may
  push the reviewed Epic branch only when preparing the final PR.

## Expected output

```text
Run: {run-id}
State: {state}
Mode: {direct-plan | task-execution}
Current task: {id | none}
Epic branch/worktree: {branch} / {path}
Epic/task claim: {active | missing | conflict | released}
GitHub Project projection: {In Progress | skipped | failed | Done}
Candidate / final reviewed SHA: {sha | none}
Final PR: {number/url | none}
Next action or blocker: {detail}
Reports: {git-common-dir}/project-orchestrator/runs/{run-id}/
```

## Output Location

Durable runtime evidence lives under the target Git common directory at:

```text
project-orchestrator/runs/<run-id>/
project-orchestrator/claims/<claim-digest>.json
```

This is an operational exception to `_protocol/ARTIFACT_OUTPUT_CONVENTIONS.md`. Local execution
contracts/materializations use `.agent/work-items/` in the implementation workspace.

## Scripts Available

- `scripts/project-orchestrator-cli.py` — sole public tool interface.

## References

- `references/architecture.md` — controller/worker/source boundaries.
- `references/initial-contract-materialization.md` — one-time GitHub/filesystem initialization contract.
- `references/configuration.md` — v2 config, providers, optional GitHub Project mapping.
- `references/agent-protocol.md` — bounded worker requests/results.
- `references/state-machine.md` — durable Epic/task states.
- `references/candidate-commit-lifecycle.md` — additive immutable task/Epic review lifecycle.
- `references/claims.md` — authoritative local claims, Project projection, recovery, and administration.
- failure/recovery/troubleshooting references — provider and operational failure handling.

## GitHub integration gates

For GitHub-backed final integration, repository configuration may declare deterministic required gates under `integration.github.required_gates`. The controller evaluates only authoritative observations for the exact current integration candidate: normally the current PR head SHA, or the current merge-group SHA when a valid Merge Queue candidate exists. A green older SHA, comment, local test, or human assertion never satisfies the gate.

Missing required gates and disallowed terminal conclusions fail closed. Pending states use `WAITING_FOR_INTEGRATION_CHECKS`; terminal gate failures use `INTEGRATION_CHECKS_FAILED`. `neutral` and `skipped` count only when that gate explicitly lists them in `allowed_conclusions`. Transient observation errors never imply GREEN. Claims and Project `Done` projection remain active/incomplete until verified `PROJECT_COMPLETED`.

Immediately before merge, re-read PR head, remote base, candidate identity, and all configured gates. Only an unchanged GREEN candidate may enter `INTEGRATING`. The squash merge itself must carry the expected PR head SHA so GitHub atomically rejects a race-window head change. Remote base movement preserves the existing base-reconciliation/review requirement. Read `references/configuration.md`, `references/state-machine.md`, and `references/recovery.md` before changing these semantics.
