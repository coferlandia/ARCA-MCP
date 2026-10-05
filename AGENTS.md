# AGENTS.md

## Critical Instructions for Agents

- Read this file before modifying the project, then review `README.md`, `DECISIONS.md`, and `RUNBOOK.md` for durable project context.
- Inspect the relevant GitHub Issue/PR before implementing or debugging tracked work. GitHub Issues/Projects own operational work state; do not create `TODO.md` or `HISTORY.md` as a parallel backlog.
- Preserve fiscal correctness over convenience. Never retry an uncertain fiscal emission by requesting a new CAE; follow the idempotency/reconciliation contracts documented under `docs/`.
- Do not change public MCP contracts, fiscal-context identity, idempotency semantics, numbering/concurrency, PDF fiscal snapshot rules, authentication/scopes, or persistence formats without checking the related documentation and durable decisions first.
- Never commit certificates, PFX/private keys, passwords, API keys, WSAA token/sign values, deployment hosts, private network details, or production secrets.
- The public repository is not a production deployment authority. Product/runtime changes must remain independent of private infrastructure.
- Run the repository CI-equivalent build, package audit, and tests before considering a code change complete.

## Project Essentials

### Architecture

- `dcArca.Core`: reusable ARCA integration library for WSAA, WSFEv1 and taxpayer registry operations.
- `dcArca.McpServer`: stateless HTTP MCP surface over `dcArca.Core`, with Bearer API keys/scopes, durable fiscal idempotency, fiscal contexts, numbering coordination, reconciliation and PDF integration.
- `dcArca.Cli`: administrative CLI, including API-key lifecycle operations.
- `dcArca.Core.Tests` and `dcArca.McpServer.Tests`: automated validation suites.
- `dcArca.TestApp`: Windows/WinForms-oriented sample/test application.
- `deploy/`: deploy-oriented assets that must remain generic/public-safe unless documentation explicitly states otherwise.

Important boundaries:

- Business/tenant state belongs to consumers such as SecretarIA; ARCA-MCP persists only the minimum state needed to protect fiscal side effects and its own authorization/configuration responsibilities.
- Recommended emission flows are server-numbered and idempotent. `solicitar_cae` is not the normal retry path.
- PDF generation is downstream from fiscal authorization. A PDF failure must not cause a second fiscal emission.
- The filesystem emission store supports one writer process per store; it is not a distributed multi-host coordinator.
- `creadorpdf` is an external document-rendering integration. Fiscal truth comes from the ARCA-backed fiscal snapshot, not caller-provided presentation data.

### Main Conventions

- Target runtime/tooling is .NET 10 for product projects; CI also installs .NET 8 where needed by dependencies/tooling.
- Keep public contracts backward-compatible unless a tracked change explicitly authorizes a breaking evolution.
- Keep deterministic fiscal/authorization behavior in code rather than delegating it to callers or LLM interpretation.
- Configuration examples stay generic. Runtime secrets are injected by the host/environment and never committed.
- Project-level Agent Skills currently coexist in two locations: Superpowers is pinned as the `.agents` submodule; Coferlandia skills are vendored under `.github/skills/`.

### Sensitive Areas

- Public contracts: MCP tool schemas, error codes, fiscal snapshot and context contracts.
- Persistence/migrations: emission idempotency store, fiscal contexts, API-key data and series reservations.
- Security/authentication: Bearer API keys, scopes, certificate handling and repository-secret boundaries.
- Fiscal side effects: WSAA/WSFEv1 calls, numbering, retries, uncertain outcomes and reconciliation.
- Third-party integration: `creadorpdf` template/render lifecycle and PDF retry behavior.

### Validation Commands

Run from repository root. These mirror `.github/workflows/ci.yml`:

```bash
dotnet restore dcArca.Core.Tests/dcArca.Core.Tests.csproj
dotnet restore dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj
dotnet build dcArca.Core.Tests/dcArca.Core.Tests.csproj --configuration Release --no-restore
dotnet build dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj --configuration Release --no-restore
dotnet build dcArca.Cli/dcArca.Cli.csproj --configuration Release
dotnet list dcArca.Core/dcArca.Core.csproj package --vulnerable --include-transitive
dotnet list dcArca.McpServer/dcArca.McpServer.csproj package --vulnerable --include-transitive
dotnet list dcArca.Cli/dcArca.Cli.csproj package --vulnerable --include-transitive
dotnet test dcArca.Core.Tests/dcArca.Core.Tests.csproj --configuration Release --no-build
dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj --configuration Release --no-build
```

Repository-secret guard used by CI:

```bash
git ls-files | grep -Ei '\.(pfx|p12|p8|key|pem)$'
```

The command must produce no tracked sensitive-key/certificate files.

## Documentation Index

### Start Here

- `README.md`: confirmed public project state, setup and examples.
- `docs/MCP_SERVER.md`: MCP architecture, configuration, tools and operational contract.
- `AGENTS.md`: minimum safe orientation for agents.

### Durable Knowledge

- `DECISIONS.md`: durable architecture/technical rationale distilled from implemented behavior and tracked work.
- `RUNBOOK.md`: repeatable build, operation, health, troubleshooting and maintenance procedures.

### Detailed Technical Contracts

- `docs/MCP_AUTHORIZATION.md`: API-key/scopes behavior.
- `docs/MCP_CONFIGURATION.md`: runtime configuration model.
- `docs/MCP_FISCAL_CONTEXTS.md`: fiscal-context identity and lifecycle.
- `docs/MCP_IDEMPOTENCY.md`: durable emission idempotency, replay and series ownership.
- `docs/MCP_NUMBERING.md`: numbering/concurrency behavior.
- `docs/WSFE_RECONCILIATION.md`: uncertain-outcome reconciliation.
- `docs/WSAA_CACHE.md`: WSAA token-cache behavior.
- `docs/CREADORPDF_INTEGRATION.md`: external PDF integration.
- `docs/REPOSITORY_SECURITY.md`: public-repository security boundary.

### Work Tracking

- GitHub Issues: planned, active, blocked and completed project work.
- Pull Requests and Git history: implementation/review evidence.
- Do not mirror this state into repository-local TODO/HISTORY files.

### Agent Tooling

- `.agents/`: pinned `obra/superpowers` submodule.
- `.github/skills/`: vendored Coferlandia Agent Skills.
- Apply the relevant skill contract when the requested work matches one of these operational workflows.

### Archivist Traceability

- `.agent/catalog/SOURCE_INDEX.md`: sources processed by Project Archivist.
- `.agent/catalog/PROCESSING_RUNS.md`: append-only Archivist processing log.

## Maintenance Notes

- Keep this file brief and operational; deep rationale belongs in `DECISIONS.md` and deep procedures in `RUNBOOK.md` or the focused `docs/` contract.
- Preserve semantic content when reorganizing this file.
- Update validation commands when CI changes.
- Material unresolved work belongs in GitHub Issues, not in this file.
- Do not recreate `TODO.md`, `HISTORY.md`, or `.agent/catalog/OPEN_QUESTIONS.md` for normal work tracking.
