# Analysis & Classification

> Detail for Phases 1 and 2 of the toolsmith procedure. See the main `SKILL.md`
> for how this file is invoked. This file is reference material, not a skill.

---

## Phase 1 — Mandatory analysis (read-only)

**Do not modify any target-skill file during this phase.** The goal is to
reconstruct how every tool is *actually used*, not what filenames suggest.

### What to inspect

Inspect the entire target skill, wherever present:

- `SKILL.md`
- `scripts/`
- `tools/`
- `bin/`
- `src/`
- `lib/`
- `hooks/`
- `references/`
- `templates/`
- `examples/`
- `tests/`
- `README.md`
- `AGENTS.md`
- Related local skills (siblings that share helpers)
- Inline shell commands and fenced code blocks inside any doc
- Dynamically generated scripts (commands the agent is told to write on the fly)
- External executable dependencies (anything the skill shells out to)

### What to identify

For every tool and operation, capture:

- Existing entry points (what is actually called, with what arguments).
- Duplicated operations (same logic appearing in multiple places).
- Repeated command sequences (fixed pipelines the agent re-derives each time).
- Structured reads and writes (fixed file shapes the skill parses/emits).
- Mechanical transformations (deterministic shape-to-shape conversions).
- Validation rules (checks with a yes/no answer and stable criteria).
- Generated artifacts (files the skill always produces in a fixed form).
- Side effects (filesystem, network, process spawning, env mutation).
- Error handling (how failures surface today — message, exit code, exception).
- Idempotency requirements (operations that must be safe to retry).
- Security constraints (paths that must be rejected, secrets that must not leak).
- Deprecated interfaces (old commands still mentioned but superseded).
- External callers and compatibility requirements (who depends on this tool
  staying callable in its current form — other skills, CI, docs).

### Output of Phase 1

A written inventory: one row per operation with its entry point, inputs, outputs,
side effects, determinism (mechanical vs. judgment), duplication count, and any
compatibility constraints. This inventory is the input to Phase 2.

---

## Phase 2 — Automation classification

Classify each operation as exactly one of:

| Tag | Meaning |
|-----|---------|
| `CLI_REQUIRED` | Deterministic, repeated, and central to the skill. Must become a CLI command. |
| `CLI_RECOMMENDED` | Deterministic and worthwhile, but lower urgency. Strong candidate. |
| `OPTIONAL` | Deterministic but marginal; encode only if the package is already being built. |
| `KEEP_IN_SKILL` | Requires semantic judgment. Must stay as prose in `SKILL.md`. |
| `NOT_JUSTIFIED` | No clear benefit; wrapping would add a layer without value. Leave as-is. |

### Move to the CLI when the operation is sufficiently deterministic

- Structured file manipulation (fixed read/parse/edit/write).
- Schema validation (stable rules, yes/no answer).
- Artifact generation (files always produced in a fixed form).
- Repetitive transformations (same conversion every time).
- Index or catalog rebuilding (mechanical recompute from sources).
- Mechanical repository inspection (grep-like checks with fixed targets).
- Stable workflow execution (a fixed sequence with no branching judgment).
- Output normalization (forcing a consistent shape).
- Duplicate prevention (idempotent upsert logic).
- Audit record creation (append-only logging in a fixed format).

### Keep in the skill when the operation requires semantic judgment

- Intent interpretation (what does the user actually want).
- Ambiguity resolution (which of several valid paths to take).
- Architectural decisions (how to structure something new).
- Prioritization (what matters most right now).
- Context-sensitive reasoning (the right answer depends on project state).
- Scope evaluation (how far does this change reach).
- Critical analysis (is this idea good).
- Supervisor communication (what to report and what to ask).

### The governing rule

```text
The skill decides what must be done and why.
The CLI deterministically executes what has already been decided.
```

If you cannot point to the specific deterministic mechanic that justifies a CLI
command, tag it `KEEP_IN_SKILL` or `NOT_JUSTIFIED`.

### A valid "no change" outcome

The toolsmith must be allowed to conclude that no CLI changes are justified —
for example when the target skill is almost entirely semantic, or when its
existing tooling is already a clean single interface. In that case, stop, report
the evidence, and modify nothing. Manufacturing work to justify a run violates
the safety constraints.
