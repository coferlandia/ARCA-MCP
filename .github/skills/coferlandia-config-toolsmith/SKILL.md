---
name: coferlandia-config-toolsmith
description: >
  Use when the user or controlling authority explicitly requests coferlandia-config-toolsmith
  for a target repository. Analyzes the repository's existing configuration mechanisms,
  produces a static agent-oriented configuration contract, records ambiguous candidates,
  and deterministically generates or integrates one standardized configuration CLI plus
  complete agent/developer documentation. It adapts to native loaders, stores, precedence,
  validation, and secret providers; it never creates a shadow configuration store or persists
  current/effective values. Explicit invocation only.
license: Apache-2.0
compatibility: >
  Requires read access to the target repository, write access after discovery/classification,
  Python 3.11+ for the deterministic Toolsmith CLI and fallback facade, and git for review.
  Native integration may additionally require the target project's own toolchain.
metadata:
  author: coferlandia
  version: "1.0.0"
  category: meta
  status: active
  tested: "2026-08-03 - activation, contract invariants, candidate regeneration lifecycle, deterministic documentation, wizard fallback, JSON flag compatibility, repository-confined native paths, transactional native-store mutation, plan hash/state rejection, and cross-skill contract tests executed with unittest."
---

## Context

This meta-skill prepares a repository for safe configuration operation by humans and agents.
It does not replace the project's configuration architecture. The project's native loaders,
stores, APIs, precedence, validation, and secret providers remain authoritative.

The governing separation is:

```text
Agentic layer      discovers and decides what configuration means.
Static contract    records approved capabilities and native bindings, never state.
Deterministic CLI  executes approved reads/writes against native mechanisms.
Config DevOps      consumes that interface for day-to-day operation.
```

A canonical key such as `notifications.reminder_lead_minutes` is an interface identifier. It
must translate to an existing native setting; it is never persisted as a second configuration.

## Activation

Activate only when the user or controlling authority explicitly names
`coferlandia-config-toolsmith` or asks for the Config Toolsmith process for a clearly identified
repository. Do not activate merely because configuration is fragmented or a CLI would be useful.

If no target repository is identifiable, stop and report that a target repository is required.

## Non-negotiable invariants

1. **Native configuration is the only runtime source of truth.**
2. **The contract describes behavior, not current state.** Never store current, effective,
   last-seen, per-environment, snapshot, or secret values in it.
3. **No Coferlandia artifact participates in application runtime resolution.** The generated
   operational CLI may read the static contract, but the application must not.
4. **Prefer native APIs/loaders over reimplementing precedence.** Direct file adapters are a
   fallback only when the project exposes no reusable authority.
5. **Ambiguity is omitted, logged, and recoverable.** Never guess a binding to keep moving.
6. **Every discovered signal receives a disposition.** Completion requires zero unclassified
   signals.
7. **Generated documentation is part of the public interface.** A repository-unfamiliar agent
   must be able to operate the CLI without reading implementation code.
8. **Deterministic search is never authoritative.** It may rank candidates but cannot support a
   final negative conclusion by itself.

Read `references/architecture.md` before designing or changing the contract boundary.

## Required disciplines

- Use `superpowers:writing-skills` when available while authoring or changing this skill.
- Use `superpowers:test-driven-development` when changing deterministic CLI behavior.
- Use `superpowers:verification-before-completion` before claiming generation or validation passed.
- Follow the target repository's own development, review, and release rules.

## Workflow

### 1. Study the target repository without modification

Read `references/discovery.md`. Inspect configuration sources, consumers, loaders, validation,
defaults, precedence, setup scripts, deployment manifests, secret providers, existing CLIs/APIs,
documentation, tests, and deprecated aliases. Record evidence and consumers rather than inferring
semantics from names alone.

Produce one inventory row per discovered signal with:

```text
native identifier | source | consumer | apparent type | authority evidence |
write path | secret risk | lifecycle/effects | confidence | disposition
```

Do not modify files during this phase.

### 2. Classify every discovered signal

Assign exactly one disposition:

- `MANAGED`
- `CANDIDATE`
- `INTENTIONALLY_UNMANAGED`
- `READ_ONLY_OR_DERIVED`
- `UNSUPPORTED`
- `OBSOLETE`

A field is `MANAGED` only when meaning, type, authority, readable source, approved writable target,
validation, secret handling, and operational effects are sufficiently established.

When any material point remains uncertain, write a structured candidate and continue. Read
`references/candidates.md` before creating or resolving candidate records.

### 3. Design the static contract

Read `references/contract.md`. Build `.coferlandia/config-toolsmith/contract.yaml` using the
JSON-compatible YAML subset accepted by the deterministic CLI. The contract may describe modules,
fields, native bindings, types, validation, setup levels, effects, user intents, examples, and
recipes. It must not contain a current configuration instance.

Validate before generation:

```bash
python skills/meta/coferlandia-config-toolsmith/scripts/coferlandia-config-toolsmith-cli.py \
  contract validate --contract .coferlandia/config-toolsmith/contract.yaml
```

Any forbidden state-bearing key is a validation failure, not a warning.

### 4. Resolve integration strategy

Read `references/adapters-platforms.md`. Select in order:

1. extend the existing project CLI;
2. generate a native companion CLI in the project's primary platform;
3. generate the Python fallback facade.

Reuse the project's native loader/API whenever practical. If a custom adapter is required, keep it
inside the target repository and make its native authority explicit. Never create a new settings
file merely because it is easier to automate.

### 5. Generate the standardized facade and documentation

Run the deterministic generator:

```bash
python skills/meta/coferlandia-config-toolsmith/scripts/coferlandia-config-toolsmith-cli.py \
  generate --contract .coferlandia/config-toolsmith/contract.yaml \
  --target-root <repo-root> --platform auto --dry-run
```

Review the dry-run, then repeat without `--dry-run`. The generator writes only declared operational
artifacts and reports every path. It does not migrate native configuration values.

Generate documentation from the same contract:

```bash
python skills/meta/coferlandia-config-toolsmith/scripts/coferlandia-config-toolsmith-cli.py \
  docs generate --contract .coferlandia/config-toolsmith/contract.yaml \
  --output-dir docs/configuration
```

Read `references/agent-documentation.md` before accepting generated docs.

### 6. Review candidates without blocking unrelated work

List unresolved candidates:

```bash
python skills/meta/coferlandia-config-toolsmith/scripts/coferlandia-config-toolsmith-cli.py \
  candidates list --candidates .coferlandia/config-toolsmith/candidates.yaml
```

Approve, reject, defer, or mark intentionally unmanaged only with explicit authority. Approval
checks the stored source fingerprint, promotes the approved field into the contract, regenerates the
facade and documentation from that contract, validates immediate drift, appends a decision, and only
then marks the candidate implemented. A stale candidate returns a failure and must be re-analyzed.

```bash
python skills/meta/coferlandia-config-toolsmith/scripts/coferlandia-config-toolsmith-cli.py \
  candidates approve <candidate-id> \
  --candidates .coferlandia/config-toolsmith/candidates.yaml \
  --contract .coferlandia/config-toolsmith/contract.yaml \
  --decisions .coferlandia/config-toolsmith/decisions.yaml \
  --expected-fingerprint <sha256> --target-root <repo-root> \
  --output-dir docs/configuration --platform auto --json
```

### 7. Test behavior and native parity

Read `references/testing.md`. At minimum prove:

- no forbidden state enters the contract;
- every discovered signal is classified;
- managed fields resolve through their native authority;
- native values are read live;
- writes affect only approved native stores/APIs;
- secret values are redacted and use safe input;
- repeated generation is deterministic;
- dry-run changes nothing;
- atomic writes leave no partial files;
- stale plans/candidates are rejected;
- generated docs cover every managed field and command;
- Python and .NET pack outputs satisfy the shared semantic contract where selected.

### 8. Rewire agent/developer entrypoints

Add concise links from the target repository's `AGENTS.md`, README, or equivalent durable entrypoint
to `CONFIG-AGENT-HANDBOOK.md` and the generated CLI. Do not duplicate the complete contract in
multiple documents. Do not instruct agents to edit managed native stores directly.

### 9. Review and report

Inspect the full diff. Confirm the target application still resolves configuration exactly as before
except for explicitly approved native writes. Report coverage, omitted candidates, generated paths,
validation evidence, and limitations. Do not claim that omitted fields are unsupported; report their
disposition and next action.

## Standard target CLI contract

The generated facade must expose semantic equivalents of:

```text
<app> setup [--quick|--reconfigure]
<app> config [<module>]
<app> config modules
<app> config describe <module-or-key>
<app> config get <key>
<app> config show [module]
<app> config set <key> <value>
<app> config unset <key>
<app> config validate [module]
<app> config capabilities
<app> config coverage
<app> config agent-context --all
<app> config prepare-change ...
<app> config apply-plan --plan-file <plan> --expect-hash <sha256> --confirm
```

Conditional capabilities include `change`, `activate`, `rollback`, and `secret set --stdin`.
All operational commands use stable structured output. Mutations require plan/dry-run semantics,
explicit confirmation, plan-hash and state revalidation, repository-confined native paths,
transactional rollback across standard writable artifacts, secret redaction, and post-change
validation.

## Gotchas

- **Treating the contract as configuration:** never put live values in it or make the application
  consume it. The contract is an operational interface description.
- **Reimplementing native precedence:** a generic reader can disagree with framework resolution.
  Prefer the application's loader/API and document it as authority.
- **Guessing an ambiguous binding:** omit it as a candidate, emit a warning, and continue.
- **Calling zero search results unsupported:** search is an optimization only. Read the complete
  generated handbook and all dispositions before a negative conclusion.
- **Passing secrets on the command line:** use stdin or the native secret provider; command history
  and process listings are not secret-safe.
- **Generating a new settings store:** that is a separate configuration-migration project and needs
  explicit approval outside this skill.
- **Silently overwriting hand-edited generated files:** run drift checks and regenerate from the
  approved contract; never merge divergent generated behavior by intuition.

## Expected Output

```text
Config Toolsmith result

Target repository: <path>
Contract: <path> (schema v1)
Integration strategy: existing-cli | native-companion | python-fallback
Native authority reused: <loader/API/store>
Discovered signals: <n>
Managed: <n>
Candidates pending: <n>
Intentionally unmanaged: <n>
Read-only/derived: <n>
Unsupported: <n>
Obsolete: <n>
Unclassified: 0
Generated CLI: <path>
Generated documentation: <paths>
Native stores changed: <paths | none>
Tests and validations: <command -> result>
Remaining review: <candidate ids | none>
Runtime configuration source added by Coferlandia: no
```

## Output Location

Target-repository analysis artifacts default to `.coferlandia/config-toolsmith/`. Generated agent
and developer documentation defaults to `docs/configuration/`. The standardized CLI is integrated
into the project's existing CLI or written under its normal scripts/tools location.

### Output Exceptions

- Existing `AGENTS.md` or README — add only concise links/entrypoints when appropriate.
- Existing native CLI files — may be modified when extension is the approved strategy.
- Existing native configuration stores — changed only by explicit CLI operations, never by
  Toolsmith generation itself.

## Scripts Available

- **`scripts/coferlandia-config-toolsmith-cli.py`** — validates static contracts, manages candidate
  decisions, generates deterministic docs/facades, checks drift, and exposes machine-readable
  capabilities. Run `--help` for the complete command tree.

## References

- Read `references/architecture.md` when resolving source-of-truth, runtime-dependency, generated/
  handwritten, or cross-skill boundaries.
- Read `references/discovery.md` before repository analysis and classification.
- Read `references/contract.md` while authoring or validating the static contract.
- Read `references/adapters-platforms.md` before selecting native integration or implementing an
  adapter/platform pack.
- Read `references/candidates.md` when omitting, reviewing, approving, or invalidating a candidate.
- Read `references/agent-documentation.md` before generating or accepting agent-facing docs.
- Read `references/testing.md` before implementation validation, review, or completion.
