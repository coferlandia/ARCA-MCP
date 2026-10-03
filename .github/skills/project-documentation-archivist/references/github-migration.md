# GitHub-Native Project Migration

This procedure migrates one existing Archivist project at a time.

## Safety rule

Do not delete `TODO.md`, `HISTORY.md`, or legacy `OPEN_QUESTIONS.md` until migration application, durable-knowledge distillation, and cutover validation all succeed.

## 1. Preflight

Run:

```bash
python scripts/github_migration.py preflight --project-root .
# When the repository is managed through a GitHub Project:
python scripts/github_migration.py preflight --project-root . --project-owner <owner> --project-number <number>
```

Confirm repository identity, `gh` authentication, Issues support, clean/known Git state, and optional GitHub Project access. Project owner and number must be supplied together.

If the managed repository does not yet have a Project association, create or choose one before cutover. With current GitHub CLI this can be done explicitly, for example:

```bash
gh project create --owner <owner> --title "<project name>"
gh project link <number> --owner <owner> --repo <owner/repository>
```

Record the resulting owner/number in the PM `projects.json` using `pm-manage-projects.sh configure-github`.

## 2. Inventory

```bash
python scripts/github_migration.py inventory --project-root .
```

The command produces an inventory and decisions template under `.agent/migrations/` unless paths are overridden.

Stable migration IDs are content-derived so reruns can recognize the same legacy item. The inventory contains legacy source text for semantic classification; treat it as potentially sensitive migration-working data and review it before committing or sharing it.

## 3. Semantic classification by Archivist

Read every inventory item in context and set exactly one disposition:

- `EXISTING_ISSUE`: an existing Issue already represents it.
- `EXISTING_PR`: an existing PR is the canonical operational evidence.
- `EXISTING_GIT_EVIDENCE`: Git history is sufficient; do not manufacture an Issue.
- `CREATE_OPEN_ISSUE`: actionable future/pending work.
- `CREATE_CLOSED_HISTORICAL_ISSUE`: meaningful historical project event with no existing Issue/PR representation.
- `KNOWLEDGE_ONLY`: durable current fact, decision, runbook procedure, or agent instruction; feed canonical docs instead of creating an Issue.
- `OBSOLETE`: no longer valid/actionable and no durable knowledge needs preservation.
- `DUPLICATE`: duplicate of another inventory item or existing GitHub entity.
- `NEEDS_REVIEW`: uncertain; blocks cutover.

Do not classify purely from keywords. Inspect context and existing GitHub/Git evidence.

## 4. Validate and preview

```bash
python scripts/github_migration.py validate-decisions --project-root . --decisions .agent/migrations/github-native-decisions.json
python scripts/github_migration.py apply --project-root . --decisions .agent/migrations/github-native-decisions.json
```

The second command is still dry-run unless `--apply` is supplied.

## 5. Apply

After all decisions are resolved (no `NEEDS_REVIEW`) and explicit authorization is given:

```bash
python scripts/github_migration.py apply --project-root . \
  --decisions .agent/migrations/github-native-decisions.json \
  --project-owner <owner> --project-number <number> \
  --apply
```

Omit the Project arguments only when no GitHub Project is configured. Every created Issue contains an invisible migration marker. During write apply, the tool also persists an incremental local migration journal immediately after Issue creation and before later mutations such as Project insertion or historical closure. Reruns prefer that journal and then fall back to the marker search, avoiding duplicate creation even when GitHub search indexing is delayed. Marker lookup fails closed: a GitHub read error blocks creation instead of risking a duplicate. Do not edit the inventory/decisions after a write apply has started; resolve the partial journal deliberately first.

Only the curated `issue.body` from the reviewed decisions file is published to GitHub. The script never copies the complete legacy TODO/HISTORY text into an Issue automatically.

When a GitHub Project is configured, created/mapped open Issues are added using `gh project item-add`.

## 6. Distill durable knowledge

Run Archivist again against legacy sources and resulting GitHub entities. Ensure knowledge-only material and final decisions/procedures reach README, AGENTS, DECISIONS, or RUNBOOK.

## 7. Validate cutover

```bash
python scripts/github_migration.py validate-cutover --project-root . --decisions .agent/migrations/github-native-decisions.json
```

Cutover fails while any item is `NEEDS_REVIEW`, required mapping evidence is missing/stale, canonical durable files are missing, or the legacy source inventory changed after classification. If TODO/HISTORY changes after inventory, regenerate the inventory and review decisions again.

## 8. Remove legacy operational files

Only after validation succeeds:

```bash
git rm TODO.md HISTORY.md
# remove .agent/catalog/OPEN_QUESTIONS.md too when all legacy questions have dispositions
```

Then run:

```bash
python scripts/validate_catalog.py --project-root . --require-github-native
```

Commit the migration with its migration map and processing evidence when repository policy allows it.
