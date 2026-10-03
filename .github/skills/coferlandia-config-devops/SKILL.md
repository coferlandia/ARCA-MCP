---
name: coferlandia-config-devops
description: >
  Use when a user wants to inspect, explain, plan, execute, or be guided through a configuration
  change in a repository prepared by coferlandia-config-toolsmith. Its Config Operator mode maps
  natural-language outcomes to the generated static contract and standardized CLI. Execute Mode
  operates the CLI when access exists; Guide Mode acts as an experienced remote control tower,
  producing exact batched commands for a person with platform access while verifying each material
  safety boundary. Never edits managed native stores directly, invents undocumented fields, or
  treats deterministic search as authoritative.
license: Apache-2.0
compatibility: >
  Requires access to the target repository's generated configuration contract, agent handbook, and
  standardized configuration CLI. Execute Mode additionally requires permission to invoke the CLI
  and access its native configuration mechanisms. Guide Mode requires only the generated contract
  and documentation plus a user/operator able to run commands in the target environment.
metadata:
  author: coferlandia
  version: "1.0.0"
  category: ops
  status: active
  tested: "2026-08-02 - activation, Toolsmith-contract dependency, Execute/Guide separation, non-authoritative search fallback, compact control-tower batching, no-direct-edit, and reporting invariants verified by contract tests."
---

## Context

This skill operates configuration interfaces produced by `coferlandia-config-toolsmith`. It is not
a configuration-discovery or adapter-authoring skill.

```text
User intent -> Config DevOps -> generated agent contract -> standardized CLI -> native config
```

The first operational capability is **Config Operator**, with two modes:

- **Execute Mode:** the agent can invoke the standardized CLI in the target environment.
- **Guide Mode:** the agent cannot operate the environment directly and guides a person or another
  agent who can.

Both modes consume the same contract and CLI. Neither may edit `.env`, JSON, YAML, appsettings,
databases, or secret providers directly when a managed CLI operation exists.

## Mode selection

Choose one mode before operational work:

```text
Can this agent inspect and invoke the standardized CLI in the target environment?
  yes -> Execute Mode
  no  -> Guide Mode
```

The user may force Guide Mode by requesting instructions only, even when execution access exists.
Never imply Execute Mode capability without verifying actual access.

Read `references/operator.md` before every Config Operator run.

## Preconditions

Locate and verify:

- `.coferlandia/config-toolsmith/contract.yaml` or its documented equivalent;
- `docs/configuration/CONFIG-AGENT-HANDBOOK.md`;
- generated CLI entrypoint and `config capabilities` output;
- contract schema compatibility;
- target project/environment identity when execution is contemplated.

If the standardized interface is absent or invalid, stop. Do not improvise a direct native-file edit.
Return the repository to `coferlandia-config-toolsmith` preparation or candidate review.

## Shared Config Operator workflow

### 1. Interpret the desired outcome

Translate the user's language into an outcome, constraints, environment, and acceptable operational
effects. Do not require the user to know canonical field names.

### 2. Discover relevant capabilities

Use deterministic search, intent catalogs, module descriptions, recipes, and structured agent
context to find candidate fields. Search is an accelerator only.

When results are absent, ambiguous, weak, contradictory, cross-module, or would support a negative
conclusion, read the complete `CONFIG-AGENT-HANDBOOK.md` and inspect all dispositions. Read
`references/exhaustive-fallback.md` now.

### 3. Inspect live state

Use the standardized CLI to read effective values, sources, overrides, validation state, and
operational effects. Never substitute values copied from documentation or repository files for live
environment state.

### 4. Build the smallest valid change set

Select only documented writable fields required for the requested outcome. Include dependent fields
when the contract requires them. Secrets use stdin/native providers. Config DevOps cannot promote candidates or
invent missing bindings.

### 5. Prepare and review

Prefer one consolidated `prepare-change` call that batches safe inspection, current values, source
resolution, validation, dependencies, warnings, effects, rollback feasibility, and a state-bound
plan. Explain what will change and what remains unknown.

### 6. Cross the mutation boundary

Execute or guide `apply-plan` only after required authority. The CLI must re-read native state and
reject stale plans. Activation, restart, migration, downtime, or destructive side effects form a
separate approval boundary when declared by the contract.

### 7. Verify and report

Validate effective state after application, report changed native artifacts, effects, activation,
rollback, warnings, and any unresolved candidates. Never claim success from write output alone.

## Execute Mode

Read `references/execute-mode.md`. Execute Mode may invoke the CLI but must not bypass it.

Preferred normal flow:

```bash
<app> config prepare-change --file requested-change.json --output plan.json --json
<app> config apply-plan --plan-file plan.json --confirm --json
<app> config activate --change <id> --confirm --health-check --json  # only when required
```

For a contract-declared low-risk, reversible, no-restart, non-secret field, the CLI may expose one
`config change` operation that inspects, applies, and validates atomically. Risk classification comes
from the contract, not informal agent judgment.

## Guide Mode

Read `references/guide-mode.md`. Guide Mode follows a **control-tower model**: an experienced remote
operator guides someone who has platform access but may have little technical knowledge.

It is not a one-command-per-message ritual. Minimize copy/paste and rounds by batching everything
safe to batch:

1. consolidated prepare/inspection;
2. apply plus post-change validation;
3. activation plus health check only when a material boundary requires separation.

Create another interaction boundary only when new evidence or approval materially affects safety.
Unexpected output triggers a go-around: stop, preserve the last known safe state, and reassess before
issuing another mutation.

Every Guide response states `Execution status: NOT EXECUTED` until the user returns verifiable
output. Never convert an expected result into a claimed result.

## Candidate and unsupported outcomes

A requested field may be:

- managed;
- a pending/stale candidate;
- intentionally unmanaged;
- read-only/derived;
- unsupported by the current adapter;
- truly not configurable.

Only the last conclusion may be reported as not configurable, and only after exhaustive handbook
review. A pending candidate must return to Config Toolsmith for explicit review; Config DevOps cannot
promote it.

## Safety boundaries

Require explicit authority before:

- applying any mutation when the user's request did not already authorize it;
- entering or rotating secrets;
- restarting services or containers;
- running migrations;
- causing downtime;
- changing production access/security/provider configuration;
- applying a plan with warnings whose impact has not been accepted;
- executing rollback that discards subsequent changes.

Read `references/recovery.md` for go-around and recovery behavior.

## Gotchas

- **Editing the native file directly:** forbidden when the standardized facade manages the field.
  The facade owns validation, precedence awareness, stale-state checks, and reporting.
- **Treating search as a router:** a miss is inconclusive, not unsupported. Read the complete handbook.
- **Assuming repository values equal production:** inspect live effective state and source.
- **Giving ten independent commands in Guide Mode:** batch safe diagnostics and validation into
  prepare/apply/activate operations; split only at real safety boundaries.
- **Claiming Guide Mode execution:** always mark commands as not executed until returned evidence is
  inspected.
- **Promoting a candidate:** Config DevOps may identify and explain it, but only Config Toolsmith can
  change the contract.
- **Passing a secret in an argument:** use stdin or the native secret provider.
- **Restarting automatically:** configuration application and activation are separate when downtime
  or other material effects exist.

## Expected Output

### Execute Mode

```text
Config Operator result

Mode: execute
Requested outcome: <normalized intent>
Target: <project/environment>
Contract/CLI: <version>
Fields changed: <key old->new, redacted when secret>
Native artifacts: <paths/providers>
Prepare/dry-run: PASS
Apply: PASS | not run
Post-change validation: PASS | FAIL | not run
Activation: completed | required | not required | not authorized
Rollback: <change id/command | unavailable>
Warnings: <items | none>
Unresolved candidates: <ids | none>
```

### Guide Mode

```text
Config Operator guidance

Mode: guide (control tower)
Execution status: NOT EXECUTED
Requested outcome: <normalized intent>
Known from contract: <facts>
Unknown until live inspection: <facts>
Current safety boundary: prepare | apply | activate | recovery

Run from: <exact location/context>
Command:
<one consolidated command or bounded block>

Mutation: no | yes
Expected evidence: <structured output fields>
Stop conditions: <conditions>
Next decision: <what returned evidence determines>
```

Read `references/reporting.md` before final output.

## Output Location

This skill normally creates no repository artifacts. Execute Mode may create transient change plans
or native audit records through the standardized CLI. Those artifacts are governed by the generated
contract and must never become configuration sources.

## References

- Read `references/operator.md` at the start of every Config Operator run.
- Read `references/execute-mode.md` when direct CLI access is available.
- Read `references/guide-mode.md` when direct access is unavailable or the user requests guidance.
- Read `references/exhaustive-fallback.md` when search is inconclusive or before any negative
  capability conclusion.
- Read `references/recovery.md` after unexpected output, stale plans, failed validation, unsafe
  activation, or a partially applied operation.
- Read `references/reporting.md` before claiming completion or handing instructions to another actor.
