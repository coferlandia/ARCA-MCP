# Architecture

`project-orchestrator-cli.py` is the sole public interface. The deterministic controller owns
source resolution, one-time contract-store initialization, durable state transitions, Git,
worktrees, commits, provider retries, repository-wide claims, GitHub operational traceability,
final integration, archival, and cleanup. Models provide semantic coding/completion/review/fix
results only; they never own controller state, claim ownership, Project status, or Git authority.

## Source boundary

The controller normalizes exactly one source into a v2 execution manifest:

- `--spec`: local direct plan -> one `DIRECT-PLAN` unit;
- `--epic`: GitHub Epic -> one frozen local Epic/analysis/task snapshot;
- `--manifest`: local v2 manifest/task DAG, optionally published once when Tracking is GitHub.

Before a run creates its Epic worktree, Initial Contract Materialization ensures the missing GitHub
or filesystem counterpart exists. GitHub-only contracts produce `EPIC.md`, canonical `ANALYSIS.md`
when applicable, `manifest.json`, and task files. Local GitHub-tracked contracts produce marked,
retry-safe Epic/task Issues and one marked canonical analysis comment. Existing dual
representations are checked only for identity and parent linkage.

After this boundary, workers use the frozen local snapshot. Contract bodies are not re-fetched,
compared, refreshed, merged, or propagated automatically. Operational Issue comments, Project
fields, commit references, PR creation, closure, and archival continue independently.

## Claim boundary

The authoritative exclusion index lives outside every worktree at:

```text
<git-common-dir>/project-orchestrator/claims/
```

`ClaimStore` atomically creates fully written Epic/task records and rejects a second owner. The Epic
claim is acquired before branch/worktree creation. A task claim is acquired after deterministic
selection and before `coding-agent` invocation. Claims remain active through review, fixes,
holistic review, PR creation, and pending merge approval.

GitHub Project `In Progress` and `Done` values are projections of this controller state; they are not
locks. Issue comments and labels are also not locking primitives. The local claim coordinates all
worktrees and processes sharing one Git common directory, but it does not claim distributed
exclusion across independent clones or machines.

## Execution boundary

One Epic branch/worktree carries all execution units. Task dependencies are validated as a DAG and
executed serially. Candidate and review-fix commits are additive. Review always occurs from a
detached worktree at an immutable SHA. Passing a task marks it `ready_for_merge`; no task is merged
to `main` independently and its claim remains active.

After all tasks pass, one holistic Epic review approves the final branch HEAD. GitHub mode then
opens one final PR; local fallback retains an equivalent explicit integration gate. `integrate`
is the only final delivery action. Verified integration projects delivered GitHub items to `Done`
and releases the task/Epic claims.

## Components

- `work_items.py`: v2 manifest, tracking/origin metadata, Execution Strategy parsing, DAG validation/order.
- `contract_initialization.py`: one-time local-to-GitHub initialization, identity recovery, and dry-run preflight.
- `materialization.py`: one-time GitHub-to-local snapshots, canonical analysis extraction, passive provenance, archive paths.
- `claims.py`: atomic claim storage, ownership checks, Project lifecycle projection, release, and status enrichment.
- `claims_runtime.py`: reviewed pre-run claim identity and failure-safe preparation lifecycle.
- `claims_cli.py`: public claim commands and claim-aware wrappers around the existing v2 controller.
- `github_service.py`: structured `gh` reads/writes for Issues, Projects, and PRs.
- `GitService`: argument-array Git operations and controlled staging.
- `RunStore`: atomic durable state/events and per-run operation locking.
- provider adapters: model/CLI execution boundary.
- `integration.py`: final traceability, PR/integration, archive, and cleanup.

Run state lives at `<git-common-dir>/project-orchestrator/runs/<run-id>/`; active claims live at
`<git-common-dir>/project-orchestrator/claims/`. Writes are atomic and run events are append-only.
Execution adapters must use argument arrays, UTF-8, timeouts, redaction, and never `shell=True`.

## Integration gate boundary

Final GitHub integration is intentionally split into three layers: `GitHubService` acquires authoritative GitHub evidence and performs the head-conditional merge; `integration_gates.py` is a pure deterministic policy evaluator with no I/O; `integration.py` owns run-state transitions, double revalidation, and the final merge decision.

The authoritative candidate is the current PR head unless a valid current `merge_group` candidate for that PR contains the current remote base. The controller uses the remote base SHA, not a possibly stale local `main` ref, at this boundary. No semantic worker or model may decide that CI is green.
