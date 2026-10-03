# State machine

States and allowed transitions are centrally defined in
`scripts/project_orchestrator_cli/state.py`. The v2 machine is Epic/task-oriented.

Main path:

```text
INITIALIZED
  -> CONFIG_VALIDATED
  -> CONTRACT_RESOLVED          # initial materialization already completed
  -> EPIC_WORKTREE_CREATING
  -> EPIC_WORKTREE_CREATED
  -> TASK_SELECTED
  -> CODING_RUNNING
  -> CODING_REPORTED
  -> COMPLETION_VERIFYING
  -> CANDIDATE_PREPARING
  -> CANDIDATE_COMMITTED
  -> REVIEW_WORKTREE_CREATING
  -> REVIEW_WORKTREE_CREATED
  -> REVIEW_RUNNING
  -> REVIEW_PASSED
  -> TASK_READY_FOR_MERGE
  -> ... next task ...
  -> HOLISTIC_REVIEW_WORKTREE_CREATING
  -> HOLISTIC_REVIEW_WORKTREE_CREATED
  -> HOLISTIC_REVIEW_RUNNING
  -> EPIC_READY_FOR_INTEGRATION
```

GitHub mode may then move to `PR_OPEN_AWAITING_MERGE_APPROVAL`; final delivery occurs only after an
explicit `integrate` command:

```text
EPIC_READY_FOR_INTEGRATION
  -> PR_OPEN_AWAITING_MERGE_APPROVAL   # GitHub mode
  -> INTEGRATING
  -> INTEGRATED
  -> ARCHIVING
  -> PROJECT_COMPLETED
```

## Claim guards

Claims are durable operation guards around the existing semantic states; they do not add parallel
state-machine branches.

- The Epic claim must be owned before `EPIC_WORKTREE_CREATING` can produce Git side effects.
- The selected task claim must be owned before `TASK_SELECTED` can invoke `coding-agent` through
  `CODING_RUNNING`.
- Claim ownership is revalidated on `resume` and `retry` before provider execution.
- `TASK_READY_FOR_MERGE`, review/fix states, provider waits, blocked states, and pending merge
  approval retain the claims.
- `PROJECT_COMPLETED` releases claims only after verified delivery and completion projection.
- `CANCELLED` releases claims through explicit cancellation cleanup; other terminal failures retain
  them until recovery or audited administrative release.

Claim acquisition and Project projection events are persisted in `run-state.json`, while the
authoritative active records live under `<git-common-dir>/project-orchestrator/claims/`.

Review changes use `FIXES_REQUIRED -> FIXING -> FIX_COMMIT_PREPARING -> CANDIDATE_COMMITTED`, then
create a fresh detached review worktree. Holistic findings use the corresponding
`HOLISTIC_FIX*` path. Fixes are additive commits; no v2 state implies `git commit --amend`.

Provider waits are durable and return to the recorded semantic state. Blocked/cancelled states
preserve the Epic implementation worktree/evidence by default. Important blockers include invalid
specification/config/authentication, invalid contract identity/linkage during initialization,
claim conflicts, Git/base movement, merge conflicts, and no semantic progress. Later contract-body
drift is not a runtime state transition because the local snapshot is frozen before the run begins.

The controller persists state before external/Git side effects where needed so `status`, `resume`,
`retry`, and `integrate` can avoid duplicating completed operations. Invalid transitions and
provider execution without valid claim ownership are rejected.

## GitHub integration-check states

The GitHub integration path is:

```text
PR_OPEN_AWAITING_MERGE_APPROVAL
  -> WAITING_FOR_INTEGRATION_CHECKS
  -> INTEGRATION_CHECKS_FAILED
  -> INTEGRATING
  -> INTEGRATED
  -> ARCHIVING
  -> PROJECT_COMPLETED
```

`WAITING_FOR_INTEGRATION_CHECKS` represents pending or temporarily unavailable authoritative CI evidence and is separate from provider availability. `INTEGRATION_CHECKS_FAILED` represents a missing required gate or a terminal non-allowed conclusion. Both preserve claims and are re-evaluable by a later integration attempt. `INTEGRATING` means review identity, remote base, exact-candidate gates, and the second pre-merge revalidation have all passed and the controller is executing the merge side effect.
