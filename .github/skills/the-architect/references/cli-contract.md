# CLI contract

Public interface: `python scripts/the-architect-cli.py <command>`.

- Python 3.11+, standard library only.
- JSON result envelope on stdout; diagnostics on stderr.
- Exit 0 success, 2 expected contract/config error, 3 unexpected filesystem/value error.
- Mutations support `--dry-run`, use atomic writes and remain confined to the configured home.
- Initialization/registration are idempotent; duplicate IDs and conflicting content fail.
- No LLM, Git commit, push, merge, reset, secret output, or interactive prompt.
- Managed index blocks preserve human-authored text outside markers.
- Word-limit validation warns normally and fails only with `--strict`.
