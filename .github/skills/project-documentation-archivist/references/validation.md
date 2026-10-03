# Validation

## Standard validation

```bash
python skills/content/project-documentation-archivist/scripts/validate_catalog.py --project-root .
```

Checks:

- README, AGENTS, DECISIONS, RUNBOOK exist;
- SOURCE_INDEX and PROCESSING_RUNS exist;
- AGENTS contains its required navigation sections;
- archived local text sources contain processed frontmatter;
- archive links resolve;
- sources marked archived are not still present in the inbox;
- legacy TODO/HISTORY/OPEN_QUESTIONS are reported as migration warnings.

## GitHub-native cutover validation

After project migration:

```bash
python skills/content/project-documentation-archivist/scripts/validate_catalog.py \
  --project-root . \
  --require-github-native
```

This additionally fails when `TODO.md`, `HISTORY.md`, or `.agent/catalog/OPEN_QUESTIONS.md` remain.

Before deleting legacy files, also run `github_migration.py validate-cutover` against the reviewed decisions file.

## Manual checks

- README describes present reality, not an Issue summary.
- AGENTS remains agent-oriented and concise at the top.
- DECISIONS records reasons rather than events.
- RUNBOOK contains no secret values.
- GitHub Issues represent actionable/open work.
- Existing Issues/PRs/commits were reused rather than duplicated during migration.
- Synthetic historical Issues retain original dates in their body.
- `SOURCE_INDEX` has revision data for remote sources so repeated processing is incremental.
