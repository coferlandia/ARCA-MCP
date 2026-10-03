# Git worktree lifecycle

For phase `{phase}`, the implementation path is
`<worktree-root>/<repository>/<run-id>/<phase>/implementation` on branch
`orchestrator/<run-id>/<phase>`. Review paths are
`review-<cycle>-<short-sha>` and are created with `git worktree add --detach` from the
candidate. Only the controller creates/removes worktrees or performs Git lifecycle
operations. Review worktrees are removed only after evidence is durable; implementation
worktrees only after merge, post-merge validation, report persistence, and phase completion.
