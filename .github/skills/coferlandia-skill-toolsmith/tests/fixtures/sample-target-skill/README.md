# sample-target-skill (fixture)

> **Fixture, not a real skill.** Used by `coferlandia-skill-toolsmith` tests so the
> full analyze → classify → design → implement → rewire → validate procedure can be
> exercised without touching a real skill.

## What it simulates

A small skill with exactly the symptoms the toolsmith targets:

- A scattered internal script (`scripts/validate_config.py`) that is a candidate
  for `WRAP` as `config validate`.
- A second scattered internal script (`scripts/rebuild_index.py`) that is a
  candidate for `WRAP` as `index rebuild`.
- A mechanical command duplicated in the prose (`echo "[date] message" >> logs/run.log`)
  that is a candidate for a `log add` command.
- One semantic step (Step 4: decide whether to run the rebuild now) that must
  stay in the skill — `KEEP_IN_SKILL`.

## Walking through the toolsmith against it

1. **Phase 1 (analyze):** read this whole directory; find the two scripts and the
   duplicated log format.
2. **Phase 2 (classify):** tag `validate` → `CLI_REQUIRED`, `rebuild` →
   `CLI_REQUIRED`, `log add` → `CLI_RECOMMENDED`, the Step-4 judgment →
   `KEEP_IN_SKILL`.
3. **Phase 3–4 (design + implement):** create
   `scripts/sample-target-skill-cli.py` exposing `config validate`, `index
   rebuild`, `log add`, plus the mandatory `version` / `self-check` /
   `capabilities`.
4. **Phase 5 (rewire):** edit this `SKILL.md` to call the CLI; keep Step 4 in
   prose.
5. **Phase 6 (test + validate):** assert the CLI matches the two scripts'
   behavior, and that Step 4's judgment is still present.
