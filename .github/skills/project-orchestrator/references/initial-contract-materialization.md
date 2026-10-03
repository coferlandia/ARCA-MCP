# Initial Contract Materialization

Initial Contract Materialization is the one-time controller operation that ensures the selected
GitHub-tracked initiative has both a GitHub contract representation and a bounded local execution
representation before the Epic worktree is created.

It is initialization, not contract synchronization. In particular, it does not establish continuous
or periodic reconciliation between GitHub and local files.

## Trigger

Run it only when the resolved `## Execution Strategy` contains:

```text
Tracking: GitHub
```

A configured GitHub Project is an operational projection and does not trigger or redefine contract
materialization. `Tracking: local fallback` never publishes Issues merely because a remote exists.

## GitHub to local

For `run --epic`, read the Epic body, the canonical marked Analyst analysis comment, linked task
Issues, and dependencies. Atomically create:

```text
.agent/work-items/epic-<issue>/
├── EPIC.md
├── ANALYSIS.md          # Analyst mode when canonical analysis exists
├── manifest.json
├── tasks/
│   └── TASK-<issue>.md
└── archive/
```

The manifest/frontmatter records repository and Issue identities plus passive snapshot provenance.
Workers use these files without GitHub access.

## Local to GitHub

For a complete local manifest whose resolved tracking is GitHub:

1. Validate the Epic, analysis, manifest, every task file, strategy, and DAG before any external write.
2. Resolve or create the Epic using a stable `coferlandia-contract-id` marker.
3. Resolve or create task Issues using stable task markers.
4. Establish native sub-Issue linkage when supported and retain `Parent Epic: #<number>` as the
   explicit fallback.
5. Publish the complete current analysis in one Epic comment beginning with:

```html
<!-- coferlandia-analysis-contract -->
```

6. Add Issues to a configured GitHub Project only as an operational projection.
7. Atomically persist repository/Epic/task Issue identities into `manifest.json`.
8. Continue execution from the already validated local files.

Stable markers make interruption recovery idempotent. Titles are never sufficient identity.

## Existing dual representation

When both stores already exist, validate only:

- repository identity;
- Epic Issue identity;
- task ID to Issue mapping;
- parent Epic linkage;
- existence of referenced local paths.

Do not compare bodies, infer identity from titles, or merge divergent text. Ambiguous or conflicting
identity blocks before worktree creation.

## Frozen snapshot boundary

After initialization completes and the run begins:

- do not re-fetch Issue bodies before tasks;
- do not compare GitHub/local contract hashes for freshness;
- do not refresh or merge contract files automatically;
- do not propagate later contract-body edits in either direction;
- do continue Issue comments, Project status, commit linkage, final PR closure, delivery evidence,
  and archival.

A future explicit refresh workflow requires a separate design and is not implied by `resume` or
`retry`.

## Dry-run

Dry-run validates local contracts and reports that GitHub initialization is required, but performs
no external writes and invents no Issue numbers. `run --epic --dry-run` retains its existing
limitation because GitHub-to-local materialization itself writes the local snapshot.
