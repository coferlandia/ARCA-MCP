# Troubleshooting

Use `doctor --json` for Python, Git, repository, branch cleanliness, configuration,
schemas, providers, interrupted runs, locks, and collisions. Use `providers probe` to
separate missing CLIs from authentication/model issues. A dirty base or moved base must
not be bypassed. A rebase conflict becomes `BLOCKED_BY_MERGE_CONFLICT`; preserve paths,
branch, candidate SHA, and evidence and request authority rather than guessing. Run
`cleanup --dry-run` before cleanup; it removes only recorded run-owned resources.
