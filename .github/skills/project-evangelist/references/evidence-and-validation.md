# Evidence and Validation

Use this reference during repository study and before declaring the documentation complete.

## Evidence priority

Prefer evidence in this order when determining implemented behavior:

1. Current executable code, configuration, schema, and migrations.
2. Tests that exercise the current implementation.
3. Build, container, CI, and dependency manifests.
4. Current canonical project documentation.
5. Merged PR and commit evidence.
6. Open or closed Issues, plans, and historical notes.

Lower-priority evidence may explain intent but must not override current implementation without an
explicitly unresolved contradiction.

## Finding states

- **Confirmed:** directly supported by current implementation or authoritative configuration.
- **Strongly-supported inference:** multiple current signals agree, but direct execution or contract
  evidence is absent. Label it or omit it.
- **Unconfirmed:** plausible but not sufficiently supported. Do not present as fact.
- **Contradictory:** credible sources disagree. Report the conflict and its impact.

## Technology verification

A package name alone is insufficient. Confirm actual use through one or more of:

- imports or application entrypoints;
- framework/bootstrap configuration;
- runtime composition or container wiring;
- migration or schema tooling;
- executed tests;
- CI commands;
- lockfile plus code usage.

Distinguish production, development-only, test-only, optional, and legacy dependencies.

## Implementation versus planning

Do not convert requested or planned behavior into present-tense documentation. A closed Issue is not
proof that the merged implementation matches the request. Inspect the linked PR, commit, tests, or current
repository state when the distinction matters.

## Command verification

For every documented command:

1. Trace it to an authoritative script, manifest, Makefile, task runner, container definition, or existing
   confirmed documentation.
2. Execute it when practical and safe.
3. Record environment prerequisites and relevant working directory.
4. If it cannot be verified, label it unconfirmed or omit it.

Never invent a convenient command from framework convention.

## Link and path checks

Validate all new or changed documentation links:

- repository-relative target exists;
- case matches the filesystem;
- anchor resolves when practical;
- image path exists;
- no link points to a temporary analysis artifact as permanent documentation;
- reading paths contain no missing page.

## Markdown checks

- one H1 per page unless repository convention explicitly differs;
- headings do not skip levels without reason;
- code fences are balanced and language tags are reasonable;
- mandatory warnings and steps are visible;
- examples do not contain secrets, PII, private URLs, or production credentials.

## Structural validation

Confirm that:

- one entrypoint clearly orients developers;
- `Technology at a Glance` is verified and compact;
- `Architecture at a Glance` describes organization rather than duplicating the stack list;
- the documentation structure is proportional to project complexity;
- no empty ceremonial directories were created;
- human-authored material was preserved or intentionally superseded with approval;
- no duplicate durable-memory or GitHub work-state system exists;
- V1 did not add documentation publishing infrastructure.

## Completion report

```text
Project Evangelist result

Repository studied: {path or repository}
Documentation entrypoint: {path}
Documents created: {paths or none}
Documents updated: {paths or none}
Documents preserved: {paths or none}
Reading paths added: {summary}
Technology summary: {verified stack summary}
Architecture summary: {verified component summary}
Unconfirmed or contradictory findings: {items or none}
Archivist handoffs: {items or none}
Validation: PASS | FAIL ({details})
Suggested commit message: {message}
```
