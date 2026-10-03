# Consolidation & Architecture

> Detail for Phases 3 and 4. How to handle each existing tool, how to lay out the
> CLI package, and how to design the command tree. Reference material, not a skill.

---

## Existing-tool consolidation strategies

For every existing skill-owned tool, choose and document exactly one strategy.
Record the reason next to the choice.

| Strategy | When to use it |
|----------|----------------|
| `WRAP` | Keep the existing tool as-is and call it from the CLI through an adapter. Use when the tool is stable, tested, and not worth rewriting. |
| `IMPORT` | Reuse the existing tool's logic by importing it as a module (when it is already Python and importable). |
| `CONSOLIDATE` | Merge several tools that do overlapping work into one CLI command surface. |
| `REFACTOR` | Improve the existing tool's structure (extract helpers, fix error paths) while keeping its behavior, then expose via CLI. |
| `REPLACE` | Substitute a flawed or limited tool with a new implementation. Only after parity is proven. |
| `KEEP_INTERNAL` | The tool stays as a private implementation detail, hidden behind the CLI. Not documented in agent-facing instructions. |
| `DEPRECATE` | The tool is superseded by a CLI command but left in place temporarily; add a deprecation notice. |
| `RETIRE` | Remove the tool entirely — only after the replacement CLI command is tested and at parity. |

### Preference order

Prefer preserving stable and tested implementations:

1. `WRAP` / `IMPORT` / `KEEP_INTERNAL` — keep working code, hide it behind the CLI.
2. `CONSOLIDATE` / `REFACTOR` — improve structure when value is clear.
3. `REPLACE` — only when the existing tool is genuinely flawed.
4. `DEPRECATE` then `RETIRE` — phased removal, never abrupt.

### Do not rewrite solely to convert internals to Python

Non-Python internal tools (shell scripts, small binaries) may remain behind
Python adapters when justified. The public CLI is always Python; the internals
do not have to be.

### Wrapping a non-Python tool

When `WRAP` targets a non-Python tool, the adapter must:

- Normalize arguments (translate the CLI's typed args to the tool's flags).
- Normalize output (parse the tool's text/JSON into the JSON envelope).
- Normalize errors (map the tool's exit codes/messages to the contract's).
- Capture exit codes explicitly.
- Document the dependency (declare the binary in `self-check`).
- Prevent unsafe shell interpolation (pass args as a list, never a joined
  string; never use `shell=True` with interpolated input).
- Test the adapter (at least a happy path and a failure path).
- Keep the internal tool hidden from the agent-facing workflow (it never
  appears in `SKILL.md` instructions after rewiring).

---

## CLI architecture

The public entry-point file should remain small — argument parsing and dispatch
only. Push logic into a package.

### Recommended structure

```text
<target-skill>/
├── SKILL.md
├── scripts/
│   ├── <skill-name>-cli.py        ← small entry point: parse + dispatch
│   └── <skill_name>_cli/          ← underscore form of the name, a package
│       ├── __init__.py
│       ├── cli.py                 ← command registration, dispatch
│       ├── contracts.py           ← input/output dataclasses, envelope builder
│       ├── errors.py              ← typed exceptions, exit-code mapping
│       ├── output.py              ← JSON envelope + human-readable rendering
│       ├── commands/              ← one module per domain
│       │   ├── __init__.py
│       │   └── ...
│       └── adapters/              ← wrappers around legacy / non-Python tools
│           ├── __init__.py
│           └── ...
└── tests/
```

### Implementation rules

- Use the Python standard library unless an external dependency provides clear
  value and is already compatible with the repository. If you add a dependency,
  declare it inline (PEP 723 `# /// script` block) and in `self-check`.
- Avoid monolithic entry-point files. If `<skill-name>-cli.py` grows past
  argument parsing and dispatch, move logic into the package.
- `<skill_name>_cli` uses the underscore form of the skill name (Python
  identifier rules); the entry-point file uses the hyphen form.
- No LLM calls, no network calls to model APIs, no reading of instructions from
  documents at runtime.

---

## Command design

Organize commands hierarchically by domain, not as a flat namespace.

### Prefer

```bash
python scripts/project-archivist-cli.py history add
python scripts/project-archivist-cli.py history validate
python scripts/project-archivist-cli.py todo close
python scripts/project-archivist-cli.py repository inspect
```

### Avoid

```bash
project-archivist-cli.py add-history
project-archivist-cli.py validate-history
project-archivist-cli.py close-todo
```

### Naming rules

- Command names are stable, concise, and machine-friendly (lowercase, hyphen-
  separated within a segment: `self-check`, `dry-run`).
- Use verbs for leaf commands (`add`, `close`, `validate`, `inspect`,
  `rebuild`); use nouns for domain groups (`history`, `todo`, `repository`).
- Dotted form (`history.add`) is the canonical identifier used in the JSON
  envelope's `command` field and in `capabilities` output.
- Once published, command names and argument names are part of the contract:
  do not rename them casually. Add new commands rather than renaming old ones.

### Flags

- `--help` on every command and subcommand.
- `--json` on every operational command (default behavior when not a TTY is also
  acceptable, but `--json` must always force JSON).
- `--dry-run` on every mutating command.
- `--confirm` only when a command is destructive enough to need an explicit
  gate; never block on stdin.
- Positional arguments for required inputs; flags for optional ones.
