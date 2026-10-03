---
name: project-evangelist
description: >
  Use when a software repository needs developer documentation for onboarding,
  technical orientation, technology-stack discovery, architecture explanation, repository
  mapping, main-flow documentation, common change paths, or contributor guidance. Also use
  when existing project docs must be reorganized into a progressive learning structure.
license: Apache-2.0
compatibility: >
  Requires read access to the target repository and its documentation. File changes require
  write access; GitHub evidence requires authenticated repository access when the relevant
  Issues or pull requests are not public.
metadata:
  author: coferlandia
  version: "1.0"
  category: content
  status: active
  tested: "2026-07-31 - validated with validate_skill.py contract coverage and natural activation pressure cases."
---

## Context

`project-evangelist` turns verified repository knowledge into a progressive documentation
experience for current and prospective developers. It explains the project without becoming
marketing copy and remains useful as plain Markdown.

This skill does not own durable project memory. `project-documentation-archivist` owns the
canonical project truth in `README.md`, `AGENTS.md`, `DECISIONS.md`, and `RUNBOOK.md` plus its
traceability catalog. Evangelist consumes those files when present, checks them against the
current implementation, and builds developer-oriented explanations under `docs/**`.

Version 1 does **not configure** MkDocs, GitHub Pages, Docusaurus, VitePress, Sphinx, themes,
search, publishing workflows, or any other documentation platform.

## Prerequisites

- Work from the intended repository root.
- Inspect Git status and preserve unrelated changes.
- Read existing project documentation before proposing a replacement structure.
- When Archivist canonical files exist, read `references/archivist-boundary.md` before deciding
  where information belongs.

## Steps

### 1. Perform preflight without modifying files

1. Resolve the project root, current branch or worktree, and dirty state.
2. Detect existing documentation entrypoints and directories.
3. Detect `README.md`, `AGENTS.md`, `DECISIONS.md`, `RUNBOOK.md`, and Archivist catalog files.
4. Detect documentation tooling only to preserve compatibility; do not add or reconfigure it.
5. Record the preflight in `.agent/project-evangelist/evidence-map.md`.

Do not write project documentation during this phase.

### 2. Inventory implementation evidence

Inspect the sources that reveal how the project actually works:

- dependency manifests, lockfiles, language and runtime configuration;
- application, API, frontend, worker, scheduler, and CLI entrypoints;
- database models, schemas, migrations, queues, authentication, and integration clients;
- container, environment, CI, testing, logging, observability, and deployment definitions;
- existing Markdown and relevant GitHub evidence when required to resolve current behavior.

Ignore generated output, vendor directories, caches, binaries, obsolete plans, and speculative
future architecture. A closed Issue is not implementation proof by itself.

### 3. Build a verified project model

Classify each material finding as `confirmed`, `strongly-supported inference`, `unconfirmed`, or
`contradictory`. Present only confirmed findings as facts.

Build the following models when the repository supports them:

- **Product:** purpose, users, main capabilities, current scope, maturity, limitations.
- **Technology:** languages, frameworks, persistence, migrations, queues, authentication,
  integrations, containers, testing, observability, and confirmed deployment environment.
- **Architecture:** runtime components, responsibilities, dependency direction, communication,
  persistence boundaries, trust boundaries, and external systems.
- **Domain:** terms, entities, relationships, states, invariants, and business rules.
- **Codebase:** semantic repository areas, entrypoints, configuration, tests, and sensitive zones.
- **Flows:** the most important end-to-end functional and technical paths.
- **Development:** setup, run, build, test, lint, typecheck, migration, and change conventions.

Read `references/evidence-and-validation.md` when evaluating technologies, commands, conflicting
sources, or implementation-versus-planning evidence.

### 4. Define developer audiences and reading paths

Identify reader goals instead of relying only on job titles. Typical paths include:

```text
New to the project
→ Project overview
→ Technology at a Glance
→ Architecture overview
→ Repository map
→ Local development
```

```text
Implementing a feature
→ Relevant domain concepts
→ Main system flow
→ Relevant component
→ Common change path
→ Testing
```

```text
Investigating a bug
→ Main flow
→ Component responsibilities
→ Integrations and diagnostics
→ Tests
→ RUNBOOK
```

### 5. Produce a documentation proposal

Copy `assets/documentation-proposal.template.md` to
`.agent/project-evangelist/documentation-proposal.md` and complete it with:

- project understanding and evidence;
- target developer audiences and reading paths;
- existing-documentation assessment;
- files to create, update, preserve, link, or flag as obsolete;
- an adaptive target structure;
- contradictions, unknowns, and Archivist handoffs.

Read `references/documentation-model.md` when designing the entrypoint, progressive depth,
reading paths, page boundaries, or optional Diátaxis classification.

### 6. Stop at the approval gate

Do not create, delete, move, or rewrite project documentation until the control authority approves
the proposal. A previously supplied plan counts as approval only when it is detailed enough to
identify the target structure and documents.

If the proposal changes materially during implementation, record the deviation and obtain renewed
approval before expanding scope.

### 7. Author the approved documentation

1. Create or improve one clear documentation entrypoint, normally `docs/index.md`.
2. Begin with what the project does, then add a verified **Technology at a Glance** and a separate
   **Architecture at a Glance**.
3. Write from orientation toward deeper architecture, domain, codebase, flows, development, and
   reference material.
4. Create only justified documents and directories; remove unsupported template sections.
5. Preserve valuable human-authored material and link to canonical sources instead of duplicating
   them.
6. Use repository-relative links, strict heading hierarchy, consistent terms, and verified examples.
7. Add repository-specific common change paths when they help feature, debugging, integration, or
   contribution work.

Use `assets/docs-index.template.md` as a starting point, not as a mandatory page shape.

### 8. Validate and report

Validate the approved documentation against `references/evidence-and-validation.md`:

- claims and technologies have evidence;
- technology and architecture remain separate;
- planned behavior is not described as implemented;
- links, paths, headings, code fences, images, and commands are valid;
- reading paths reach the material they promise;
- terminology matches the repository;
- no duplicate backlog, project history, publishing platform, or unrelated code change was added;
- Archivist-owned files were not silently rewritten.

Write `.agent/project-evangelist/validation-report.md` and finish with the expected output below.

## Skill maintenance

When changing this skill itself, use `superpowers:writing-skills`: run the positive and negative
pressure prompts in `tests/cases.json`, capture the baseline behavior before the change, make the
smallest instruction change that closes the observed gap, rerun the cases and contract tests, and
refactor only while the tests remain green.

## Gotchas

- **Confusing Evangelist with Archivist:** do not turn `docs/**` into a second durable-memory or
  work-state database. Link to canonical knowledge and hand durable gaps back to Archivist.
- **Writing promotional claims:** enthusiasm never overrides evidence. State confirmed limitations
  and omit unsupported capabilities.
- **Treating a dependency name as proof of implementation:** confirm actual configuration, imports,
  entrypoints, or runtime usage before documenting a technology.
- **Installing a ceremonial directory tree:** adapt the structure to the repository; never create
  empty tutorial, guide, explanation, or reference folders merely to imitate a framework.
- **Copying canonical documents:** link to `DECISIONS.md` or `RUNBOOK.md` instead of cloning deep
  rationale or operations into multiple pages.
- **Presenting a historical plan as current behavior:** verify merged code and current configuration.
- **Writing before study is complete:** repository discovery and the proposal precede project-doc
  modifications.
- **Publishing unverified commands:** run commands when practical or trace them to authoritative
  repository configuration; otherwise label them unconfirmed.
- **Expanding into publishing infrastructure:** V1 may preserve existing tooling but must not
  configure MkDocs, Pages, themes, or deployment workflows.

## Expected Output

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

## Output Location

Analysis, proposal, and validation artifacts go to:

```text
.agent/project-evangelist/
```

### Output Exceptions

- `docs/**` — approved developer-facing repository documentation.

`README.md`, `AGENTS.md`, `DECISIONS.md`, and `RUNBOOK.md` remain Archivist-owned by default. A
change to one of them requires an explicit approval-gated exception or an Archivist handoff.

## References

- Read `references/documentation-model.md` when designing or reorganizing the documentation
  architecture, entrypoint, reading paths, page types, or progressive levels.
- Read `references/archivist-boundary.md` when Archivist canonical files exist, ownership is
  ambiguous, or a proposed document risks duplicating durable knowledge or GitHub work state.
- Read `references/evidence-and-validation.md` when studying repository evidence, verifying stack
  claims or commands, resolving contradictions, and performing final validation.
