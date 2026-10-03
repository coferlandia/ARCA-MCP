# Durable execution claims

`project-orchestrator` uses repository-wide claims to prevent two runs that share a Git common directory from executing the same Epic or task concurrently.

## Authority and storage

The local claim is the exclusion authority. GitHub Project status is an operational projection only.

Claims are stored outside all worktrees at:

```text
<git-common-dir>/project-orchestrator/claims/<sha256-of-claim-key>.json
```

The complete canonical key remains in the JSON record. Claim files are created atomically from a fully written temporary file, so concurrent acquisition produces exactly one owner and never exposes a partially written claim.

Epic keys use the GitHub repository and Epic issue when available. Local manifests and direct plans use stable source fingerprints. Task keys use the GitHub task issue when available and otherwise combine the Epic identity with the task id.

This provides exclusion across branches and worktrees that share one Git common directory. It does not provide distributed locking across independent clones or machines.

## Lifecycle

The controller acquires the Epic claim before creating the Epic branch or worktree. It acquires a task claim after task selection and before invoking `coding-agent`.

Claims remain active through implementation, completion verification, candidate commits, independent review, review fixes, holistic review, PR creation, and pending merge approval. `TASK_READY_FOR_MERGE`, provider waits, blocked runs, process termination, `resume`, and `retry` do not release ownership.

Claims are released only after verified delivery to `main`, explicit cancellation, or an audited administrative release. There is no automatic timeout, expiration, or claim stealing.

## GitHub Project projection

When `github_project` is configured, the controller projects the Epic and selected task to the configured `in_progress` status before provider execution. The default display value is `In Progress`.

Tasks remain `In Progress` while they are reviewed and while the final PR awaits merge. After verified integration, delivered tasks and the Epic are projected to `Done`, then their local claims are released.

When no GitHub Project is configured, execution continues using the local claim. GitHub labels and comments are not locking primitives.

On cancellation, the controller restores the previous Project status when it can do so safely. It does not overwrite a status that changed externally after this run applied `In Progress`.

## Administrative commands

```text
claims list
claims inspect <claim-key-or-full-digest>
claims release <claim-key-or-full-digest> --reason <text> --force
```

Administrative release is explicit, exact, and audited under the owning run's `claim-history/` directory. It does not revert code, commits, branches, worktrees, PRs, or Issue state.
