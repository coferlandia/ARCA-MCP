# Remote Development Validation Contract

Use this reference when adapting a repository so Chat Coder can finish Development validation without requiring its active client to execute the repository locally.

The shared semantic authority is `_protocol/delivery/DEVELOPMENT_VALIDATION.md`. This reference describes how `coferlandia-ci-adapter` materializes that contract.

## Repository-owned inputs

Discover, do not invent:

- `repository`: `owner/name`;
- `documentation`: current authoritative development/testing docs;
- `working_directory`: where Development commands run;
- `commands`: ordered cheap deterministic checks required before `READY_FOR_CI`;
- `required_services`: services the selected runner must make available;
- `environment`: variable names only, never values;
- `github.runner_labels`: the exact approved runner selector;
- `github.shell`: `bash` or `pwsh`;
- optional `github.setup`: ordered repository-approved GitHub Actions used to provision the job-local project toolchain before Development commands run;
- optional `github.submission.fallback`: exact-head workflow dispatch mode plus a trusted repository control ref when the normal Draft PR event path cannot produce fresh evidence.

Use the standard gate name `Development Validation / Gate` unless repository policy already owns another Development gate identity. The only supported allowed conclusion in v1 is `success`.

The workflow path must live under `.github/workflows/` and the candidate binding is always `pull-request-head`.

Before declaring commands, compare them with the repository's Qualification/FULL validation. Do not reuse a canonical full-suite command merely because it already exists. Prefer affected/scoped Development routing when available, and fail closed if the changed surface cannot be resolved deterministically. A shared script is valid only when repository policy explicitly owns it for Development and its normal runtime remains suitable for iterative feedback.

## Declarative toolchain setup

`github.setup` is optional, so existing v1 contracts remain valid unchanged. Use it when the repository's Development commands require a project-specific toolchain that should not be permanently baked into a generic runner image, for example Python, Node.js, .NET, Java or Go.

Each setup entry has:

- `name`: unique human-readable step name;
- `uses`: one static external action reference in `owner/repo@ref` form;
- optional `with`: flat action inputs whose values are strings, integers or booleans.

The adapter intentionally does not support local actions, dynamic action references, nested inputs, multiline input values, GitHub expressions, sensitive-key inputs or per-step credentials in this contract. Repository commands remain available for setup that does not fit this bounded action model.

Setup actions execute after exact candidate verification and Development fingerprint recording, and before the first repository Development command. Their declaration participates in the Development fingerprint.

The adapter does not decide which language/runtime/version a repository needs. Discover those versions from repository-owned manifests, workflows and development documentation.

## Example semantic input

```json
{
  "schema_version": 1,
  "repository": "example/repo",
  "documentation": ["AGENTS.md", "docs/development.md"],
  "working_directory": ".",
  "commands": [
    {
      "id": "validate",
      "command": "bash scripts/validate-development.sh",
      "purpose": "Run scoped Development validation"
    }
  ],
  "required_services": [],
  "environment": [],
  "github": {
    "submission": {
      "mode": "existing-pr-events",
      "workflow": ".github/workflows/coferlandia-development-validation.yml",
      "fallback": {
        "mode": "workflow-dispatch-exact-head",
        "control_ref": "dev"
      }
    },
    "candidate_binding": "pull-request-head",
    "runner_labels": ["self-hosted", "Linux", "ARM64", "coferlandia-ci", "docker"],
    "shell": "bash",
    "setup": [
      {
        "name": "Set up Python",
        "uses": "actions/setup-python@v5",
        "with": {
          "python-version": "3.12"
        }
      },
      {
        "name": "Set up Node",
        "uses": "actions/setup-node@v4",
        "with": {
          "node-version": 22,
          "cache": "npm",
          "cache-dependency-path": "apps/frontend/package-lock.json"
        }
      }
    ],
    "gate": {
      "name": "Development Validation / Gate",
      "allowed_conclusions": ["success"]
    }
  }
}
```

`development render` adds the deterministic `fingerprint` and writes both the contract and workflow.

## Exact-head fallback

The fallback is optional and does not replace the normal PR-event path. When declared, callers dispatch the workflow at exactly `fallback.control_ref` and pass fixed `pr_number` and `candidate_sha` inputs. The trusted workflow re-reads the pull request before candidate checkout and fails unless it is open, Draft, and its current head matches the requested SHA. Dispatching the workflow from the candidate branch is invalid.

The fallback needs only read access to pull-request metadata in addition to `contents: read`. It executes the same fingerprinted setup/actions/commands and gate as PR-event Development validation.

## Safety

The generated workflow uses `pull_request` Draft lifecycle events and optional trusted exact-head `workflow_dispatch`, never `pull_request_target`. Candidate code receives no GitHub write permission. The adapter does not inject repository credentials or private values.

Setup actions are still executable code. Use only repository-approved static action references, and pin stronger immutable refs when repository policy requires them. Do not choose a self-hosted runner unless repository policy explicitly allows candidate code and declared setup actions to execute there. Runner isolation and ambient host environment remain operator responsibilities.

The generic runner should provide the execution substrate required by the repository's runner contract. Project-specific language/runtime versions belong in `github.setup` when the repository needs deterministic job-local provisioning.

## Idempotence and drift

The fingerprint excludes only the stored `fingerprint` field itself. Reordering JSON object keys does not change it. Any semantic change to commands, setup actions/inputs, runner labels, shell, paths, services, environment names or gate identity changes the fingerprint.

Re-rendering the same input produces the same stored contract and workflow. Setup input keys render in deterministic sorted order. Chat Coder must reject remote evidence bound to an older fingerprint or candidate SHA.

## Stage boundary

This surface is Development-only. Do not encode Qualification gates, merge-group behavior, release publication, deployment or exceptional-lane authority here. Those remain owned by their existing contracts.
