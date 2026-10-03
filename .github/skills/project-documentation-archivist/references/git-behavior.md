# Git Behavior

Detect Git before any archive move or history-based inference.

## Required Actions

1. Detect whether `.git/` exists.
2. Read the current branch when possible.
3. Read the current commit when possible.
4. Detect whether the working tree already contains uncommitted changes.
5. Register branch, commit, and dirty state in `docs/catalog/PROCESSING_RUNS.md`.

## Archive Moves

When the source project uses Git:

1. Prefer `git mv` for tracked files.
2. Fall back to a normal move if `git mv` fails because the file is untracked or the
   environment blocks the command.
3. Record the fallback in `PROCESSING_RUNS.md`.

When the source project does not use Git:

1. Use a normal move.
2. Record branch as `none`.
3. Record base commit as `none`.

## Forbidden Actions

Never:

- create commits automatically
- push automatically
- rewrite `.git/`
- delete source documents without archiving them first

## Suggested Commits

Use this commit suggestion after processing mode:

```text
docs: update project documentation catalog
```

Use this commit suggestion after resolution mode:

```text
docs: resolve documentation catalog conflicts
```
