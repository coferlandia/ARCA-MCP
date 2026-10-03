---
name: coferlandia-ci-adapter
description: >
  Use when the user or controlling authority asks to adapt a repository to Coferlandia's generic
  Chat/Local CI controllers, repository-owned remote Development validation, or complete GitHub-native
  release delivery. Study the repository's real validation/CI/release contracts and materialize only
  the approved repository interfaces; do not invent project commands.
license: Apache-2.0
compatibility: >
  Requires read access to the target repository and its validation/workflow documentation, Python 3.11+
  for deterministic adapter tooling, and write access only after the adaptation scope is authorized.
metadata:
  author: coferlandia
  version: "1.4.0"
  category: meta
  status: active
  tested: "2026-09-26 - CI profile, remote Development validation with declarative toolchain setup and exact-head dispatch fallback, configurable publication runners, isolated publication control-plane execution, immutable request retry recovery, and opt-in GitHub-native release publication rendering covered by unittest."
---

## Context

This meta-skill adapts repository-specific facts to shared Coferlandia delivery controllers. It does not run CI itself, does not publish a release, and does not authorize merge.

Keep the three interfaces separate:

```text
Development
  .coferlandia/development/validation.json
  .github/workflows/<development-validation>.yml
        -> source-candidate Development evidence only

Qualification
  .coferlandia/ci/profile.json
        -> Chat ci / local-ci

Publication
  .coferlandia/release/policy.json
  .github/workflows/<release-publication>.yml
        -> coferlandia-release-publisher
```

Do not generate repository-local copies of generic Coferlandia prompts or skills. The repository remains authoritative for commands, services, runner labels, shell, toolchain setup, documentation and policy.

## Activation

Activate when the controlling authority explicitly asks to:

- create or update the Coferlandia CI profile;
- adapt a repository for generic GitHub-native or local Qualification;
- add or repair repository-owned remote Development validation;
- bootstrap the remote Development surface delegated by Chat Coder;
- adapt complete GitHub-native release publication; or
- use `coferlandia-ci-adapter` for a target repository.

Do not activate merely to run tests, diagnose one failure, merge a PR, publish one release, or infer a missing delivery stage.

A Chat Coder delegation to bootstrap the minimum remote Development surface is explicit adaptation authority for that bounded purpose. It is not authority to alter Qualification, publication, deployment, product behavior, or unrelated workflows.

## Repository study

Read `references/discovery.md`. Inspect current repository instructions, development/testing docs, canonical scripts, package/build metadata, required services, current GitHub workflows/checks, runner requirements, release policy and exceptional lanes. Prefer executable current behavior over historical prose.

For remote Development validation, additionally establish:

```text
applicable cheap deterministic Development commands
working directory
required services
environment variable names only (never values)
project-specific runtime/toolchain versions required by those commands
repository-approved setup actions when deterministic job-local provisioning is needed
GitHub Actions availability
approved runner labels
approved shell
Draft-PR event compatibility
whether candidate/setup code can safely execute on the selected runner
```

When GitHub-native release publication is in scope, establish its publication-capable runner label(s) separately. Do not assume `ubuntu-latest` is available and do not infer publication runner labels from Qualification or Development configuration merely because a repository happens to share a runner pool.

Do not modify the target repository during discovery.

## Approval and scope

Propose the exact interface being adapted and its source evidence. Do not guess commands, toolchain versions, setup actions, runner labels, check names, or workflow semantics. Stop for explicit approval before writing unless the controlling invocation already authorized the exact adaptation surface.

Changing one interface does not implicitly authorize changing another.

## CI Qualification profile

The Qualification interface remains:

```text
.coferlandia/ci/profile.json
```

Use the existing `profile validate`, `profile fingerprint`, `profile check`, and `profile render` commands. `.coferlandia/ci/profile.json` remains Qualification-only; never place Development execution or release publication fields there.

## Remote Development validation

Read `references/development-validation-contract.md` and `_protocol/delivery/DEVELOPMENT_VALIDATION.md`.

The canonical repository contract is:

```text
.coferlandia/development/validation.json
```

The standard generated workflow path is:

```text
.github/workflows/coferlandia-development-validation.yml
```

A Development contract owns repository-specific facts:

- repository identity and supporting documentation;
- working directory;
- ordered Development commands;
- required services and environment variable names;
- GitHub PR-event submission plus an optional repository-declared exact-head dispatch fallback for Draft candidates whose normal event path cannot produce fresh evidence;
- exact `pull-request-head` candidate binding;
- runner labels;
- `bash` or `pwsh` shell;
- optional ordered `github.setup` actions for project-specific job-local toolchains;
- one Development gate whose only GREEN conclusion is `success`.

Existing v1 contracts without `github.setup` remain valid. When setup is needed, declare only repository-approved static `owner/repo@ref` actions with flat scalar `with` inputs. The adapter rejects local/dynamic actions, nested or multiline inputs, dynamic expressions, sensitive-key inputs and per-step private-value transport.

Validate/fingerprint an approved source:

```bash
python skills/meta/coferlandia-ci-adapter/scripts/coferlandia-ci-adapter-cli.py development validate \
  --contract <development.json> --json

python skills/meta/coferlandia-ci-adapter/scripts/coferlandia-ci-adapter-cli.py development fingerprint \
  --contract <development.json> --json
```

Preview materialization:

```bash
python skills/meta/coferlandia-ci-adapter/scripts/coferlandia-ci-adapter-cli.py development render \
  --input <approved-development.json> \
  --target-root <repo-root> \
  --dry-run --json
```

After approval, repeat without `--dry-run`.

The generated workflow:

- listens to Draft-PR lifecycle events needed for Development and, only when declared, an exact-head `workflow_dispatch` fallback;
- resolves an exact current Draft PR head before candidate checkout; normal PR events use their event head, while fallback dispatch requires the declared trusted control ref plus PR number/current candidate SHA verification;
- grants candidate-controlled code only `contents: read` GitHub permission;
- uses the repository-declared runner labels and shell;
- records the exact candidate SHA and Development contract fingerprint before project setup/execution;
- executes repository-declared setup actions, when present, in declared order;
- executes only the repository-declared ordered Development commands;
- never emits `READY_FOR_CI`, `READY_FOR_MERGE`, or any Qualification authority.

The renderer never injects secret values. Self-hosted runner isolation, preinstalled tooling and any ambient runner environment remain repository/operator responsibilities.

Setup actions are executable code and remain subject to repository runner-safety policy. Generic runners provide the execution substrate; project-specific runtime versions belong to `github.setup` when deterministic provisioning is needed.

Rendering is deterministic and idempotent: the same semantic contract produces the same fingerprint, stored contract and workflow text. Setup input keys render in deterministic sorted order.

## GitHub-native release publication

Publication adaptation remains opt-in and separate from Development and Qualification. Read `references/publication-contract.md`.

Validate:

```bash
python skills/meta/coferlandia-ci-adapter/scripts/coferlandia-ci-adapter-cli.py publication validate \
  --policy <release-policy.json> --json
```

Render only when explicitly in scope:

```bash
python skills/meta/coferlandia-ci-adapter/scripts/coferlandia-ci-adapter-cli.py publication render \
  --policy <release-policy.json> \
  --target-root <repo-root> \
  --publisher-path <repo-relative-publisher-entrypoint> \
  --workflow-path .github/workflows/<approved-publication-workflow>.yml \
  --runs-on <runner-label> [<runner-label> ...] \
  --dry-run --json
```

Omit `--runs-on` only when repository study confirms the compatible default `ubuntu-latest` is intentionally valid. If current policy already declares `publication.github.runs_on`, rendering without the flag preserves that value. `runs_on` may be one non-empty runner string or a non-empty duplicate-free list of labels.

Publication configuration belongs to `.coferlandia/release/policy.json`, not to either Development or Qualification contracts. The publication workflow retains its exact integrated-SHA, actor-authorization and minimal `contents: write` release transport boundaries and never deploys.

## Drift and consumers

After adaptation, pressure-test the relevant consumers:

- Chat Coder can distinguish local Development execution from repository-owned remote Development execution.
- Remote Development evidence binds to the exact Draft PR head and contract fingerprint.
- Remote Development GREEN is not treated as Qualification GREEN.
- Chat/GitHub-native CI and Local CI still resolve Qualification only from `.coferlandia/ci/profile.json`.
- Exceptional lanes remain externally authorized.
- Publication remains separately resolved, uses its repository-approved runner contract, and fails closed.

Re-run the adapter when any material repository fact changes: Development commands, setup actions/inputs, runner labels, shell, required services/environment names, Development workflow identity, Qualification commands/gates, merge-group semantics, publication policy/runner labels or exceptional lanes. A changed Development fingerprint invalidates older remote Development evidence for the handoff that relied on it.

## Gotchas

- **Copying generic controllers into the project:** prohibited.
- **Using Fast/Qualification CI as Development validation because local execution is unavailable:** prohibited unless that check is independently declared by repository policy as the Development gate.
- **Hardcoding one repository's stack or runner labels into the adapter:** prohibited.
- **Baking every project's runtime versions into the generic runner image:** wrong; declare repository-owned job-local setup when deterministic provisioning is required.
- **Guessing a runtime/toolchain version from the runner:** prohibited; derive it from repository-owned evidence.
- **Using local/dynamic setup actions or private-value inputs in the Development contract:** prohibited.
- **Storing tokens, secret values, credentials or current run results in contracts:** prohibited.
- **Treating a successful remote Development gate as merge authority:** prohibited.
- **Assuming `ubuntu-latest` for publication:** prohibited when repository evidence requires another runner; declare the publication runner explicitly.
- **Inferring runner labels across stages:** prohibited. Development, Qualification and Publication each own their declared execution contract.
- **Auto-authorizing exceptional lanes or release publication:** prohibited.
- **Dispatching the candidate branch as Development control plane:** prohibited; exact-head fallback must execute from the repository-declared trusted control ref and verify the current Draft PR head before checkout.
- **Trusting a stale remote run:** candidate SHA and Development fingerprint must still match.

## Expected Output

```text
Coferlandia repository adapter result
Target repository: <owner/repo>
Development validation: <not requested | existing | adapted>
Development contract: <path | not touched>
Development fingerprint: <sha256 | not applicable>
Development workflow/gate: <path + gate | not applicable>
CI profile: <path | not touched>
CI profile fingerprint: <sha256 | not applicable>
GitHub-native publication: <not requested | disabled | workflow path>
Publication runner: <default ubuntu-latest | repository labels | not applicable>
Unresolved ambiguity: <none | items>
Generic prompt/skill copies generated: no
Validation: <commands/results>
```

## Output Location

Standard target-repository configuration written by this skill is limited to the explicitly approved interfaces:

- `.coferlandia/development/validation.json`
- its approved `.github/workflows/<development-validation>.yml`
- `.coferlandia/ci/profile.json`
- `.coferlandia/release/policy.json`
- its approved `.github/workflows/<publication>.yml`

Discovery/proposal notes remain conversational or use the target repository's approved planning convention.

## Scripts Available

- **`scripts/coferlandia-ci-adapter-cli.py`** — validates, fingerprints, checks and atomically renders CI profiles and remote Development contracts including optional repository-owned toolchain setup; validates/renders optional GitHub-native publication transport including repository-owned runner labels. Run `capabilities --json` for the command catalog.

## References

- Read `references/discovery.md` before repository study.
- Read `references/profile-contract.md` for Qualification profile fields.
- Read `references/development-validation-contract.md` for remote Development adaptation.
- Read `references/publication-contract.md` for GitHub-native publication.
- Read `references/authority-and-staleness.md` when identities or sources conflict.
- Read `references/testing.md` before completion/pressure validation.
