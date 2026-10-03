# Workflow

Execute knowledge distillation in fixed phases. Operational work state belongs to GitHub.

## Phase 0 - Preparation

1. Resolve project root, Git branch, base commit, remote, and dirty state.
2. Detect canonical files: `README.md`, `AGENTS.md`, `DECISIONS.md`, `RUNBOOK.md`, `.agent/catalog/SOURCE_INDEX.md`, `.agent/catalog/PROCESSING_RUNS.md`.
3. Create missing canonical files from `assets/`.
4. Detect legacy `TODO.md`, `HISTORY.md`, and `.agent/catalog/OPEN_QUESTIONS.md`; report them as migration inputs, not canonical outputs.
5. When GitHub is available, resolve repository identity with `gh repo view` and verify authentication.
6. Record the run base state in `PROCESSING_RUNS.md`.

## Phase 1 - Inventory

Inventory both local and remote sources.

Local candidates include `docs/inbox/`, `docs/`, `notes/`, `documentation/`, `design/`, `specs/`, `planning/`, `issues/`, and project-root documentation.

GitHub candidates include relevant Issues, issue comments, PRs, reviews, and commits.

Do not inventory GitHub Project item state as documentation to copy. It is operational state.

For every source retain an identity and revision signal:

- local file: path + SHA-256;
- GitHub Issue/PR: repository + entity type + number + `updatedAt`;
- commit: repository + SHA.

## Phase 2 - Classification

Classify information, not merely files:

- current state;
- agent-critical instruction;
- decision/rationale;
- operational procedure;
- actionable work;
- historical operational event;
- temporary uncertainty;
- mixed.

Actionable work and material unresolved questions belong in GitHub Issues. Historical events normally remain represented by existing Issues/PRs/commits. Only durable knowledge is written into Archivist canonical files.

## Phase 3 - Detailed reading

Read enough evidence to separate:

- requested behavior from implemented behavior;
- discussion from final decision;
- historical event from durable rationale;
- one-off recovery action from repeatable runbook procedure;
- unresolved work from present-state documentation.

For completed GitHub work, inspect linked implementation evidence when necessary. Do not assume a closed Issue proves current repository state.

## Phase 4 - Distribution

Route durable knowledge strictly:

- confirmed present state -> `README.md`;
- minimum agent orientation -> `AGENTS.md`;
- rationale/trade-offs -> `DECISIONS.md`;
- repeatable operations -> `RUNBOOK.md`;
- traceability -> `.agent/catalog/SOURCE_INDEX.md`;
- run metadata / temporary uncertainty -> `.agent/catalog/PROCESSING_RUNS.md`.

Route actionable future work and material unresolved questions to GitHub Issues.

Do not create `TODO.md`, `HISTORY.md`, or a new local open-question backlog.

## Phase 5 - Update order

1. `DECISIONS.md`
2. `RUNBOOK.md`
3. `README.md`
4. `AGENTS.md`
5. `.agent/catalog/SOURCE_INDEX.md`
6. `.agent/catalog/PROCESSING_RUNS.md`

Preserve existing human content. Managed blocks may be used when repeated deterministic updates are necessary.

## Phase 6 - Local source marking

Only Archivist-owned local processable sources receive catalog frontmatter. Never inject Archivist frontmatter into GitHub entities or files owned by another workflow.

Required local-source traceability keys are documented in `frontmatter.md`.

## Phase 7 - Local source archiving

Archive only local sources that Archivist owns and that are safe to move under:

`.agent/archive/YYYY/YYYY-MM-DD-original-name.ext`

Use `git mv` when appropriate. Never archive CCPM-owned or other workflow-owned live artifacts merely because Archivist read them.

## Phase 8 - Validation

Run:

```bash
python skills/content/project-documentation-archivist/scripts/validate_catalog.py --project-root .
```

After a GitHub-native migration, require the stricter cutover validation:

```bash
python skills/content/project-documentation-archivist/scripts/validate_catalog.py \
  --project-root . \
  --require-github-native
```

Record validation evidence in `PROCESSING_RUNS.md`.
