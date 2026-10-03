# Developer Documentation Model

Use this reference to design the documentation experience after repository study.

## Core principle

Organize documentation around developer understanding and reader intent. Start with a compact
orientation, then link progressively into architecture, domain, codebase, flows, development, and
reference detail. A directory hierarchy is an implementation detail, not the goal.

## Progressive levels

### Level 0 — Orientation

A developer should learn in under a minute:

- what the project does;
- its current scope and important limitations;
- the verified technology stack;
- the main runtime shape;
- where to continue for a specific goal.

Place this material in the main documentation entrypoint, normally `docs/index.md`.

### Level 1 — General understanding

Explain:

- architecture overview and major components;
- core domain concepts;
- main functional and technical flows;
- external systems and boundaries.

### Level 2 — Development understanding

Explain:

- semantic repository map and module responsibilities;
- local setup and validation;
- persistence, integrations, background processing, and frontend/backend boundaries;
- repository-specific common change paths.

### Level 3 — Detailed consultation

Provide exact APIs, configuration keys, schemas, commands, formats, and deep procedures only where
they are justified and maintainable.

## Documentation entrypoint

The entrypoint is a map, not a complete manual. Include supported sections from:

1. What This Project Does.
2. Technology at a Glance.
3. Architecture at a Glance.
4. Main Capabilities.
5. Start Here reading paths.
6. Project Map.
7. Known Limitations.
8. Further Reading.

Remove unsupported sections instead of filling them with generic prose.

## Technology at a Glance

Summarize the implemented stack by responsibility, for example:

```markdown
- **Backend:** FastAPI on Python 3.12.
- **Database:** PostgreSQL with SQLAlchemy and Alembic.
- **Frontend:** React with TypeScript and Tailwind CSS.
- **Background processing:** Celery with Redis.
- **Local development:** Docker Compose.
- **Testing:** Pytest, Vitest, and Playwright.
```

Use versions only when current repository evidence confirms them. Omit categories that do not apply.

## Architecture at a Glance

Explain how components collaborate rather than repeating the technology list. Identify component
responsibilities, protocol or queue boundaries, persistence, external systems, and synchronous versus
asynchronous paths. Use a compact text or Mermaid diagram only when it materially improves clarity and
the repository already accepts that format.

## Reading paths

Each path begins with a developer goal and contains the minimum useful sequence of documents. Common
goals include evaluation, local setup, feature work, debugging, integration, frontend work, data-model
changes, background tasks, and contribution.

A path is invalid when it promises a destination that does not exist or requires unexplained context.

## Adaptive structures

A small project may need only:

```text
docs/
├── index.md
├── architecture.md
├── development.md
└── contributing.md
```

A complex project may justify:

```text
docs/
├── index.md
├── getting-started/
├── architecture/
├── domain/
├── codebase/
├── guides/
├── reference/
└── contributing/
```

Do not create empty directories or split a coherent page solely to satisfy this example.

## Main documentation areas

### Project overview

State purpose, users, implemented capabilities, scope, maturity, and confirmed limitations. Avoid
release chronology and detailed rationale.

### Architecture

Explain components, responsibilities, boundaries, communication, persistence, trust, and important
trade-offs. Link to `DECISIONS.md` for deep rationale.

### Domain

Explain terms, entities, relationships, states, invariants, and business rules that a developer must
understand before changing code.

### Repository map

Describe the meaning of important directories and modules. A raw `tree` dump is not documentation.
Tie locations to responsibilities and entrypoints.

### Main flows

Trace representative end-to-end paths from trigger to result. Include synchronous/asynchronous
boundaries, persistence, external calls, and important failure behavior when confirmed.

### Development

Document verified setup, run, build, test, lint, typecheck, migrations, fixtures, debugging, and
configuration conventions.

### Common change paths

For recurring changes, name all relevant impact areas. Examples include API endpoint, frontend page,
scheduled task, migration, integration client, event consumer, or authentication rule. These are
repository-specific maps, not generic tutorials.

## Diátaxis

Tutorial, how-to, explanation, and reference are optional page classifications inside this broader
model. Use them when they clarify reader intent. Do not treat them as abstraction levels and do not
force all four categories into every repository.

## Page-level writing rules

- Begin with purpose and required context.
- Put the essential conclusion before deep detail.
- Maintain strict heading hierarchy.
- Use consistent project terminology.
- Link prerequisites and deeper sources.
- Prefer semantic prose over raw file listings.
- Use examples only when supported by repository evidence.
- Keep warnings, required steps, and critical constraints visible; do not hide them in accordions.
