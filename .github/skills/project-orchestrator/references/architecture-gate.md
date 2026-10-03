# Architecture Gate enforcement

`work_items.py` parses the optional gate while resolving direct plans and v2 manifests. Before the
engine can create a branch/worktree, unresolved `Mode: the-architect` contracts raise a deterministic
validation blocker. Absent and `not-required` gates preserve compatibility; `passed` continues.

The complete Epic body, including its managed Architect Addendum, remains in `EPIC.md` through
one-time Initial Contract Materialization. Do not invoke the Architect automatically, create
`ARCHITECTURE.md`, or introduce contract synchronization.
