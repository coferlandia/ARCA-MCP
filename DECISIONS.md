# Decisions

## Active Records

## DECISION-20261003-001 - GitHub is the operational source of truth

Status: accepted
Date: 2026-10-03
Area: docs

Context:
The repository uses GitHub Issues, Pull Requests and Git history to represent planned, active and completed work. Project Archivist requires durable knowledge to remain distinct from operational work state.

Decision:
Use GitHub Issues/Projects for work state and Git/PRs for implementation history. Repository documentation stores only durable current-state facts, rationale, agent constraints and repeatable operations. Do not create or maintain `TODO.md`, `HISTORY.md` or a local open-question backlog.

Alternatives considered:
- Maintain a parallel Markdown backlog/history in the repository.

Reasons:
- Avoids two competing sources of truth.
- Keeps lifecycle state attached to the GitHub entities where implementation/review evidence exists.

Consequences:
- Material unresolved questions that require action must become GitHub Issues.
- Archivist catalogs may link GitHub entities but must not mirror their full state.

Trade-offs:
- Reading historical context may require following Issue/PR/Git links rather than one chronological Markdown file.

Sources:
- `.github/skills/project-documentation-archivist/SKILL.md`
- `.github/skills/project-documentation-archivist/references/catalog-files.md`

Related:
- Issue: #31

## DECISION-20261003-002 - The public repository is not a production deployment authority

Status: accepted
Date: 2026-10-03
Area: infra

Context:
dcARCA is public/open source while deployment-specific infrastructure, secrets and private network information belong to environments operated by consumers.

Decision:
`main` and repository workflows may build, test and audit the project, but production deployment authority and private infrastructure configuration stay outside this repository. Generic Docker/deploy examples are allowed only when they contain no private hosts, networks or secrets.

Alternatives considered:
- Keep production deploy workflows and environment details in the public repository.

Reasons:
- Reduces accidental disclosure and coupling between reusable library/server code and one operator's infrastructure.
- Keeps deployment secrets and topology under the host's security boundary.

Consequences:
- Certificates, PFX/private keys, passwords, API keys, SSH keys and private topology must be injected externally.
- CI rejects tracked certificate/private-key file extensions.

Trade-offs:
- Production deployment requires a separate operator-controlled process.

Sources:
- `docs/REPOSITORY_SECURITY.md`
- `.github/workflows/ci.yml`

## DECISION-20261003-003 - Fiscal emission retries are durable and fail closed

Status: accepted
Date: 2026-10-03
Area: backend

Context:
A transport failure around `FECAESolicitar` can leave the caller uncertain whether ARCA authorized a comprobante. Retrying with a fresh number can duplicate a fiscal side effect.

Decision:
Recommended MCP emission tools require a durable `idempotencyKey`, persist the assigned fiscal identity before the side effect, and reconcile `Submitting`/`Uncertain` operations by consulting the exact same comprobante. Fiscal mismatch or insufficient evidence remains uncertain; it never triggers a second automatic `FECAESolicitar`.

Alternatives considered:
- Stateless retry that requests the next number after transport failure.
- Treat matching number/CAE alone as sufficient recovery evidence.

Reasons:
- Protects against duplicate fiscal emission.
- Preserves a stable operation identity across process restarts and API-key rotation.
- Requires comparable fiscal evidence before declaring recovery successful.

Consequences:
- `Authorized` and `FiscalRejected` are terminal replays.
- Pending operations retain authority over their series and may block another key.
- Correcting a fiscally rejected request requires a new operation/key.

Trade-offs:
- Ambiguous outcomes intentionally block progress until they can be reconciled rather than guessing.

Sources:
- `docs/MCP_IDEMPOTENCY.md`
- `docs/MCP_SERVER.md`

## DECISION-20261003-004 - One writer process owns a filesystem emission store

Status: accepted
Date: 2026-10-03
Area: architecture

Context:
Fiscal numbering must be serialized per canonical series while the current persistence mechanism is filesystem-based.

Decision:
ARCA-MCP V1 supports a single writer process per emission store. A writer lease is acquired before readiness. The canonical series is `(environment, represented CUIT, point of sale, voucher type)`; distinct series may progress independently inside that writer.

Alternatives considered:
- Multiple writer processes sharing the same filesystem store without distributed coordination.

Reasons:
- Keeps numbering authority deterministic with the persistence model actually implemented.
- Avoids presenting a local filesystem lock as distributed coordination.

Consequences:
- A second process for the same store fails with `SERIES_WRITER_BUSY`.
- External systems using the same point of sale are outside the lease and can cause drift; operationally the point of sale should be exclusive to the service.

Trade-offs:
- Horizontal writer scaling requires a future distributed coordination design or dedicated fiscal series.

Sources:
- `docs/MCP_IDEMPOTENCY.md`
- `docs/MCP_SERVER.md`

## DECISION-20261003-005 - PDF rendering is downstream of fiscal authorization

Status: accepted
Date: 2026-10-03
Area: backend

Context:
Document rendering through `creadorpdf` can fail independently after ARCA has already authorized a comprobante.

Decision:
Treat PDF rendering as a post-fiscal side effect. A result with fiscal authorization and PDF failure still represents an existing invoice. A retry with the same fiscal `idempotencyKey` reuses the existing authorization and retries only document rendering. Regeneration of an existing document starts from `FECompConsultar` and must never emit.

Alternatives considered:
- Roll back or repeat fiscal emission when PDF rendering fails.
- Include presentation/template data in the fiscal idempotency fingerprint.

Reasons:
- Fiscal authorization cannot be undone by a document-render failure.
- Presentation concerns must not alter fiscal operation identity.

Consequences:
- Fiscal fingerprint deliberately excludes template/PDF presentation data.
- Consumers must distinguish fiscal success from document-render success.

Trade-offs:
- Callers need explicit handling of partially successful `Fiscal=Authorized, PDF=Failed` outcomes.

Sources:
- `docs/MCP_IDEMPOTENCY.md`
- `docs/MCP_SERVER.md`

## DECISION-20261003-006 - Agent skill families coexist without replacing each other

Status: accepted
Date: 2026-10-03
Area: operations

Context:
The project uses both the Superpowers toolkit and Coferlandia Agent Skills. `.agents` is already a git submodule pinned to `obra/superpowers`.

Decision:
Keep Superpowers under the `.agents` submodule and vendor Coferlandia skills under `.github/skills/`. Agents select and apply the skill that owns the requested workflow instead of flattening or overwriting one toolkit with the other.

Alternatives considered:
- Replace the `.agents` submodule with copied Coferlandia skills.
- Copy both toolkits into one unmanaged directory.

Reasons:
- Preserves upstream Superpowers as an independently pinned dependency.
- Avoids path collision while keeping Coferlandia project skills directly available in the repository.

Consequences:
- Changes to either skill family must respect its own update mechanism.
- Project guidance should reference both locations.

Trade-offs:
- Agents must understand two skill roots rather than one.

Sources:
- `.gitmodules`
- `.github/skills/project-documentation-archivist/SKILL.md`

Related:
- PR: #30
