# Configuration

Run `init-config` to create `.project-orchestrator/config.json` without overwriting an existing
file, then run `validate-config`.

Configuration schema version 2 covers:

- Epic-scoped Git/worktree behavior;
- role-specific primary/fallback provider, model, and reasoning settings;
- retry and timeout policy;
- deterministic validation commands;
- protocol/evidence retention;
- optional GitHub Project coordinates and logical-to-Project `Status` mapping.

Retired v1 phase fields such as `one_commit_per_phase`, `candidate_commit_strategy: amend`, and
per-phase merge/worktree cleanup are not v2 execution semantics. `load_config` can normalize a v1
file for migration, but new configuration must use v2.

GitHub Project configuration is optional. A representative block is:

```json
{
  "github_project": {
    "owner": "diegocofre",
    "number": 2,
    "status_mapping": {
      "pending": "Todo",
      "in_progress": "In Progress",
      "review": "Review",
      "blocked": "Blocked",
      "done": "Done"
    }
  }
}
```

The `in_progress` mapping defaults to the exact display value `In Progress`; `done` defaults to
`Done`. When a Project is configured, an Epic/task must be projected to `In Progress` before the
coding provider is invoked. The same items remain there through independent review and pending
merge approval, then move to `Done` only after integration is verified.

Project fields are projections only; they never redefine Epic/task contracts and never replace the
local claim. Project mutations require `gh` authentication with sufficient project scope. If no
Project is configured, local claims still provide exclusion across all runs sharing the Git common
directory. Local `--spec` and `--manifest` execution remain available without GitHub.

A configured Project mutation failure is visible and blocks the provider operation that depended on
it. Cancellation attempts compare-aware restoration of the previous Project status and will not
overwrite a later external status change.

Never put credentials in config. Use native provider/GitHub authentication or environment
variables. Providers are probed/resolved at run time; an unavailable provider is a fallback/retry
condition, never a success signal.

## GitHub integration gates

GitHub-backed integration may configure required gates independently of GitHub branch-protection availability:

```json
{
  "integration": {
    "github": {
      "required_gates": [
        {
          "id": "primary-ci",
          "kind": "workflow",
          "workflow": ".github/workflows/ci.yml",
          "allowed_conclusions": ["success"],
          "events": ["pull_request", "merge_group"]
        }
      ],
      "wait_seconds": 30,
      "max_wait_cycles": null
    }
  }
}
```

`workflow` gates match a workflow path or numeric workflow id. `check_run` gates match an exact check name and optional app slug/name. Gate IDs must be unique. `allowed_conclusions` is explicit; `neutral` and `skipped` are not successful unless listed. An absent `integration` section or an empty `required_gates` list is backward-compatible and declares no orchestrator-owned remote CI requirement.

The policy is validated by `validate-config`. Gate observations are evaluated only for the exact integration-candidate SHA and never inferred from local validation, comments, or historical runs.
