# Candidate commit lifecycle

Project-orchestrator v2 uses **additive immutable commits** on one Epic branch.

For each task:

1. coding/completion/deterministic checks pass in the Epic implementation worktree;
2. the controller stages only allowed product changes;
3. it creates one candidate commit;
4. it creates a detached review worktree at that exact SHA;
5. an independent reviewer approves or returns findings;
6. findings are applied by fix-agent in the Epic implementation worktree;
7. the controller creates a new additive `review-fix` commit;
8. the new branch HEAD receives a fresh detached review.

Never use `git commit --amend` after review begins. Previous candidate/fix SHAs remain durable
evidence and, in GitHub mode, are associated with task Issue + Epic identity.

A passing task becomes `ready_for_merge` but remains on the Epic branch. No task candidate is merged
to `main` independently.

After all tasks pass, one holistic Epic review evaluates the complete final HEAD against the Epic
contract and cross-task regressions. Holistic corrections are additive and invalidate the previous
holistic approval. Only the exact final reviewed SHA may reach `EPIC_READY_FOR_INTEGRATION`.

GitHub mode then opens one final PR. Task commits contain non-closing `Issue:` / `Epic:` metadata;
`Closes` references appear only in the final PR. Final squash/merge is a separate explicit
`integrate` operation, after which local task contracts become `done`/archived.

## Integration candidate authority

The reviewed Epic HEAD and the CI integration candidate are related but distinct authorities. Direct PR integration uses the current PR head SHA for both review identity and gate identity. Merge Queue may introduce a `merge_group` SHA whose CI is authoritative for the queued candidate while the merge side effect still requires the expected PR head SHA.

A green run for any superseded PR SHA is historical evidence only. Any material change that changes the reviewed PR head invalidates the old review and must pass the existing fresh-review lifecycle before its own exact-candidate CI can authorize integration.
