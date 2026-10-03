# CLI Contract

> Detail for Phases 3 and 4 of the toolsmith procedure. The stable contract every
> generated or consolidated CLI must satisfy. Reference material, not a skill.

---

## Naming and location

- File: `scripts/<skill-name>-cli.py`
  - `<skill-name>` is the target skill's canonical name (the `name` field of its
    frontmatter, which matches its folder name).
  - Lowercase kebab-case.
  - Suffix exactly `-cli.py`.
  - Lives under the target skill's `scripts/` directory.
- Language: always Python (3.11+).
- Standard invocation:

  ```bash
  python scripts/<skill-name>-cli.py <command>
  ```

### Worked examples

```text
target skill name: project-archivist
CLI:  scripts/project-archivist-cli.py

target skill name: coferlandia-project-manager
CLI:  scripts/coferlandia-project-manager-cli.py
```

---

## Mandatory common commands

Every generated or consolidated CLI must expose:

```bash
python scripts/<skill-name>-cli.py --help
python scripts/<skill-name>-cli.py version
python scripts/<skill-name>-cli.py self-check
python scripts/<skill-name>-cli.py capabilities
```

- `--help` — standard argparse help: description, available commands, flags,
  usage examples.
- `version` — print the CLI's own version string.
- `self-check` — validate required runtime dependencies and relevant
  target-skill configuration (paths exist, Python version is sufficient,
  declared external tools are on PATH). Exit 0 when healthy, non-zero with an
  actionable message otherwise.
- `capabilities` — structured output describing available commands. Supports
  JSON. Example shape:

  ```json
  {
    "skill": "project-archivist",
    "cli": "project-archivist-cli.py",
    "version": "1.0.0",
    "commands": [
      {
        "name": "history.add",
        "mutating": true,
        "supports_dry_run": true,
        "supports_json": true
      }
    ]
  }
  ```

---

## Output contract — JSON envelope

All operational commands must support machine-readable JSON output. Use one
consistent envelope:

```json
{
  "status": "success",
  "skill": "project-archivist",
  "command": "history.add",
  "changed": true,
  "result": {},
  "artifacts": [
    {
      "path": "HISTORY.md",
      "action": "updated"
    }
  ],
  "warnings": [],
  "errors": []
}
```

- `status`: `success` | `failure`.
- `skill`: the target skill's canonical name.
- `command`: the dotted command path that ran.
- `changed`: boolean — did the command mutate anything.
- `result`: command-specific payload (may be `{}`).
- `artifacts`: list of files touched, each with `path` and `action`
  (`created` | `updated` | `deleted`).
- `warnings`: non-fatal issues (strings or objects).
- `errors`: fatal issues (strings or objects); present when `status` is
  `failure`.

Human-readable output may also be provided (default when stdout is a TTY, or
behind a `--human` flag), but **JSON output must remain stable.** Data to stdout;
diagnostics and logs to stderr.

---

## Exit codes

Documented and stable. At minimum:

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Operational failure |
| 2 | Invalid arguments |
| 3 | Validation failure |
| 4 | Missing dependency |
| 5 | Unsafe operation rejected |
| 6 | Partial execution |

---

## Operational requirements

All applicable commands must provide:

- Deterministic behavior (same inputs → same outputs and side effects).
- Explicit input validation (fail fast with actionable messages).
- Actionable errors (what failed + what was expected + what to try).
- Stable exit codes (see table above).
- Machine-readable output (the JSON envelope).
- Atomic writes (write to a temp file in the same directory, then `os.replace`;
  never leave a half-written artifact).
- Path-safety validation (reject paths escaping declared scope; reject absolute
  paths unless explicitly allowed).
- No silent changes outside the declared scope.
- No secrets in command-line output or logs (redact or omit).
- Idempotency where logically possible (re-running produces the same result,
  not a duplicate).
- `--dry-run` for mutating operations (report what *would* change, change
  nothing).
- Clear reporting of changed artifacts (via the `artifacts` field).
- Cross-platform behavior where practical (forward-slash paths, no shell-isms
  inside the CLI, UTF-8 I/O).

**Do not make the CLI interactive by default.** No prompts that wait on stdin.
If confirmation is ever needed, require it via a flag (`--confirm`) or a
`--dry-run` first; never block on user input.

---

## What not to wrap

Do not wrap universal external tools unless the target skill defines a stable
higher-level operation on top of them:

- `git`
- `gh`
- `pytest`
- `docker`
- Language compilers or package managers

Wrapping bare `git status` behind `my-cli git-status` adds a layer with no
added behavior. Wrapping `git` *plus* a fixed commit-message convention *plus*
a validation hook *plus* an audit-log append is justified, because the skill
defines a stable higher-level operation.
